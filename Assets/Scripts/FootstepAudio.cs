using System;
using UnityEngine;

/// <summary>
/// Reproduce sonidos de pasos basándose en el desplazamiento real del
/// transform (compatible con multiplayer: no depende de Move ni del
/// Animator, que solo corren en el dueño del jugador).
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
/// Además corta el clip anterior antes de reproducir el siguiente paso
/// (audioSource.Stop() en PlayFootstep), para que nunca se escuchen dos
/// pasos superpuestos aunque el clip de audio tenga cola/reverb más larga
/// que el intervalo de cadencia (sobre todo corriendo, con intervalos
/// cortos).
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

    [Header("Cadencia")]
    [SerializeField] private float stepIntervalWalk = 0.6f;
    [SerializeField] private float stepIntervalRun = 0.3f;
    [SerializeField] private float runSpeedThreshold = 8f;
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
    private Vector3 lastPosition;
    private float stepTimer;

    private float lowVerticalTimer;
    private bool airborne;
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
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        lastPosition = transform.position;
    }

    private void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - lastPosition;
        lastPosition = currentPosition;

        float dt = Time.deltaTime;
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

        HandleJumpAndLand(rawGrounded || assumedGrounded, effectiveGrounded, signedVerticalSpeed, dt);

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

        if (effectiveGrounded && movingLikeWalking)
        {
            stepTimer -= dt;

            if (stepTimer <= 0f)
            {
                PlayFootstep(hitForSurface);

                bool running = horizontalSpeed >= runSpeedThreshold;
                stepTimer = running ? stepIntervalRun : stepIntervalWalk;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    // Detecta el despegue de un salto y el aterrizaje, y reproduce su sonido.
    private void HandleJumpAndLand(bool groundedNow, bool effectiveGrounded, float signedVerticalSpeed, float dt)
    {
        if (!airborne)
        {
            // Despegue: venía en el piso y sale disparado hacia arriba más rápido
            // que al subir un escalón (mismo umbral que usa el sistema de pasos).
            if (wasEffectiveGrounded && signedVerticalSpeed > maxVerticalSpeedForStep)
            {
                airborne = true;
                airTime = 0f;
                maxFallSpeed = 0f;
                PlayOneShotFrom(jumpClips, jumpVolumeScale);
            }
            // Caída sin salto (te caés de un borde).
            else if (!groundedNow && signedVerticalSpeed < -fallTriggerSpeed)
            {
                airborne = true;
                airTime = 0f;
                maxFallSpeed = 0f;
            }
        }
        else
        {
            airTime += dt;

            if (signedVerticalSpeed < 0f)
                maxFallSpeed = Mathf.Max(maxFallSpeed, -signedVerticalSpeed);

            // Aterrizaje: toca el piso, ya no sube y estuvo un mínimo de tiempo en el aire.
            if (groundedNow && signedVerticalSpeed <= 0.5f)
            {
                airborne = false;

                if (airTime >= minAirTime)
                {
                    float volume = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(maxFallSpeed / 12f));
                    PlayOneShotFrom(landClips, volume);

                    // Evita que el primer paso corte el sonido de aterrizaje.
                    stepTimer = stepIntervalWalk;
                }
            }
        }

        wasEffectiveGrounded = effectiveGrounded;
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

        // Corta el paso anterior si todavía estaba sonando (clip con cola
        // más larga que el intervalo de cadencia, típico al correr). Sin
        // esto, dos pasos se superponen y se escucha "amontonado".
        audioSource.Stop();

        audioSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
        audioSource.volume = UnityEngine.Random.Range(volumeMin, volumeMax);
        audioSource.PlayOneShot(clip);
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