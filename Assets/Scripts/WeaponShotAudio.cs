using UnityEngine;

/// <summary>
/// Sonidos de un arma: disparo (con versión en primera persona, tercera
/// persona y lejana) y "click" de cargador vacío. Se agrega al prefab de
/// cada arma y se le pide el sonido desde el código del arma.
///
/// Los packs de armas suelen traer tres versiones del mismo disparo:
///  - 1p  (primera persona): lo que escucha QUIEN dispara.
///  - 3p  (tercera persona): lo que escucha otro jugador que está cerca.
///  - far (lejano): lo que escucha otro jugador que está lejos.
/// Este componente elige solo cuál reproducir.
///
/// Cómo usarlo:
/// 1. Poné este archivo en Assets/Scripts (reemplaza la versión anterior).
/// 2. En el prefab del arma: Add Component > Weapon Shot Audio.
/// 3. Arrastrá los clips a "Shot 1P", "Shot 3P" y "Shot Far". Con varios
///    clips por grupo se elige uno al azar y no suena repetitivo.
/// 4. Desde el script que confirma el disparo, en CADA cliente, llamá a
///    PlayShot(esElJugadorLocal). Ejemplo:
///        weaponShotAudio.PlayShot(IsOwner);
///    donde IsOwner es true solo en la máquina de quien disparó.
///
/// Nota de multijugador: este componente solo reproduce el sonido en la
/// máquina donde se lo llama. Para que lo escuchen todos, hay que llamarlo
/// desde el mismo evento de red que ya sincroniza el disparo o el fogonazo
/// (por ejemplo, un ClientRpc que se ejecuta en todos los clientes cuando
/// el servidor acepta el disparo).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WeaponShotAudio : MonoBehaviour
{
    [Header("Disparo")]
    [Tooltip("Lo que escucha quien dispara.")]
    [SerializeField] private AudioClip[] shot1P;
    [Tooltip("Lo que escucha otro jugador que está cerca.")]
    [SerializeField] private AudioClip[] shot3P;
    [Tooltip("Lo que escucha otro jugador que está lejos.")]
    [SerializeField] private AudioClip[] shotFar;

    [Header("Cargador vacío (opcional)")]
    [SerializeField] private AudioClip[] emptyClips;

    [System.Serializable]
    public class ReloadStep
    {
        public AudioClip clip;
        [Tooltip("Segundos desde que empieza la recarga hasta que suena este clip.")]
        public float delay;
    }

    [Header("Recarga (opcional)")]
    [Tooltip("Sonidos de la recarga en orden. Cada uno suena a los 'delay' segundos de empezar.")]
    [SerializeField] private ReloadStep[] reloadSteps;

    [Header("Cuándo es 'lejos'")]
    [Tooltip("Si el disparo es de otro jugador y está a más metros que esto, se usa el clip 'Far'.")]
    [SerializeField] private float farDistance = 30f;

    [Header("Variación")]
    [SerializeField] private float volumeMin = 0.9f;
    [SerializeField] private float volumeMax = 1f;
    [SerializeField] private float pitchMin = 0.96f;
    [SerializeField] private float pitchMax = 1.04f;

    [Header("Distancia (3D)")]
    [Tooltip("Hasta esta distancia (metros) se escucha a volumen completo.")]
    [SerializeField] private float minDistance = 3f;
    [Tooltip("Más allá de esta distancia (metros) ya no se escucha.")]
    [SerializeField] private float maxDistance = 100f;

    [Header("Debug")]
    [Tooltip("Escribe en la Console el estado del audio cada vez que suena un disparo. Desactivalo antes de la entrega final.")]
    [SerializeField] private bool debugLog = true;

    private AudioSource audioSource;
    private AudioListener listener;
    private Coroutine reloadRoutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    /// <summary>
    /// Reproduce el sonido de un disparo. Pasá true si quien disparó es el
    /// jugador local (usa el clip de primera persona); false si es otro
    /// jugador (usa el de tercera persona o el lejano según la distancia).
    /// </summary>
    public void PlayShot(bool isLocalShooter)
    {
        AudioClip[] clips;

        if (isLocalShooter)
        {
            clips = shot1P;
        }
        else
        {
            bool isFar = DistanceToListener() > farDistance;
            clips = isFar ? shotFar : shot3P;

            // Si falta el grupo que corresponde, usa el otro antes que quedar mudo.
            if (clips == null || clips.Length == 0)
                clips = isFar ? shot3P : shotFar;
        }

        // Último recurso: si el grupo elegido está vacío, usa cualquiera cargado.
        if (clips == null || clips.Length == 0)
            clips = FirstNonEmpty(shot1P, shot3P, shotFar);

        Play(clips);
    }

    /// <summary>Reproduce el 'click' de cargador vacío.</summary>
    public void PlayEmpty() => Play(emptyClips);

    /// <summary>Reproduce la secuencia de sonidos de la recarga.</summary>
    public void PlayReload()
    {
        StopReload();
        if (reloadSteps == null || reloadSteps.Length == 0) return;
        reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    /// <summary>Corta la secuencia de recarga (por si se cancela o se cambia de arma).</summary>
    public void StopReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        float elapsed = 0f;
        foreach (ReloadStep step in reloadSteps)
        {
            if (step == null || step.clip == null) continue;

            float wait = step.delay - elapsed;
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                elapsed = step.delay;
            }

            Play(new[] { step.clip });
        }
        reloadRoutine = null;
    }

    private void OnDisable() => StopReload();

    // --- Pruebas rápidas desde el Editor -------------------------------
    // Clic derecho sobre el título del componente en el Inspector (o en los
    // tres puntos de arriba a la derecha) y elegí una de estas opciones.
    // Sirven para confirmar que los clips y el AudioSource funcionan, sin
    // depender todavía del código del arma.
    [ContextMenu("Probar: disparo del jugador local (1P)")]
    private void TestLocalShot() => PlayShot(true);

    [ContextMenu("Probar: disparo de otro jugador (3P o lejano)")]
    private void TestRemoteShot() => PlayShot(false);

    [ContextMenu("Probar: recarga")]
    private void TestReload() => PlayReload();

    private float DistanceToListener()
    {
        if (listener == null)
            listener = FindFirstObjectByType<AudioListener>();

        return listener != null
            ? Vector3.Distance(listener.transform.position, transform.position)
            : 0f;
    }

    private static AudioClip[] FirstNonEmpty(params AudioClip[][] groups)
    {
        foreach (AudioClip[] g in groups)
            if (g != null && g.Length > 0) return g;
        return null;
    }

    private void Play(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Un AudioSource en un objeto desactivado no puede sonar.
        if (!audioSource.isActiveAndEnabled)
        {
            Debug.LogWarning($"[WeaponShotAudio] '{name}' está desactivado en la jerarquía (o el AudioSource está apagado): no puede sonar.", this);
            return;
        }

        if (debugLog)
        {
            bool hasListener = FindFirstObjectByType<AudioListener>() != null;
            Debug.Log($"[WeaponShotAudio] Suena '{clip.name}' en '{name}' | AudioListener en escena: {hasListener} | " +
                      $"AudioListener.volume: {AudioListener.volume} | AudioListener.pause: {AudioListener.pause} | " +
                      $"AudioSource mute: {audioSource.mute} | volumen del source: {audioSource.volume}", this);
        }

        // Sin Stop(): un disparo rápido puede solaparse con la cola del
        // anterior, que en armas suena natural (no como los pasos).
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, Random.Range(volumeMin, volumeMax));
    }
}