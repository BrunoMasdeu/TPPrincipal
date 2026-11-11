using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Presenta locomocion y armas. La velocidad remota viene de NetworkTransform;
/// el lado de wallrun se replica porque no puede deducirse de la velocidad.
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
    [SerializeField, Min(0f)] private float deathBlendTime = 0.08f;

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
    // [ANIMACIONES] Blend tree de la capa base: 0 quieto, 1 caminar, 2 correr.
    // [ANIMACIONES] Negativo es lo mismo pero hacia atrás (-1 y -2).
    private static readonly int LocomocionParam =
        Animator.StringToHash("Locomocion");
    // [ANIMACIONES] Pasa la capa base a "Crouch Idle".
    private static readonly int AgachadoParam =
        Animator.StringToHash("Agachado");
    // [ANIMACIONES] Lleva la capa Upper Body a "Grenade Throw".
    private static readonly int ThrowParam =
        Animator.StringToHash("Throw");

    // [ANIMACIONES] Antes la velocidad de los otros jugadores se adivinaba
    // midiendo cuánto se movieron entre un cuadro y otro. Como la red no
    // actualiza la posición en todos los cuadros, a veces daba 0 y la
    // caminata se cortaba y volvía a empezar. Ahora el dueño manda su
    // velocidad real y si está corriendo.
    private readonly NetworkVariable<float> networkSpeed = new(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> networkRunning = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // [ANIMACIONES] La velocidad que se manda no tiene dirección: el dueño
    // avisa aparte si va hacia atrás.
    private readonly NetworkVariable<bool> networkHaciaAtras = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // Se manda solo si la velocidad cambió al menos esto, para no llenar la red.
    private const float CambioMinimoDeVelocidad = 0.25f;
    private const float VelocidadQuieto = 0.1f;
    // [ANIMACIONES] Retroceso mínimo (m/s) para usar la animación hacia atrás.
    private const float VelocidadHaciaAtras = 0.5f;

    private const int NeutralWeaponPose = 0;
    private const int PistolWeaponPose = 1;
    private const int RifleWeaponPose = 2;
    private const int KnifeWeaponPose = 3; // [ANIMACIONES]

    private NetworkPlayerInventory inventory;
    private NetworkPlayerCombat combat;
    private WallRun wallRun;
    private NetworkPlayerHealth health;
    private NetworkPlayerCrouch crouch; // [ANIMACIONES]
    private NetworkPlayerGrenades grenades; // [ANIMACIONES]
    private GameObject granadaEnMano; // [ANIMACIONES]
    private int deathLayer = -1;
    private bool presentingDeath;
    private float deathWeight;
    [SerializeField, HideInInspector] private int wallRunLayer = -1;
    [Header("Wall Run Debug (Play Mode)")]
    [SerializeField] private float wallRunWeight;
    [SerializeField] private int presentedWallSide;
    private readonly NetworkVariable<int> networkWallSide = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private static readonly int WallRunSideParam = Animator.StringToHash("WallRunSide");
    private static readonly int WallRunRateParam = Animator.StringToHash("WallRunRate");
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
        wallRun = GetComponent<WallRun>();
        health = GetComponent<NetworkPlayerHealth>();
        crouch = GetComponent<NetworkPlayerCrouch>(); // [ANIMACIONES]
        grenades = GetComponent<NetworkPlayerGrenades>(); // [ANIMACIONES]
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            wallRunLayer = animator.GetLayerIndex("Wall Run");
            deathLayer = animator.GetLayerIndex("Death");
        }
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.Jumped += OnLocalJumped;

        ResetSamples();
    }

    private void OnDisable()
    {
        ClearWallRunPresentation();
        if (movement != null)
            movement.Jumped -= OnLocalJumped;
    }

    public override void OnNetworkSpawn()
    {
        ResetSamples();
        if (health != null)
        {
            health.LifeStateChanged += OnLifeStateChanged;
            OnLifeStateChanged(health.LifeState);
            // A player joining during the respawn wait should see the corpse,
            // rather than replay a death that happened before they connected.
            if (presentingDeath && animator != null && deathLayer >= 0)
            {
                deathWeight = 1f;
                animator.SetLayerWeight(deathLayer, 1f);
                animator.Play("Death Back", deathLayer, 1f);
            }
        }

        // [ANIMACIONES]
        if (grenades != null)
            grenades.ThrowPresented += OnThrowPresented;

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
        if (health != null)
            health.LifeStateChanged -= OnLifeStateChanged;
        OnLifeStateChanged(PlayerLifeState.Alive);
        ClearWallRunPresentation();
        // [ANIMACIONES]
        if (grenades != null)
            grenades.ThrowPresented -= OnThrowPresented;
        Destroy(granadaEnMano);

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

        if (presentingDeath)
        {
            deathWeight = deathBlendTime <= 0f ? 1f :
                Mathf.MoveTowards(deathWeight, 1f, Time.deltaTime / deathBlendTime);
            if (deathLayer >= 0)
                animator.SetLayerWeight(deathLayer, deathWeight);
            return;
        }

        float horizontalSpeed = SampleHorizontalSpeed();
        bool grounded = movement != null && movement.IsGrounded();

        animator.SetFloat(
            SpeedParam,
            horizontalSpeed,
            speedDampTime,
            Time.deltaTime
        );
        animator.SetBool(IsGroundedParam, grounded);

        bool running = IsRunning(horizontalSpeed);
        bool haciaAtras = HaciaAtras(); // [ANIMACIONES]
        animator.SetBool(RunningParam, running);
        animator.SetFloat(
            LocomocionParam,
            Locomocion(horizontalSpeed, running, haciaAtras),
            speedDampTime,
            Time.deltaTime
        );
        // Todas las copias usan la postura confirmada por NetworkPlayerCrouch.
        animator.SetBool(AgachadoParam, crouch != null && crouch.IsCrouched);
        PublicarMovimiento(horizontalSpeed, running, haciaAtras);
        UpdateWallRunPresentation(horizontalSpeed);

        if (ShouldPresentRemoteJump(grounded))
            animator.SetTrigger(JumpParam);

        wasGrounded = grounded;
        groundSampleInitialized = true;
    }

    private void UpdateWallRunPresentation(float speed)
    {
        if (wallRunLayer < 0 && animator.runtimeAnimatorController != null)
            wallRunLayer = animator.GetLayerIndex("Wall Run");
        if (wallRunLayer < 0)
            return;

        int localSide = wallRun != null && movement != null && movement.wallrunning
            ? wallRun.AnimationSide : 0;
        if (IsSpawned && IsOwner && networkWallSide.Value != localSide)
            networkWallSide.Value = localSide;
        int side = IsSpawned && !IsOwner ? networkWallSide.Value : localSide;

        // Retain the final side during the fade back into locomotion.
        if (side != 0)
        {
            if (wallRunWeight <= 0f)
                animator.Play(side > 0 ? "Wall Run Right" : "Wall Run Left", wallRunLayer, 0f);
            presentedWallSide = side;
        }
        animator.SetInteger(WallRunSideParam, presentedWallSide);
        animator.SetFloat(WallRunRateParam, Mathf.Clamp(speed / 7f, 0.75f, 1.5f));
        wallRunWeight = Mathf.MoveTowards(wallRunWeight, side == 0 ? 0f : 1f,
            Time.deltaTime / 0.14f);
        animator.SetLayerWeight(wallRunLayer, wallRunWeight);
    }

    private void ClearWallRunPresentation()
    {
        wallRunWeight = 0f;
        presentedWallSide = 0;
        if (animator != null && wallRunLayer >= 0)
            animator.SetLayerWeight(wallRunLayer, 0f);
        if (IsSpawned && IsOwner)
            networkWallSide.Value = 0;
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
            return networkSpeed.Value;
        }

        Vector3 displacement = currentPosition - previousPosition;
        previousPosition = currentPosition;

        if (displacement.sqrMagnitude >
            remoteTeleportDistance * remoteTeleportDistance)
        {
            sampledVerticalSpeed = 0f;
            return networkSpeed.Value;
        }

        // La posición se sigue midiendo solo para detectar el salto del rival
        // (velocidad vertical). La horizontal la manda el dueño.
        Vector3 remoteVelocity = displacement / Time.deltaTime;
        sampledVerticalSpeed = remoteVelocity.y;
        return networkSpeed.Value;
    }

    private bool IsRunning(float horizontalSpeed)
    {
        if ((!IsSpawned || IsOwner) && movement != null)
            return movement.IsRunning;

        // [ANIMACIONES] El rival: lo que mandó su dueño.
        return networkRunning.Value;
    }

    /// <summary>
    /// [ANIMACIONES] Va hacia atrás si la velocidad apunta contra el frente
    /// del cuerpo. El rival usa lo que mandó su dueño.
    /// </summary>
    private bool HaciaAtras()
    {
        if (IsSpawned && !IsOwner)
            return networkHaciaAtras.Value;

        return playerRigidbody != null &&
            Vector3.Dot(playerRigidbody.linearVelocity, transform.forward) <
                -VelocidadHaciaAtras;
    }

    /// <summary>
    /// [ANIMACIONES] Valor del blend tree de la capa base:
    /// 0 quieto, 1 caminando a velocidad normal, 2 corriendo.
    /// Hacia atrás es el mismo valor en negativo.
    /// </summary>
    public static float Locomocion(
        float velocidad, bool corriendo, float velocidadCaminar, bool haciaAtras = false)
    {
        if (velocidad < VelocidadQuieto)
            return 0f;

        float valor = corriendo
            ? 2f
            : velocidadCaminar > 0f
                ? Mathf.Clamp01(velocidad / velocidadCaminar)
                : 1f;

        return haciaAtras ? -valor : valor;
    }

    private float Locomocion(float velocidad, bool corriendo, bool haciaAtras) =>
        Locomocion(velocidad, corriendo, movement != null ? movement.speed : 0f, haciaAtras);

    /// <summary>[ANIMACIONES] El dueño avisa a los demás cómo se está moviendo.</summary>
    private void PublicarMovimiento(float velocidad, bool corriendo, bool haciaAtras)
    {
        if (!IsSpawned || !IsOwner)
            return;

        float aMandar = velocidad < VelocidadQuieto ? 0f : velocidad;
        if (Mathf.Abs(networkSpeed.Value - aMandar) >= CambioMinimoDeVelocidad ||
            (aMandar == 0f && networkSpeed.Value != 0f))
        {
            networkSpeed.Value = aMandar;
        }

        if (networkRunning.Value != corriendo)
            networkRunning.Value = corriendo;

        if (networkHaciaAtras.Value != haciaAtras)
            networkHaciaAtras.Value = haciaAtras;
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
        if (animator == null || presentingDeath)
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
        if (animator == null || presentingDeath ||
            !UsesFireAnimation(weaponId, attackType))
            return;

        // El RPC trae el arma que el servidor realmente acepto. Aplicar su
        // pose antes del trigger evita reproducir el estado de otra arma si
        // una seleccion replicada llega apenas despues del disparo.
        ApplyWeaponPose(weaponId);
        animator.SetTrigger(FireParam);
    }

    /// <summary>
    /// [ANIMACIONES] Alguien empezó a lanzar una granada: gesto del brazo y
    /// una granada de adorno en la mano izquierda hasta que sale la de verdad.
    /// </summary>
    private void OnThrowPresented(float secondsUntilRelease)
    {
        if (animator == null || presentingDeath)
            return;

        animator.SetTrigger(ThrowParam);

        Transform mano = animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
            : null;
        MeshFilter modelo = grenades.GrenadePrefab != null
            ? grenades.GrenadePrefab.GetComponentInChildren<MeshFilter>(true)
            : null;
        if (mano == null || modelo == null)
            return;

        // Solo la malla: sin física, sin red y sin explosión.
        Destroy(granadaEnMano);
        granadaEnMano = new GameObject("Granada en mano");
        granadaEnMano.AddComponent<MeshFilter>().sharedMesh = modelo.sharedMesh;
        granadaEnMano.AddComponent<MeshRenderer>().sharedMaterials =
            modelo.GetComponent<MeshRenderer>().sharedMaterials;
        granadaEnMano.transform.SetPositionAndRotation(mano.position, mano.rotation);
        granadaEnMano.transform.localScale = modelo.transform.lossyScale;
        // true: conserva el tamaño aunque el hueso de la mano tenga otra escala.
        granadaEnMano.transform.SetParent(mano, true);
        Destroy(granadaEnMano, secondsUntilRelease);
    }

    private void OnReloadChanged()
    {
        if (animator == null || combat == null)
            return;

        animator.SetBool(
            ReloadingParam,
            !presentingDeath && combat.ReloadRemaining > 0f
        );
    }

    private void OnLifeStateChanged(PlayerLifeState state)
    {
        // Respawning remains dead until the server confirms the teleport.
        bool dead = state != PlayerLifeState.Alive;
        if (dead == presentingDeath)
            return;

        presentingDeath = dead;
        if (animator == null || deathLayer < 0)
            return;

        ClearWallRunPresentation();
        animator.ResetTrigger(JumpParam);
        animator.ResetTrigger(FireParam);
        animator.ResetTrigger(ThrowParam); // [ANIMACIONES]
        Destroy(granadaEnMano); // [ANIMACIONES]
        animator.SetBool(ReloadingParam, false);
        animator.SetFloat(SpeedParam, 0f);
        animator.SetBool(RunningParam, false);
        deathWeight = 0f;
        animator.SetLayerWeight(deathLayer, 0f);

        if (dead)
        {
            animator.Play("Death Back", deathLayer, 0f);
        }
        else
        {
            // Reset interrupted jumps/fire/reloads as well as the death pose.
            animator.Play("Locomotion Base", 0, 0f);
            int upperBody = animator.GetLayerIndex("Upper Body");
            if (upperBody >= 0)
                animator.Play("Neutral Upper Body", upperBody, 0f);
            ApplyWeaponPose(inventory != null ? inventory.SelectedWeaponId : WeaponId.None);
            ResetSamples();
        }
    }

    // [ANIMACIONES] El cuchillo también usa el trigger Fire (con su pose va a
    // "Knife Combat Attack") y el francotirador dispara como el rifle.
    private static bool UsesFireAnimation(WeaponId weaponId, WeaponAttackType attackType) =>
        weaponId == WeaponId.Knife ||
        (attackType == WeaponAttackType.Hitscan &&
            weaponId is WeaponId.Pistol or
                WeaponId.Revolver or
                WeaponId.AK or
                WeaponId.M4 or
                WeaponId.Sniper);

    private void ApplyWeaponPose(WeaponId weaponId)
    {
        if (animator == null)
            return;

        animator.SetInteger(WeaponPoseParam, PoseDelArma(weaponId));
    }

    /// <summary>
    /// [ANIMACIONES] Valor de WeaponPose de cada arma (public para los tests).
    /// El cuchillo ahora tiene su pose; el gancho sigue con la de pistola.
    /// </summary>
    public static int PoseDelArma(WeaponId weaponId) => weaponId switch
    {
        WeaponId.Pistol or WeaponId.Revolver or WeaponId.Grapple => PistolWeaponPose,
        WeaponId.AK or WeaponId.M4 or WeaponId.Sniper => RifleWeaponPose,
        WeaponId.Knife => KnifeWeaponPose,
        _ => NeutralWeaponPose
    };

    private void ResetSamples()
    {
        previousPosition = transform.position;
        positionSampleInitialized = true;
        sampledVerticalSpeed = 0f;
        groundSampleInitialized = false;
        wasGrounded = false;
    }
}
