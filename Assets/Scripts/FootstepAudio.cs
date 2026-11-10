using System;
using UnityEngine;

/// <summary>
/// Reproduce sonidos de pasos basándose en el desplazamiento real del
/// jugador. Usa el evento de Move para los saltos locales y conserva la
/// deteccion por desplazamiento para jugadores remotos.
///
/// Incluye un panel de diagnóstico opcional (Debug Overlay) que muestra
/// en pantalla, en tiempo real, por qué SÍ o por qué NO está sonando un
/// paso en cada instante. Sirve para encontrar rápido en qué parte del
/// mapa falla, sin tener que grabar video ni adivinar.
///
/// Tolera dos situaciones que antes rompían el sonido en escaleras reales
/// (geometría con escalones, no una rampa lisa):
///  1) El raycast puede fallar por una fracción de segundo exactamente en
///     el borde entre un escalón y el siguiente. "Grounded Grace Time" lo
///     cubre: si estuviste parado en el piso hace poquito, se sigue
///     considerando "parado" un rato corto, en vez de cortar el sonido.
///  2) Subir escalones genera velocidad vertical real y sostenida (no es
///     solo un salto puntual). "Max Vertical Speed For Step" ahora debe
///     ajustarse más alto que la velocidad típica de subir escalones,
///     pero todavía bastante más bajo que la velocidad de un salto real,
///     para seguir diferenciando ambos casos.
///
/// Los pasos usan una fuente dedicada: el siguiente corta el clip anterior,
/// aunque su cola sea mas larga que el intervalo de la cadencia.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepAudio : MonoBehaviour
{
    [Serializable]
    public class SurfaceClipSet
    {
        public SurfaceType surfaceType;
        public AudioClip[] clips;
    }

    [Header("Clips por superficie")]
    [SerializeField] private AudioClip[] defaultClips;
    [SerializeField] private SurfaceClipSet[] surfaceClipSets;

    [Header("Cadencia (por distancia recorrida)")]
    [Tooltip("Metros que recorrés entre un paso y el siguiente. Es lo que hace que los pasos suenen naturales: más rápido = pasos más seguidos, más lento = más espaciados. BAJALO (ej. 2.5) si querés más pasos; SUBILO (ej. 4.5) si querés menos.")]
    [SerializeField] private float strideLength = 3.5f;
    [Tooltip("Tiempo mínimo (segundos) entre dos pasos aunque vayas muy rápido. Evita que se amontonen al correr.")]
    [SerializeField] private float minStepInterval = 0.28f;
    [Tooltip("Tiempo máximo (segundos) entre pasos mientras te movés lento. Evita que a muy poca velocidad pasen demasiados segundos sin sonar.")]
    [SerializeField] private float maxStepInterval = 0.7f;
    [SerializeField] private float minSpeedToStep = 0.3f;
    [Tooltip("Velocidad vertical máxima para seguir considerándolo un paso caminado (incluye subir escaleras). Tiene que quedar por ARRIBA de la velocidad vertical normal al subir escalones, pero por DEBAJO de la velocidad de un salto real. Si subís escaleras y no suena, subí este número; si vuelve a sonar en ráfaga durante saltos, bajalo.")]
    [SerializeField] private float maxVerticalSpeedForStep = 4f;

    [Header("Variación de sonido")]
    [SerializeField] private float volumeMin = 0.85f;
    [SerializeField] private float volumeMax = 1f;
    [SerializeField] private float pitchMin = 0.92f;
    [SerializeField] private float pitchMax = 1.08f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.3f;
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Radio del SphereCast que busca el piso. Subilo un poco (0.2-0.3) si en escaleras o bordes irregulares el sistema pierde el piso por instantes.")]
    [SerializeField] private float groundCheckRadius = 0.15f;
    [Tooltip("Cuánto tiempo (segundos) se sigue considerando 'en el piso' después del último instante confirmado, aunque el raycast de este frame no haya detectado nada. Cubre los huecos entre escalones de una escalera real.")]
    [SerializeField] private float groundedGraceTime = 0.15f;

    [Header("Salto y aterrizaje (opcional)")]
    [Tooltip("Sonido al despegar del piso con un salto. Podés dejarlo vacío si solo querés sonido de aterrizaje.")]
    [SerializeField] private AudioClip[] jumpClips;
    [Tooltip("Sonido al aterrizar después de un salto o una caída. Usá los archivos 'Land' / 'Jump_Land' del pack.")]
    [SerializeField] private AudioClip[] landClips;
    [Tooltip("Volumen del sonido de salto respecto al normal (0 a 1).")]
    [SerializeField] private float jumpVolumeScale = 0.7f;
    [Tooltip("Tiempo mínimo en el aire (segundos) para que cuente como salto/caída y suene el aterrizaje. Evita sonidos falsos en escalones.")]
    [SerializeField] private float minAirTime = 0.2f;
    [Tooltip("Velocidad de caída (m/s) a partir de la cual, sin haber saltado, se considera que te caíste de un borde.")]
    [SerializeField] private float fallTriggerSpeed = 5f;

    [Tooltip("Umbral de salto para jugadores remotos u objetos sin Move activo. Los saltos locales usan el evento Jumped y no dependen de esta velocidad.")]
    [SerializeField] private float jumpTakeoffSpeed = 6f;
    [Tooltip("Espera para confirmar saltos estimados. En saltos locales solo protege del falso aterrizaje inicial; el sonido es inmediato.")]
    [SerializeField] private float jumpConfirmTime = 0.08f;
    [Tooltip("Si no saltaste y solo caés, el aterrizaje suena solo si la velocidad de caída fue mayor a este valor (m/s). Evita sonidos al bajar escaleras o rampas.")]
    [SerializeField] private float landMinFallSpeed = 8f;
    [Tooltip("Tiempo mínimo entre aterrizajes o saltos estimados. No silencia los saltos confirmados por Move.")]
    [SerializeField] private float airSoundCooldown = 0.5f;

    [Tooltip("Velocidad de caída (m/s) de un aterrizaje 'suave' (un salto normal).")]
    [SerializeField] private float landSoftFallSpeed = 6f;
    [Tooltip("Velocidad de caída (m/s) de un aterrizaje 'fuerte' (caída desde altura).")]
    [SerializeField] private float landHardFallSpeed = 14f;
    [Tooltip("Volumen del aterrizaje suave (0 a 1).")]
    [SerializeField] private float landSoftVolume = 0.4f;
    [Tooltip("Volumen del aterrizaje fuerte (0 a 1).")]
    [SerializeField] private float landHardVolume = 1f;

    [Header("Respaldo si el piso no tiene Collider")]
    [Tooltip("Si el raycast no detecta nada debajo pero el jugador se mueve en plano (casi sin velocidad vertical), se asume que está pisando el suelo y suenan los Default Clips. Evita tener que ponerle Collider a cada pieza de piso. Si en un salto o en el gancho llegara a sonar un paso en el aire, desactivalo.")]
    [SerializeField] private bool assumeGroundedIfNoHit = true;
    [Tooltip("Velocidad vertical (m/s) por debajo de la cual se considera 'sin subir ni bajar'.")]
    [SerializeField] private float assumeMaxVerticalSpeed = 0.5f;
    [Tooltip("Cuánto tiempo seguido (segundos) tiene que cumplirse lo anterior para asumir que está en el piso. Así el punto más alto de un salto no cuenta.")]
    [SerializeField] private float assumeRequiredTime = 0.15f;

    [Header("Debug")]
    [Tooltip("Muestra un panel en la esquina de la pantalla (en Play) con el estado actual del sistema de pasos. Desactivalo antes de la entrega final.")]
    [SerializeField] private bool debugOverlay = true;

    private AudioSource audioSource;
    private AudioSource stepSource;
    private Rigidbody playerBody;
    private Move movement;
    private Vector3 lastPosition;
    private float stepDistance;

    private float lastStepTime = -999f;
    private float lowVerticalTimer;
    private bool airborne;
    private bool jumped;
    private bool jumpPending;
    private float jumpPendingTimer;
    private float lastAirSoundTime = -999f;
    private float airTime;
    private float maxFallSpeed;
    private bool wasEffectiveGrounded;
    private float lastGroundedRealTime = -999f;
    private RaycastHit lastGroundHit;
    private bool hasLastGroundHit;

    // Valores del último frame, guardados solo para mostrarlos en el overlay.
    private bool debugRawGrounded;
    private float debugHorizontalSpeed;
    private float debugVerticalSpeed;
    private bool debugGrounded;
    private string debugHitName = "-";
    private string debugLastReason = "-";
    private string debugLastLoggedReason = "";

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        playerBody = GetComponent<Rigidbody>();
        movement = GetComponent<Move>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        lastPosition = playerBody != null ? playerBody.position : transform.position;

        // El AudioSource del prefab queda para salto y aterrizaje. Los pasos
        // usan una sola fuente para impedir que se acumulen sus colas.
        stepSource = gameObject.AddComponent<AudioSource>();
        stepSource.playOnAwake = false;
        stepSource.spatialBlend = audioSource.spatialBlend;
        stepSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        stepSource.spatialize = audioSource.spatialize;
        stepSource.rolloffMode = audioSource.rolloffMode;
        stepSource.minDistance = audioSource.minDistance;
        stepSource.maxDistance = audioSource.maxDistance;
        stepSource.dopplerLevel = audioSource.dopplerLevel;
        stepSource.priority = audioSource.priority;

        if (debugOverlay)
        {
            Debug.Log($"[FootstepDebug] Awake en '{name}' (id {GetInstanceID()}). " +
                      $"Componentes FootstepAudio en este mismo objeto: {GetComponents<FootstepAudio>().Length}. " +
                      $"AudioSources en este objeto: {GetComponents<AudioSource>().Length}.", this);
        }
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.Jumped -= OnJumped;
    }

    private void OnJumped()
    {
        // Move ya comprobo el suelo y aplico el impulso: no esperar a que
        // el SphereCast se separe ni exigir una velocidad vertical minima.
        airborne = true;
        jumped = true;
        jumpPending = false;
        jumpPendingTimer = 0f;
        airTime = 0f;
        maxFallSpeed = 0f;
        lowVerticalTimer = 0f;
        stepDistance = 0f;
        lastStepTime = Time.time;
        PlayAirSound(jumpClips, jumpVolumeScale, "SALTO", confirmedJump: true);
    }

    private void FixedUpdate()
    {
        // Muestrear al ritmo de la fisica evita alternar velocidad cero y
        // picos cuando hay mas frames de render que pasos de simulacion.
        Vector3 currentPosition = playerBody != null ? playerBody.position : transform.position;
        Vector3 delta = currentPosition - lastPosition;
        lastPosition = currentPosition;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        float horizontalSpeed = new Vector2(delta.x, delta.z).magnitude / dt;
        float signedVerticalSpeed = delta.y / dt;
        float verticalSpeed = Mathf.Abs(signedVerticalSpeed);

        bool rawGrounded = TryGetGroundHit(out RaycastHit hit);

        if (rawGrounded)
        {
            lastGroundedRealTime = Time.time;
            lastGroundHit = hit;
            hasLastGroundHit = true;
        }

        bool withinGrace = hasLastGroundHit && (Time.time - lastGroundedRealTime) <= groundedGraceTime;

        // Respaldo: piso sin Collider detectable. Si no hay golpe del raycast
        // pero el jugador lleva un rato sin subir ni bajar, se asume piso.
        if (!rawGrounded && assumeGroundedIfNoHit && verticalSpeed <= assumeMaxVerticalSpeed)
            lowVerticalTimer += dt;
        else
            lowVerticalTimer = 0f;

        bool assumedGrounded = !rawGrounded && !withinGrace
                               && assumeGroundedIfNoHit
                               && lowVerticalTimer >= assumeRequiredTime;

        bool effectiveGrounded = rawGrounded || withinGrace || assumedGrounded;
        RaycastHit hitForSurface = rawGrounded ? hit : (withinGrace ? lastGroundHit : default);

        HandleJumpAndLand(rawGrounded, rawGrounded || assumedGrounded, effectiveGrounded, signedVerticalSpeed, dt);

        // Guardado para el panel de diagnóstico.
        debugRawGrounded = rawGrounded;
        debugGrounded = effectiveGrounded;
        debugHorizontalSpeed = horizontalSpeed;
        debugVerticalSpeed = verticalSpeed;
        debugHitName = (effectiveGrounded && hitForSurface.collider != null)
            ? hitForSurface.collider.name
            : (assumedGrounded ? "(sin collider: asumido)" : "(nada)");

        bool movingLikeWalking = horizontalSpeed >= minSpeedToStep
                                  && verticalSpeed <= maxVerticalSpeedForStep;

        if (!effectiveGrounded)
            debugLastReason = "NO SUENA: no hay piso detectado debajo (raycast no pega en nada)";
        else if (horizontalSpeed < minSpeedToStep)
            debugLastReason = "NO SUENA: velocidad horizontal muy baja (parado o casi parado)";
        else if (verticalSpeed > maxVerticalSpeedForStep)
            debugLastReason = "NO SUENA: velocidad vertical alta (salto/caída/vault)";
        else if (assumedGrounded)
            debugLastReason = "OK (asumido): el raycast no ve piso, pero se mueve en plano";
        else
            debugLastReason = withinGrace && !rawGrounded
                ? "OK (gracia): sonando gracias al margen de piso reciente"
                : "OK: cumple condiciones para sonar";

        // Respaldo por Console: por si el panel OnGUI queda tapado por el
        // Canvas del HUD. Solo loguea cuando el estado CAMBIA, para no
        // inundar la Console con un mensaje por frame.
        if (debugOverlay && debugLastReason != debugLastLoggedReason)
        {
            Debug.Log($"[FootstepDebug] {debugLastReason} | piso: {debugHitName} | h: {horizontalSpeed:F2} | v: {verticalSpeed:F2}");
            debugLastLoggedReason = debugLastReason;
        }

        UpdateStepCadence(effectiveGrounded && movingLikeWalking && !airborne,
            airborne, horizontalSpeed * dt, Time.time, hitForSurface);
    }

    private void UpdateStepCadence(bool canStep, bool resetStride,
        float distance, float now, RaycastHit hitForSurface)
    {
        if (canStep)
        {
            // Los pasos dependen de la DISTANCIA recorrida, no de un reloj fijo.
            stepDistance += distance;

            float sinceLast = now - lastStepTime;
            bool minimumElapsed = sinceLast >= minStepInterval;
            bool distanceReached = stepDistance >= strideLength;
            bool tooLong = sinceLast >= maxStepInterval;

            if (minimumElapsed && (distanceReached || tooLong))
            {
                PlayFootstep(hitForSurface);
                lastStepTime = now;
                stepDistance = 0f;
            }
        }
        else if (resetStride)
        {
            stepDistance = 0f;
        }
        // Una muestra sin desplazamiento NO adelanta el reloj ni precarga
        // una zancada. El intervalo minimo se conserva incluso al frenar.
    }

    // Detecta el despegue de un salto y el aterrizaje, y reproduce su sonido.
    private void HandleJumpAndLand(bool rawGrounded, bool groundedNow, bool effectiveGrounded, float signedVerticalSpeed, float dt)
    {
        if (!airborne)
        {
            if (jumpPending)
            {
                // Se detectó un impulso hacia arriba. Se espera un instante y se
                // confirma que dejaste el piso y seguís subiendo: así no cuentan
                // las rampas ni las escaleras recorridas a toda velocidad.
                jumpPendingTimer += dt;

                if (jumpPendingTimer >= jumpConfirmTime)
                {
                    jumpPending = false;

                    if (!rawGrounded && signedVerticalSpeed > 0f)
                    {
                        airborne = true;
                        jumped = true;
                        airTime = jumpPendingTimer;
                        maxFallSpeed = 0f;
                        PlayAirSound(jumpClips, jumpVolumeScale, "SALTO");
                    }
                }
            }
            else if ((movement == null || !movement.isActiveAndEnabled)
                     && wasEffectiveGrounded && signedVerticalSpeed > jumpTakeoffSpeed)
            {
                jumpPending = true;
                jumpPendingTimer = 0f;
            }
            // Caída sin salto (te caés de un borde).
            else if (!groundedNow && signedVerticalSpeed < -fallTriggerSpeed)
            {
                airborne = true;
                jumped = false;
                airTime = 0f;
                maxFallSpeed = 0f;
            }
        }
        else
        {
            airTime += dt;

            // El evento puede llegar antes de simular el impulso de fisica.
            // Ese primer muestreo aun ve el piso y velocidad cero: no es
            // un aterrizaje. La espera es acotada para saltos con techo bajo.
            if (jumped && airTime <= Mathf.Max(jumpConfirmTime, dt))
            {
                wasEffectiveGrounded = effectiveGrounded;
                return;
            }

            if (signedVerticalSpeed < 0f)
                maxFallSpeed = Mathf.Max(maxFallSpeed, -signedVerticalSpeed);

            // Aterrizaje: toca el piso y ya no sube.
            if (groundedNow && signedVerticalSpeed <= 0.5f)
            {
                airborne = false;

                bool enoughAir = airTime >= Mathf.Max(minAirTime, 0.3f);
                bool strongEnough = jumped || maxFallSpeed >= landMinFallSpeed;

                if (enoughAir && strongEnough)
                {
                    float volume = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(maxFallSpeed / 12f));
                    PlayAirSound(landClips, volume, "ATERRIZAJE");

                    // Evita que un paso pise el sonido de aterrizaje.
                    stepDistance = 0f;
                    lastStepTime = Time.time;
                }

                jumped = false;
            }
        }

        wasEffectiveGrounded = effectiveGrounded;
    }

    private void PlayAirSound(AudioClip[] clips, float volumeScale, string label, bool confirmedJump = false)
    {
        // Cada salto aceptado tiene su sonido. El cooldown solo filtra
        // detecciones estimadas y aterrizajes, sin bloquear el salto siguiente.
        if (!confirmedJump && Time.time - lastAirSoundTime < airSoundCooldown) return;
        if (clips == null || clips.Length == 0) return;

        if (!confirmedJump)
            lastAirSoundTime = Time.time;

        if (debugOverlay)
            Debug.Log($"[FootstepDebug] {label} | tiempo en el aire: {airTime:F2}s | caída máx: {maxFallSpeed:F1} m/s");

        PlayOneShotFrom(clips, volumeScale);
    }

    private void PlayOneShotFrom(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, volumeScale * UnityEngine.Random.Range(volumeMin, volumeMax));
    }

    private bool TryGetGroundHit(out RaycastHit hit)
    {
        if (groundCheck == null)
        {
            hit = default;
            return true;
        }

        return Physics.SphereCast(
            groundCheck.position + Vector3.up * groundCheckRadius,
            groundCheckRadius,
            Vector3.down,
            out hit,
            groundDistance + groundCheckRadius,
            groundMask
        );
    }

    private void PlayFootstep(RaycastHit hit)
    {
        AudioClip[] clips = ResolveClipsForSurface(hit);

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];

        if (debugOverlay)
            Debug.Log($"[FootstepDebug] PASO (id {GetInstanceID()}) t={Time.time:F2} clip={(clip != null ? clip.name : "null")} dur={(clip != null ? clip.length : 0f):F2}s");

        if (clip == null) return;

        stepSource.Stop();
        stepSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
        stepSource.clip = clip;
        stepSource.volume = UnityEngine.Random.Range(volumeMin, volumeMax);
        stepSource.Play();
    }

    private AudioClip[] ResolveClipsForSurface(RaycastHit hit)
    {
        if (hit.collider != null)
        {
            FootstepSurface surface = hit.collider.GetComponentInParent<FootstepSurface>();

            if (surface != null)
            {
                foreach (SurfaceClipSet set in surfaceClipSets)
                {
                    if (set.surfaceType == surface.surfaceType && set.clips != null && set.clips.Length > 0)
                        return set.clips;
                }
            }
        }

        return defaultClips;
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;

        // Fuerza que este panel se dibuje por encima de otros elementos
        // IMGUI. No afecta al Canvas de UI (uGUI), que se renderiza por
        // su cuenta — por eso además logueamos a Console como respaldo.
        GUI.depth = -1000;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };

        string text =
            "== FOOTSTEP DEBUG ==\n" +
            $"Grounded (crudo): {debugRawGrounded}\n" +
            $"Grounded (con gracia): {debugGrounded}\n" +
            $"Piso detectado: {debugHitName}\n" +
            $"Vel. horizontal: {debugHorizontalSpeed:F2}\n" +
            $"Vel. vertical: {debugVerticalSpeed:F2}\n" +
            $"Estado: {debugLastReason}";

        GUI.Box(new Rect(10, 10, 460, 150), text, style);
    }
}
