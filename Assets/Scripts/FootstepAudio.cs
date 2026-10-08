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
    [SerializeField] private float stepIntervalWalk = 0.5f;
    [SerializeField] private float stepIntervalRun = 0.3f;
    [SerializeField] private float runSpeedThreshold = 5.5f;
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

    [Header("Debug")]
    [Tooltip("Muestra un panel en la esquina de la pantalla (en Play) con el estado actual del sistema de pasos. Desactivalo antes de la entrega final.")]
    [SerializeField] private bool debugOverlay = true;

    private AudioSource audioSource;
    private Vector3 lastPosition;
    private float stepTimer;

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
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - lastPosition;
        lastPosition = currentPosition;

        // Usamos Lerp/Filtro para que la velocidad no parpadee a 0 entre frames
        float rawHorizontalSpeed = new Vector2(delta.x, delta.z).magnitude / dt;
        float horizontalSpeed = Mathf.Lerp(debugHorizontalSpeed, rawHorizontalSpeed, dt * 10f);
        float verticalSpeed = Mathf.Abs(delta.y) / dt;

        bool rawGrounded = TryGetGroundHit(out RaycastHit hit);

        if (rawGrounded)
        {
            lastGroundedRealTime = Time.time;
            lastGroundHit = hit;
            hasLastGroundHit = true;
        }

        bool withinGrace = hasLastGroundHit && (Time.time - lastGroundedRealTime) <= groundedGraceTime;
        bool effectiveGrounded = rawGrounded || withinGrace;
        RaycastHit hitForSurface = rawGrounded ? hit : lastGroundHit;

        // Variables de Debug
        debugRawGrounded = rawGrounded;
        debugGrounded = effectiveGrounded;
        debugHorizontalSpeed = horizontalSpeed;
        debugVerticalSpeed = verticalSpeed;
        debugHitName = (effectiveGrounded && hitForSurface.collider != null) ? hitForSurface.collider.name : "(nada)";

        bool movingLikeWalking = horizontalSpeed >= minSpeedToStep
                                  && verticalSpeed <= maxVerticalSpeedForStep;

        if (!effectiveGrounded)
            debugLastReason = "NO SUENA: no hay piso detectado debajo";
        else if (horizontalSpeed < minSpeedToStep)
            debugLastReason = "NO SUENA: velocidad horizontal muy baja";
        else if (verticalSpeed > maxVerticalSpeedForStep)
            debugLastReason = "NO SUENA: velocidad vertical alta";
        else
            debugLastReason = "OK: cumple condiciones para sonar";

        // --- LÓGICA CORREGIDA DEL TIMER ---
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
            // EN LUGAR DE CERO: Mantenemos el timer topeado en el intervalo.
            // Así, si se frena un microsegundo, NO resetea a 0f de golpe.
            // Y si arranca desde parado, esperará a lo sumo 0.1s o el intervalo sin ráfaga.
            stepTimer = Mathf.Min(stepTimer, 0.1f);
        }
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