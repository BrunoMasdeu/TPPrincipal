using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Inventario y lanzamiento autoritativo de granadas del jugador.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerHealth))]
[RequireComponent(typeof(NetworkPlayerCombat))]
public sealed class NetworkPlayerGrenades : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NetworkObject grenadePrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Camera aimCamera;

    [Header("Lanzamiento")]
    [SerializeField, Min(0f)] private float throwForce = 12f;
    [SerializeField, Min(0f)] private float upwardForce = 4f;
    [SerializeField, Min(0.05f)] private float throwCooldownSeconds = 0.5f;
    [SerializeField, Min(0)] private int startingGrenades = 3;

    private readonly NetworkVariable<int> grenadeCount = new(
        0,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private NetworkPlayerHealth health;
    private double nextThrowAt;
    private bool localInputEnabled = true;

    public event Action<int> GrenadeCountChanged;

    public int GrenadeCount => grenadeCount.Value;
    public NetworkObject GrenadePrefab => grenadePrefab;
    public Transform ThrowPoint => throwPoint;
    public Camera AimCamera => aimCamera;
    public bool HasRequiredReferences =>
        grenadePrefab != null && throwPoint != null && aimCamera != null;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        grenadeCount.OnValueChanged += OnGrenadeCountChanged;

        if (IsServer)
            ResetForRespawnOnServer();
    }

    public override void OnNetworkDespawn()
    {
        grenadeCount.OnValueChanged -= OnGrenadeCountChanged;
        GrenadeCountChanged = null;
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || !localInputEnabled ||
            MatchDebugConsole.IsInputCaptured ||
            NetworkPlayerShop.IsInputCaptured ||
            Keyboard.current == null ||
            !Keyboard.current.gKey.wasPressedThisFrame ||
            grenadeCount.Value <= 0)
            return;

        RequestLocalThrow();
    }

    public void SetLocalInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
    }

    public void ResetForRespawnOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        grenadeCount.Value = Mathf.Max(0, startingGrenades);
        nextThrowAt = 0d;
    }

    private void RequestLocalThrow()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError(
                $"[Granada] '{name}' necesita prefab, punto de lanzamiento y camara.",
                this);
            return;
        }

        Transform aim = aimCamera.transform;
        RequestThrowRpc(aim.position, aim.forward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestThrowRpc(
        Vector3 aimOrigin,
        Vector3 aimDirection,
        RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId ||
            !HasRequiredReferences || health == null)
            return;

        NetworkMatchManager match = NetworkMatchManager.Instance;
        MatchPhase phase = match != null
            ? match.Phase
            : MatchPhase.WaitingForPlayers;
        double now = NetworkManager.ServerTime.Time;

        if (!GrenadeValidationRules.CanThrow(
                phase,
                health.LifeState,
                grenadeCount.Value,
                nextThrowAt,
                now) ||
            !GrenadeValidationRules.IsAimValid(
                aimOrigin,
                aimDirection,
                transform.position))
            return;

        Vector3 direction = aimDirection.normalized;
        NetworkObject grenade = Instantiate(
            grenadePrefab,
            throwPoint.position,
            Quaternion.LookRotation(direction, Vector3.up));
        NetworkGrenadeProjectile projectile =
            grenade.GetComponent<NetworkGrenadeProjectile>();

        if (projectile == null)
        {
            Debug.LogError(
                "[Granada] El prefab necesita NetworkGrenadeProjectile.",
                grenade);
            Destroy(grenade.gameObject);
            return;
        }

        grenade.Spawn(true);
        projectile.InitializeOnServer(
            OwnerClientId,
            GrenadeValidationRules.CalculateInitialVelocity(
                direction,
                throwForce,
                upwardForce),
            GetComponentsInChildren<Collider>());

        grenadeCount.Value = GrenadeValidationRules.CountAfterThrow(
            grenadeCount.Value);
        nextThrowAt = now + throwCooldownSeconds;
    }

    private void OnGrenadeCountChanged(int previous, int current)
    {
        GrenadeCountChanged?.Invoke(current);
    }
}
