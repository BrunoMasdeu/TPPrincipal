using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Parte de red de las granadas de Felipe (Grenade y GrenadeExplosion).
///
/// - Grenade lee la tecla G en la PC del dueño, descuenta la granada y llama
///   a RequestThrow con la dirección de la cámara.
/// - El servidor revisa que se pueda lanzar y ejecuta el ThrowGrenade de
///   Felipe, que crea la granada en red (Spawn).
/// - La explosión (GrenadeExplosion) ocurre solo en el servidor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerHealth))]
[RequireComponent(typeof(NetworkPlayerCombat))]
public sealed class NetworkPlayerGrenades : NetworkBehaviour
{
    [Header("Reglas")]
    [SerializeField, Min(0.05f)] private float throwCooldownSeconds = 0.5f;
    [SerializeField, Min(0)] private int startingGrenades = 3;

    private Grenade grenade;
    private NetworkPlayerHealth health;
    private double nextThrowAt;
    private int lastCount = -1;

    public event Action<int> GrenadeCountChanged;

    /// <summary>Granadas que le quedan al dueño (las cuenta Grenade).</summary>
    public int GrenadeCount => FelipeGrenade != null ? FelipeGrenade.grenades : 0;

    // Referencias de la granada de Felipe (las usan los tests del prefab).
    public NetworkObject GrenadePrefab =>
        FelipeGrenade != null && FelipeGrenade.grenadePrefab != null
            ? FelipeGrenade.grenadePrefab.GetComponent<NetworkObject>()
            : null;
    public Transform ThrowPoint => FelipeGrenade != null ? FelipeGrenade.throwPoint : null;

    // Se busca la primera vez que se necesita (también funciona en el editor).
    private Grenade FelipeGrenade => grenade != null ? grenade : (grenade = GetComponent<Grenade>());
    public bool HasRequiredReferences => GrenadePrefab != null && ThrowPoint != null;

    private void Awake()
    {
        grenade = GetComponent<Grenade>();
        health = GetComponent<NetworkPlayerHealth>();

        // Grenade lee el teclado: solo se enciende en el dueño.
        SetLocalInputEnabled(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner && grenade != null)
            grenade.grenades = startingGrenades;
    }

    public override void OnNetworkDespawn()
    {
        GrenadeCountChanged = null;
    }

    private void Update()
    {
        // Avisa al HUD cuando cambia la cantidad.
        if (!IsSpawned || !IsOwner || GrenadeCount == lastCount)
            return;

        lastCount = GrenadeCount;
        GrenadeCountChanged?.Invoke(lastCount);
    }

    /// <summary>Lo llama PlayerNetworkSetup: el teclado solo funciona en el dueño.</summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        if (grenade != null)
            grenade.enabled = enabled;
    }

    /// <summary>Lo llama Grenade (Felipe) en la PC del dueño.</summary>
    public void RequestThrow(Vector3 direction)
    {
        if (IsSpawned && IsOwner)
            RequestThrowRpc(direction);
    }

    // SendTo.Server: solo el servidor puede crear objetos de red.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestThrowRpc(Vector3 direction, RpcParams rpcParams = default)
    {
        double now = NetworkManager.ServerTime.Time;

        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            grenade == null || health == null ||
            health.LifeState != PlayerLifeState.Alive ||
            NetworkMatchManager.Instance == null ||
            NetworkMatchManager.Instance.Phase != MatchPhase.Playing ||
            now < nextThrowAt ||
            direction.sqrMagnitude < 0.5f)
            return;

        nextThrowAt = now + throwCooldownSeconds;

        // El mismo código de Felipe, ejecutado en el servidor.
        grenade.ThrowGrenade(direction.normalized);
    }

    /// <summary>Lo llama NetworkPlayerHealth al reaparecer.</summary>
    public void ResetForRespawnOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        nextThrowAt = 0d;
        RefillOwnerRpc();
    }

    // SendTo.Owner: las granadas las cuenta la PC del dueño.
    [Rpc(SendTo.Owner)]
    private void RefillOwnerRpc()
    {
        if (grenade != null)
            grenade.grenades = startingGrenades;
    }
}
