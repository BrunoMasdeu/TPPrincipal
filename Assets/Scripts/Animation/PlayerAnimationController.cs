using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Presenta el movimiento del jugador en el Animator. No replica estado
/// propio: para el propietario usa el Rigidbody y para jugadores remotos
/// deriva la velocidad del movimiento recibido por NetworkTransform.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerAnimationController : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Move movement;

    [Header("Suavizado")]
    [SerializeField, Min(0f)] private float speedDampTime = 0.1f;
    [SerializeField, Min(0.1f)] private float remoteTeleportDistance = 3f;

    private static readonly int SpeedParam =
        Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam =
        Animator.StringToHash("IsGrounded");
    private static readonly int JumpParam =
        Animator.StringToHash("Jump");
    private static readonly int RunningParam =
        Animator.StringToHash("Running");
    private static readonly int WeaponPoseParam =
        Animator.StringToHash("WeaponPose");
    private static readonly int FireParam =
        Animator.StringToHash("Fire");
    private static readonly int ReloadingParam =
        Animator.StringToHash("Reloading");

    private const int NeutralWeaponPose = 0;
    private const int PistolWeaponPose = 1;
    private const int RifleWeaponPose = 2;

    private NetworkPlayerInventory inventory;
    private NetworkPlayerCombat combat;
    private Vector3 previousPosition;
    private float sampledVerticalSpeed;
    private bool positionSampleInitialized;
    private bool groundSampleInitialized;
    private bool wasGrounded;

    private void Awake()
    {
        // Unity serializa las referencias sin asignar como un "fake null".
        // Por eso no se debe usar ??= con objetos de Unity: la comprobacion
        // == null invoca el operador de Unity y permite completar referencias.
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (movement == null)
            movement = GetComponent<Move>();

        inventory = GetComponent<NetworkPlayerInventory>();
        combat = GetComponent<NetworkPlayerCombat>();
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.Jumped += OnLocalJumped;

        ResetSamples();
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.Jumped -= OnLocalJumped;
    }

    public override void OnNetworkSpawn()
    {
        ResetSamples();

        if (combat != null)
        {
            combat.AttackPresented += OnAttackPresented;
            combat.ReloadChanged += OnReloadChanged;
            OnReloadChanged();
        }
        else
            Debug.LogWarning(
                $"[Animacion] '{name}' necesita NetworkPlayerCombat " +
                "para presentar los disparos y recargas.",
                this);

        if (inventory == null)
        {
            ApplyWeaponPose(WeaponId.None);
            Debug.LogWarning(
                $"[Animacion] '{name}' necesita NetworkPlayerInventory " +
                "para presentar la pose del arma.",
                this);
            return;
        }

        inventory.SelectedWeaponChanged += OnSelectedWeaponChanged;
        ApplyWeaponPose(inventory.SelectedWeaponId);
    }

    public override void OnNetworkDespawn()
    {
        if (combat != null)
        {
            combat.AttackPresented -= OnAttackPresented;
            combat.ReloadChanged -= OnReloadChanged;
        }

        if (inventory != null)
            inventory.SelectedWeaponChanged -= OnSelectedWeaponChanged;

        if (animator != null)
            animator.SetBool(ReloadingParam, false);

        ApplyWeaponPose(WeaponId.None);
    }

    private void Update()
    {
        if (animator == null)
            return;

        float horizontalSpeed = SampleHorizontalSpeed();
        bool grounded = movement != null && movement.IsGrounded();

        animator.SetFloat(
            SpeedParam,
            horizontalSpeed,
            speedDampTime,
            Time.deltaTime
        );
        animator.SetBool(IsGroundedParam, grounded);
        animator.SetBool(RunningParam, IsRunning(horizontalSpeed));

        if (ShouldPresentRemoteJump(grounded))
            animator.SetTrigger(JumpParam);

        wasGrounded = grounded;
        groundSampleInitialized = true;
    }

    private float SampleHorizontalSpeed()
    {
        if (!IsSpawned || IsOwner)
        {
            Vector3 localVelocity = playerRigidbody != null
                ? playerRigidbody.linearVelocity
                : Vector3.zero;
            sampledVerticalSpeed = localVelocity.y;
            return new Vector2(
                localVelocity.x,
                localVelocity.z
            ).magnitude;
        }

        Vector3 currentPosition = transform.position;

        if (!positionSampleInitialized || Time.deltaTime <= Mathf.Epsilon)
        {
            previousPosition = currentPosition;
            positionSampleInitialized = true;
            sampledVerticalSpeed = 0f;
            return 0f;
        }

        Vector3 displacement = currentPosition - previousPosition;
        previousPosition = currentPosition;

        if (displacement.sqrMagnitude >
            remoteTeleportDistance * remoteTeleportDistance)
        {
            sampledVerticalSpeed = 0f;
            return 0f;
        }

        Vector3 remoteVelocity = displacement / Time.deltaTime;
        sampledVerticalSpeed = remoteVelocity.y;
        return new Vector2(
            remoteVelocity.x,
            remoteVelocity.z
        ).magnitude;
    }

    private bool IsRunning(float horizontalSpeed)
    {
        if ((!IsSpawned || IsOwner) && movement != null)
            return movement.IsRunning;

        if (movement == null)
            return false;

        float threshold = (movement.speed + movement.runSpeed) * 0.5f;
        return horizontalSpeed >= threshold;
    }

    private bool ShouldPresentRemoteJump(bool grounded) =>
        IsSpawned &&
        !IsOwner &&
        groundSampleInitialized &&
        wasGrounded &&
        !grounded &&
        sampledVerticalSpeed > 0.1f;

    private void OnLocalJumped()
    {
        if (animator == null)
            return;

        if (!IsSpawned || IsOwner)
            animator.SetTrigger(JumpParam);
    }

    private void OnSelectedWeaponChanged(WeaponId weaponId)
    {
        ApplyWeaponPose(weaponId);
    }

    private void OnAttackPresented(
        WeaponId weaponId,
        WeaponAttackType attackType)
    {
        if (animator == null ||
            attackType != WeaponAttackType.Hitscan ||
            !UsesFireAnimation(weaponId))
            return;

        // El RPC trae el arma que el servidor realmente acepto. Aplicar su
        // pose antes del trigger evita reproducir el estado de otra arma si
        // una seleccion replicada llega apenas despues del disparo.
        ApplyWeaponPose(weaponId);
        animator.SetTrigger(FireParam);
    }

    private void OnReloadChanged()
    {
        if (animator == null || combat == null)
            return;

        animator.SetBool(
            ReloadingParam,
            combat.ReloadRemaining > 0f
        );
    }

    private static bool UsesFireAnimation(WeaponId weaponId) =>
        weaponId is WeaponId.Pistol or
            WeaponId.Revolver or
            WeaponId.AK or
            WeaponId.M4;

    private void ApplyWeaponPose(WeaponId weaponId)
    {
        if (animator == null)
            return;

        int weaponPose = weaponId switch
        {
            WeaponId.Pistol or WeaponId.Revolver => PistolWeaponPose,
            WeaponId.AK or WeaponId.M4 => RifleWeaponPose,
            _ => NeutralWeaponPose
        };

        animator.SetInteger(WeaponPoseParam, weaponPose);
    }

    private void ResetSamples()
    {
        previousPosition = transform.position;
        positionSampleInitialized = true;
        sampledVerticalSpeed = 0f;
        groundSampleInitialized = false;
        wasGrounded = false;
    }
}
