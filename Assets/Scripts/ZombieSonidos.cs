using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [ZOMBIES] Sonidos de un zombie. Va en cada prefab de zombie, con su propia voz.
///
/// Solo:      gruñidos cada tanto y pasos al ritmo en que camina o corre.
/// Zombie.cs: llama a Quejido() cuando le pega una bala, a Ataque() cuando
///            ataca, a GolpeAlJugador() cuando el golpe te bajó la vida y a
///            Morir() cuando muere.
///
/// La voz y los pasos salen del zombie (3D): se escuchan más fuerte cerca y
/// desde donde está. El golpe al jugador suena en la cámara (lo sentís vos).
/// </summary>
public class ZombieSonidos : MonoBehaviour
{
    [Header("Voz (se elige uno al azar)")]
    public AudioClip[] grunidos;
    public AudioClip[] quejidos;
    public AudioClip[] gritosAtaque;

    [Header("Pasos")]
    public AudioClip[] pasos;

    [Header("Golpe al jugador (suenan todos juntos, en la cámara)")]
    public AudioClip[] golpeAlJugador;

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumenVoz = 1f;
    [Range(0f, 1f)] public float volumenPasos = 0.6f;
    [Range(0f, 1f)] public float volumenGolpe = 1f;

    [Header("Tiempos")]
    public float grunidoCadaMin = 4f;
    public float grunidoCadaMax = 8f;
    public float pasoCaminando = 0.6f;
    public float pasoCorriendo = 0.35f;

    [Header("Distancia")]
    [Tooltip("Desde esta distancia se escucha al máximo.")]
    public float distanciaMinima = 2f;
    [Tooltip("Más lejos que esto no se escucha.")]
    public float distanciaMaxima = 25f;

    private AudioSource voz;
    private AudioSource pies;
    private NavMeshAgent agente;
    private float proximoGrunido;
    private float proximoPaso;
    private float ultimoQuejido = -10f;
    private bool muerto;

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        voz = CrearFuente();
        pies = CrearFuente();
    }

    private void Start()
    {
        // Arranca en un momento distinto en cada zombie, para que no gruñan todos juntos.
        proximoGrunido = Time.time + Random.Range(1f, grunidoCadaMax);
    }

    private void Update()
    {
        if (muerto)
            return;

        if (Time.time >= proximoGrunido)
        {
            proximoGrunido = Time.time + Random.Range(grunidoCadaMin, grunidoCadaMax);
            if (!voz.isPlaying)
                Reproducir(voz, grunidos, volumenVoz, 0.92f, 1.08f);
        }

        float velocidad = agente != null ? agente.velocity.magnitude : 0f;
        if (velocidad > 0.2f && Time.time >= proximoPaso)
        {
            proximoPaso = Time.time + (velocidad > 2.5f ? pasoCorriendo : pasoCaminando);
            Reproducir(pies, pasos, volumenPasos * Random.Range(0.8f, 1f), 0.9f, 1.1f);
        }
    }

    /// <summary>Le pegó una bala. Corta el gruñido para que se escuche el dolor.</summary>
    public void Quejido()
    {
        // Con armas rápidas no se pisa un quejido con otro.
        if (Time.time - ultimoQuejido < 0.35f)
            return;

        ultimoQuejido = Time.time;
        voz.Stop();
        Reproducir(voz, quejidos, volumenVoz, 0.95f, 1.05f);
    }

    /// <summary>Empieza un ataque (suena aunque el golpe falle).</summary>
    public void Ataque()
    {
        if (!voz.isPlaying)
            Reproducir(voz, gritosAtaque, volumenVoz, 0.95f, 1.05f);
    }

    /// <summary>El golpe le bajó la vida al jugador.</summary>
    public void GolpeAlJugador()
    {
        Camera camara = Camera.main;
        if (camara == null || golpeAlJugador == null)
            return;

        foreach (AudioClip clip in golpeAlJugador)
        {
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, camara.transform.position, volumenGolpe);
        }
    }

    /// <summary>Murió: deja de gruñir y de dar pasos (el último quejido sigue sonando).</summary>
    public void Morir()
    {
        muerto = true;
        pies.Stop();
    }

    private AudioSource CrearFuente()
    {
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // 3D
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = distanciaMinima;
        fuente.maxDistance = distanciaMaxima;
        fuente.dopplerLevel = 0f;
        return fuente;
    }

    // La voz usa Play() (así se puede cortar y saber si está sonando);
    // los pasos usan PlayOneShot() (se pueden superponer).
    private void Reproducir(AudioSource fuente, AudioClip[] clips, float volumen,
        float tonoMin, float tonoMax)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        fuente.pitch = Random.Range(tonoMin, tonoMax);
        if (fuente == voz)
        {
            fuente.clip = clip;
            fuente.volume = volumen;
            fuente.Play();
        }
        else
        {
            fuente.PlayOneShot(clip, volumen);
        }
    }
}
