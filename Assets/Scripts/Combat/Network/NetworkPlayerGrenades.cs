using System;
using System.Collections;
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

    // [ANIMACIONES] Tiempo entre apretar la tecla y que la granada salga de
    // la mano, para que coincida con la animación de lanzar.
    [SerializeField, Min(0f)] private float throwDelaySeconds = 0.5f;

    private Grenade grenade;
    private NetworkPlayerHealth health;
    private double nextThrowAt;
    private int lastCount = -1;

    public event Action<int> GrenadeCountChanged;

    /// <summary>
    /// [ANIMACIONES] Se dispara en TODAS las copias del jugador cuando empieza
    /// a lanzar. Trae los segundos que faltan para que la granada salga.
    /// </summary>
    public event Action<float> ThrowPresented;

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
        ThrowPresented = null; // [ANIMACIONES]
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
            !CanThrowNow() ||
            now < nextThrowAt ||
            direction.sqrMagnitude < 0.5f)
            return;

        // [ANIMACIONES] No se puede volver a lanzar hasta que salga esta.
        nextThrowAt = now + Mathf.Max(throwCooldownSeconds, throwDelaySeconds);

        // [ANIMACIONES] Primero todos ven el gesto; la granada sale después.
        PresentThrowRpc(throwDelaySeconds);
        StartCoroutine(ThrowAfterDelay(direction.normalized));
    }

    // [ANIMACIONES]
    private IEnumerator ThrowAfterDelay(Vector3 direction)
    {
        yield return new WaitForSeconds(throwDelaySeconds);

        // Si murió mientras hacía el gesto, la granada no sale.
        if (IsSpawned && grenade != null && health != null &&
            health.LifeState == PlayerLifeState.Alive)
        {
            // El mismo código de Felipe, ejecutado en el servidor.
            grenade.ThrowGrenade(direction);
        }
    }

    // [ANIMACIONES] SendTo.ClientsAndHost: todos ven el lanzamiento.
    [Rpc(SendTo.ClientsAndHost)]
    private void PresentThrowRpc(float secondsUntilRelease)
    {
        ThrowPresented?.Invoke(secondsUntilRelease);
    }

    // [ANIMACIONES] Igual que las armas (NetworkPlayerCombat): con la partida
    // en curso o en el tutorial, para poder probar las granadas ahí.
    private static bool CanThrowNow() =>
        (NetworkMatchManager.Instance != null &&
         NetworkMatchManager.Instance.Phase == MatchPhase.Playing) ||
        (NetworkLobbySession.Instance != null &&
         NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None &&
         TutorialManager.IsTutorialLoaded);

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
