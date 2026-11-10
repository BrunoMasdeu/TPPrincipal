using Unity.Netcode;
using UnityEngine;

/// <summary>
/// [ANIMACIONES] Efecto y sonido de la granada, en todas las PCs.
///
/// La explosión de Felipe (GrenadeExplosion) ocurre solo en el servidor y
/// termina con Despawn. Este componente no toca ese código: cada PC lo
/// detecta al desaparecer la granada de la red y muestra la explosión.
///
/// - Al aparecer la granada (el momento en que sale de la mano): sonido de
///   lanzamiento.
/// - Al desaparecer (explota): efecto de fuego y humo + sonido de explosión.
/// El sonido es 3D: se escucha más fuerte cuanto más cerca esté la granada.
/// </summary>
[DisallowMultipleComponent]
public sealed class GrenadeEffects : NetworkBehaviour
{
    [Header("Explosión")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip[] explosionSounds;
    [SerializeField] private Vector3 explosionOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField, Min(0.5f)] private float effectLifetime = 2f;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;
    [SerializeField, Min(0.1f)] private float explosionMinDistance = 8f;
    [SerializeField, Min(1f)] private float explosionMaxDistance = 80f;

    [Header("Lanzamiento")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField, Range(0f, 1f)] private float throwVolume = 0.6f;
    [SerializeField, Min(0.1f)] private float throwMinDistance = 2f;
    [SerializeField, Min(1f)] private float throwMaxDistance = 25f;

    // Para los tests del prefab.
    public GameObject ExplosionPrefab => explosionPrefab;
    public AudioClip[] ExplosionSounds => explosionSounds;
    public AudioClip ThrowSound => throwSound;

    public override void OnNetworkSpawn()
    {
        PlayAt(throwSound, transform.position, throwVolume, throwMinDistance, throwMaxDistance);
    }

    public override void OnNetworkDespawn()
    {
        // Al cerrar la partida o cambiar de escena las granadas también se
        // quitan de la red, pero ahí no tiene que explotar nada.
        if (NetworkManager == null || NetworkManager.ShutdownInProgress)
            return;
        if (!gameObject.scene.isLoaded)
            return;

        Vector3 position = transform.position + explosionOffset;

        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(explosionPrefab, position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }

        if (explosionSounds != null && explosionSounds.Length > 0)
        {
            AudioClip clip = explosionSounds[Random.Range(0, explosionSounds.Length)];
            PlayAt(clip, position, explosionVolume, explosionMinDistance, explosionMaxDistance);
        }
    }

    // La granada se quita al explotar, así que el sonido va en un objeto
    // aparte que se destruye cuando termina.
    private static void PlayAt(
        AudioClip clip,
        Vector3 position,
        float volume,
        float minDistance,
        float maxDistance)
    {
        if (clip == null)
            return;

        GameObject sound = new GameObject("GrenadeSound");
        sound.transform.position = position;

        AudioSource source = sound.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.Play();

        Destroy(sound, clip.length + 0.1f);
    }
}
