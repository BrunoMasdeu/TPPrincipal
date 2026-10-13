using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Agachado replicado. El propietario solicita el cambio y el servidor
/// publica el estado que deben aplicar todas las copias del jugador.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class NetworkPlayerCrouch : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CapsuleCollider capsule;
    [SerializeField] private Transform playerCamera;

    [Header("Agachado")]
    [SerializeField, Min(0.1f)] private float crouchHeight = 1.2f;
    [SerializeField, Min(0f)] private float cameraOffset = 0.5f;
    [SerializeField, Min(0.1f)] private float transitionSpeed = 10f;
    [SerializeField] private Key crouchKey = Key.LeftCtrl;
    [SerializeField] private LayerMask standUpCollisionMask = ~0;

    private readonly NetworkVariable<bool> crouched = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkPlayerHealth health;
    private float originalHeight;
    private Vector3 originalCenter;
    private Vector3 originalCameraPosition;
    private bool originalStateCaptured;
    private bool localInputEnabled = true;
    private bool lastRequestedState;

    public event Action<bool> CrouchChanged;

    public bool IsCrouched => crouched.Value;
    public CapsuleCollider Capsule => capsule;
    public Transform PlayerCamera => playerCamera;

    private void Awake()
    {
        capsule ??= GetComponent<CapsuleCollider>();
        health = GetComponent<NetworkPlayerHealth>();
        CaptureOriginalState();
    }

    private void OnValidate()
    {
        capsule ??= GetComponent<CapsuleCollider>();
    }

    public override void OnNetworkSpawn()
    {
        CaptureOriginalState();
        crouched.OnValueChanged += OnCrouchedChanged;
        lastRequestedState = crouched.Value;
        ApplyStateImmediately(crouched.Value);
    }

    public override void OnNetworkDespawn()
    {
        crouched.OnValueChanged -= OnCrouchedChanged;
        CrouchChanged = null;
        ApplyStateImmediately(false);
    }

    private void Update()
    {
        if (!IsSpawned || capsule == null)
            return;

        ApplyReplicatedState();

        if (IsServer && crouched.Value && health != null &&
            health.IsCombatActive &&
            NetworkMatchManager.Instance != null &&
            NetworkMatchManager.Instance.Phase != MatchPhase.Playing)
        {
            crouched.Value = false;
        }

        if (!IsOwner || !localInputEnabled ||
            MatchDebugConsole.IsInputCaptured ||
            NetworkPlayerShop.IsInputCaptured ||
            Keyboard.current == null)
            return;

        bool requestedState = Keyboard.current[crouchKey].isPressed;
        if (!requestedState && crouched.Value && !CanStandUp())
            requestedState = true;

        if (requestedState == lastRequestedState)
            return;

        lastRequestedState = requestedState;
        RequestCrouchRpc(requestedState);
    }

    public void SetLocalInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
    }

    public void ForceStandingOnServer()
    {
        if (IsServer && IsSpawned)
            crouched.Value = false;
    }

    public bool CanStandUp()
    {
        if (capsule == null)
            return false;

        Vector3 scale = transform.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float heightScale = Mathf.Abs(scale.y);
        float radius = capsule.radius * radiusScale * 0.95f;
        float height = Mathf.Max(originalHeight * heightScale, radius * 2f);
        Vector3 center = transform.TransformPoint(originalCenter);
        Vector3 standingTopCenter = center +
            transform.up * (height * 0.5f - radius);

        // Solo se prueba el espacio adicional que ocuparia la cabeza al
        // levantarse. Probar la capsula completa puede detectar el suelo y
        // forzar un agachado aun cuando el jugador ya estaba de pie.
        Collider[] hits = Physics.OverlapSphere(
            standingTopCenter,
            radius,
            standUpCollisionMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == capsule || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCrouchRpc(
        bool requestedState,
        RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (requestedState)
        {
            if (health != null && health.IsCombatActive &&
                (health.LifeState != PlayerLifeState.Alive ||
                 NetworkMatchManager.Instance == null ||
                 NetworkMatchManager.Instance.Phase != MatchPhase.Playing))
                return;
        }
        else if (!CanStandUp())
        {
            return;
        }

        crouched.Value = requestedState;
    }

    private void CaptureOriginalState()
    {
        if (originalStateCaptured || capsule == null)
            return;

        originalHeight = capsule.height;
        originalCenter = capsule.center;
        if (playerCamera != null)
            originalCameraPosition = playerCamera.localPosition;

        originalStateCaptured = true;
    }

    private void ApplyReplicatedState()
    {
        float targetHeight = crouched.Value
            ? Mathf.Min(crouchHeight, originalHeight)
            : originalHeight;

        capsule.height = Mathf.MoveTowards(
            capsule.height,
            targetHeight,
            transitionSpeed * Time.deltaTime);

        float heightDifference = originalHeight - capsule.height;
        capsule.center = new Vector3(
            originalCenter.x,
            originalCenter.y - heightDifference * 0.5f,
            originalCenter.z);

        if (!IsOwner || playerCamera == null)
            return;

        Vector3 targetCameraPosition = originalCameraPosition;
        if (crouched.Value)
            targetCameraPosition.y -= cameraOffset;

        playerCamera.localPosition = Vector3.MoveTowards(
            playerCamera.localPosition,
            targetCameraPosition,
            transitionSpeed * Time.deltaTime);
    }

    private void ApplyStateImmediately(bool isCrouched)
    {
        if (!originalStateCaptured || capsule == null)
            return;

        float height = isCrouched
            ? Mathf.Min(crouchHeight, originalHeight)
            : originalHeight;
        float heightDifference = originalHeight - height;

        capsule.height = height;
        capsule.center = new Vector3(
            originalCenter.x,
            originalCenter.y - heightDifference * 0.5f,
            originalCenter.z);

        if (IsOwner && playerCamera != null)
        {
            Vector3 position = originalCameraPosition;
            if (isCrouched)
                position.y -= cameraOffset;
            playerCamera.localPosition = position;
        }
    }

    private void OnCrouchedChanged(bool previous, bool current)
    {
        if (IsOwner)
            lastRequestedState = current;

        CrouchChanged?.Invoke(current);
    }
}
