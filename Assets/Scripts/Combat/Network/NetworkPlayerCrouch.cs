using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Parte de red del agachado de Felipe (Crouch).
///
/// - Crouch lee Ctrl en la PC del dueño y llama a RequestCrouch.
/// - El servidor revisa que se pueda y publica el estado en una
///   NetworkVariable.
/// - En todas las PCs, Crouch achica la cápsula según ese estado, así los
///   demás ven (y le pegan a) la cápsula agachada.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerCrouch : NetworkBehaviour
{
    // Read Everyone: todos deben ver la cápsula agachada.
    // Write Server: el cliente solo puede pedir el cambio.
    private readonly NetworkVariable<bool> crouched = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Crouch crouch;
    private NetworkPlayerHealth health;
    private bool localInputEnabled;
    private bool lastRequestedState;

    public event Action<bool> CrouchChanged;

    public bool IsCrouched => crouched.Value;
    public CapsuleCollider Capsule => FelipeCrouch != null ? FelipeCrouch.capsule : null;
    public Transform PlayerCamera => FelipeCrouch != null ? FelipeCrouch.playerCamera : null;

    // Se busca la primera vez que se necesita (también funciona en el editor).
    private Crouch FelipeCrouch => crouch != null ? crouch : (crouch = GetComponent<Crouch>());

    private void Awake()
    {
        crouch = GetComponent<Crouch>();
        health = GetComponent<NetworkPlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        crouched.OnValueChanged += OnCrouchedChanged;
        lastRequestedState = crouched.Value;
    }

    public override void OnNetworkDespawn()
    {
        crouched.OnValueChanged -= OnCrouchedChanged;
        CrouchChanged = null;
    }

    private void Update()
    {
        // Fuera de la fase Playing de TDM/CTF nadie queda agachado.
        if (IsSpawned && IsServer && crouched.Value &&
            health != null && health.IsCombatActive && !IsMatchPlaying())
            crouched.Value = false;
    }

    /// <summary>Lo llama PlayerNetworkSetup (pausa, muerte, pantallas finales).</summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
    }

    /// <summary>
    /// Lo llama Crouch (Felipe) en la PC del dueño cada frame. Solo se manda
    /// un RPC cuando cambia lo que quiere el jugador.
    /// </summary>
    public void RequestCrouch(bool wantsToCrouch)
    {
        if (!IsSpawned || !IsOwner || !localInputEnabled ||
            MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured ||
            wantsToCrouch == lastRequestedState)
            return;

        lastRequestedState = wantsToCrouch;
        RequestCrouchRpc(wantsToCrouch);
    }

    public void ForceStandingOnServer()
    {
        if (IsServer && IsSpawned)
            crouched.Value = false;
    }

    public bool CanStandUp() => FelipeCrouch == null || FelipeCrouch.CanStandUp();

    // SendTo.Server: el servidor valida antes de cambiar la postura.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCrouchRpc(bool wantsToCrouch, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (wantsToCrouch)
        {
            // En TDM/CTF solo se agacha un jugador vivo durante Playing.
            if (health != null && health.IsCombatActive &&
                (health.LifeState != PlayerLifeState.Alive || !IsMatchPlaying()))
                return;
        }
        else if (!CanStandUp())
        {
            return;
        }

        crouched.Value = wantsToCrouch;
    }

    private void OnCrouchedChanged(bool previous, bool current)
    {
        if (IsOwner)
            lastRequestedState = current;

        CrouchChanged?.Invoke(current);
    }

    private static bool IsMatchPlaying() =>
        NetworkMatchManager.Instance != null &&
        NetworkMatchManager.Instance.Phase == MatchPhase.Playing;
}
