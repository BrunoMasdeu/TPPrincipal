using System.Collections;
using UnityEngine;

/// <summary>
/// [ZOMBIES] Música y ambiente del modo zombies. Va en el objeto "Regla", al lado del WaveManager.
///
///   Empieza una oleada  -> suena la música de esa ronda (una vez) y entra el
///                          ambiente en loop, de a poco (fade in).
///   Termina la oleada   -> la música y el ambiente se apagan de a poco (fade out).
///   Te matan            -> se apaga todo de a poco.
///
/// También guarda los sonidos de la caja de armas y se los pasa a cada caja nueva.
/// Todos los sonidos y volúmenes se cambian desde el Inspector.
/// </summary>
public class SonidosOleadas : MonoBehaviour
{
    [Header("Música de cada ronda (suena una vez al empezar la ronda)")]
    public AudioClip[] musicaRondas;
    [Range(0f, 1f)] public float volumenMusica = 0.8f;

    [Header("Ambiente (en loop mientras hay zombies)")]
    public AudioClip ambiente;
    [Range(0f, 1f)] public float volumenAmbiente = 0.5f;
    [Tooltip("Segundos que tarda en subir el ambiente al empezar la ronda.")]
    public float segundosEntrada = 2f;
    [Tooltip("Segundos que tarda en apagarse al terminar la ronda.")]
    public float segundosSalida = 4f;

    [Header("Caja de armas")]
    public AudioClip cajaAmbiente;
    public AudioClip cajaApertura;
    public AudioClip cajaRuleta;
    public AudioClip cajaPremio;

    private AudioSource fuenteMusica;
    private AudioSource fuenteAmbiente;
    private Coroutine fadeMusica;
    private Coroutine fadeAmbiente;

    private void Awake()
    {
        fuenteMusica = CrearFuente(false);
        fuenteAmbiente = CrearFuente(true);
    }

    public void EmpiezaOleada(int numero)
    {
        // Música de la ronda (si hay menos músicas que rondas, se repite la última).
        if (musicaRondas != null && musicaRondas.Length > 0)
        {
            AudioClip musica = musicaRondas[Mathf.Clamp(numero - 1, 0, musicaRondas.Length - 1)];
            if (musica != null)
            {
                Detener(ref fadeMusica);
                fuenteMusica.clip = musica;
                fuenteMusica.volume = volumenMusica;
                fuenteMusica.Play();
            }
        }

        // Ambiente en loop, entrando de a poco.
        if (ambiente != null)
        {
            Detener(ref fadeAmbiente);
            fuenteAmbiente.clip = ambiente;
            if (!fuenteAmbiente.isPlaying)
            {
                fuenteAmbiente.volume = 0f;
                fuenteAmbiente.Play();
            }
            fadeAmbiente = StartCoroutine(Fundir(fuenteAmbiente, volumenAmbiente, segundosEntrada, false));
        }
    }

    /// <summary>Murió el último zombie de la oleada: todo se apaga de a poco.</summary>
    public void TerminaOleada()
    {
        ApagarTodo();
    }

    /// <summary>El jugador murió: todo se apaga de a poco.</summary>
    public void FinPartida()
    {
        ApagarTodo();
    }

    /// <summary>Le pasa a una caja nueva sus sonidos.</summary>
    public void ConfigurarCaja(CajaArmas caja)
    {
        caja.sonidoAmbiente = cajaAmbiente;
        caja.sonidoApertura = cajaApertura;
        caja.sonidoRuleta = cajaRuleta;
        caja.sonidoPremio = cajaPremio;
    }

    private void ApagarTodo()
    {
        if (fuenteMusica.isPlaying)
        {
            Detener(ref fadeMusica);
            fadeMusica = StartCoroutine(Fundir(fuenteMusica, 0f, segundosSalida, true));
        }

        if (fuenteAmbiente.isPlaying)
        {
            Detener(ref fadeAmbiente);
            fadeAmbiente = StartCoroutine(Fundir(fuenteAmbiente, 0f, segundosSalida, true));
        }
    }

    // Lleva el volumen de a poco hasta "destino". Si "pararAlFinal", al llegar a 0 corta el sonido.
    private static IEnumerator Fundir(AudioSource fuente, float destino, float segundos, bool pararAlFinal)
    {
        float inicio = fuente.volume;
        float tiempo = 0f;
        while (tiempo < segundos)
        {
            // Sin escala de tiempo: funciona aunque el juego esté pausado.
            tiempo += Time.unscaledDeltaTime;
            fuente.volume = Mathf.Lerp(inicio, destino, tiempo / segundos);
            yield return null;
        }

        fuente.volume = destino;
        if (pararAlFinal)
            fuente.Stop();
    }

    private void Detener(ref Coroutine rutina)
    {
        if (rutina != null)
            StopCoroutine(rutina);
        rutina = null;
    }

    private AudioSource CrearFuente(bool enLoop)
    {
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = enLoop;
        fuente.spatialBlend = 0f; // 2D: se escucha igual en todo el mapa
        return fuente;
    }
}
