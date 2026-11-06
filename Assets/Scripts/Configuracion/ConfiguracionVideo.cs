using UnityEngine;

/// <summary>
/// Guarda lo que el jugador elige en Opciones (Video y Audio) y lo aplica
/// cada vez que arranca el juego.
///
/// - El límite de FPS y la calidad usan las funciones de Felipe
///   (FPSSettings y GraphicsSettings).
/// - Todo se guarda en PlayerPrefs, igual que las teclas (Controles).
/// - No tiene nada de red: cada PC guarda su propia configuración.
/// </summary>
public static class ConfiguracionVideo
{
    // Opciones que muestra el menú. Las de FPS son las que usaba Felipe;
    // -1 es "Sin límite".
    public static readonly int[] OpcionesFPS = { 30, 60, 75, 144, 250, -1 };
    public static readonly string[] OpcionesCalidad = { "Bajo", "Medio", "Alto" };

    // Nombres con los que se guarda cada opción en PlayerPrefs.
    private const string ClavePantallaCompleta = "Video_PantallaCompleta";
    private const string ClaveAncho = "Video_Ancho";
    private const string ClaveAlto = "Video_Alto";
    private const string ClaveFPS = "Video_FPS";
    private const string ClaveCalidad = "Video_Calidad";
    private const string ClaveMostrarFPS = "Video_MostrarFPS";
    private const string ClaveVolumen = "Audio_Volumen";

    // ---- Valores actuales (los usa el menú para mostrarlos) ----

    public static bool PantallaCompleta => Screen.fullScreenMode != FullScreenMode.Windowed;

    public static int FPS => PlayerPrefs.GetInt(ClaveFPS, -1);

    public static string Calidad => QualitySettings.names[QualitySettings.GetQualityLevel()];

    public static float Volumen => AudioListener.volume;

    // Lo lee FPSCounter en cada frame, por eso se guarda en una variable.
    private static bool? mostrarFPS;
    public static bool MostrarFPS
    {
        get
        {
            if (mostrarFPS == null)
                mostrarFPS = PlayerPrefs.GetInt(ClaveMostrarFPS, 1) == 1;

            return mostrarFPS.Value;
        }
    }

    // ---- Cambios desde el menú (se aplican y se guardan al instante) ----

    public static void CambiarModoVentana(bool pantallaCompleta)
    {
        Screen.fullScreenMode = pantallaCompleta
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        PlayerPrefs.SetInt(ClavePantallaCompleta, pantallaCompleta ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void CambiarResolucion(int ancho, int alto)
    {
        Screen.SetResolution(ancho, alto, Screen.fullScreenMode);

        PlayerPrefs.SetInt(ClaveAncho, ancho);
        PlayerPrefs.SetInt(ClaveAlto, alto);
        PlayerPrefs.Save();
    }

    public static void CambiarFPS(int fps)
    {
        FPSSettings.CambiarFPS(fps);

        PlayerPrefs.SetInt(ClaveFPS, fps);
        PlayerPrefs.Save();
    }

    public static void CambiarCalidad(string nombre)
    {
        GraphicsSettings.CambiarCalidad(nombre);

        // Cambiar la calidad puede volver a prender el VSync, que hace que se
        // ignore el límite de FPS. Por eso se vuelve a aplicar el límite.
        if (PlayerPrefs.HasKey(ClaveFPS))
            FPSSettings.CambiarFPS(FPS);

        PlayerPrefs.SetString(ClaveCalidad, nombre);
        PlayerPrefs.Save();
    }

    public static void CambiarMostrarFPS(bool mostrar)
    {
        mostrarFPS = mostrar;

        PlayerPrefs.SetInt(ClaveMostrarFPS, mostrar ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void CambiarVolumen(float volumen)
    {
        AudioListener.volume = volumen;

        PlayerPrefs.SetFloat(ClaveVolumen, volumen);
        PlayerPrefs.Save();
    }

    // ---- Al iniciar el juego ----

    // Unity llama a esta función sola cuando arranca el juego (en cualquier
    // escena). Solo se aplica lo que el jugador ya eligió alguna vez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AplicarAlIniciar()
    {
        if (PlayerPrefs.HasKey(ClavePantallaCompleta))
        {
            Screen.fullScreenMode = PlayerPrefs.GetInt(ClavePantallaCompleta) == 1
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        }

        if (PlayerPrefs.HasKey(ClaveAncho) && PlayerPrefs.HasKey(ClaveAlto))
        {
            Screen.SetResolution(
                PlayerPrefs.GetInt(ClaveAncho),
                PlayerPrefs.GetInt(ClaveAlto),
                Screen.fullScreenMode);
        }

        // Primero la calidad y después los FPS (por lo del VSync).
        if (PlayerPrefs.HasKey(ClaveCalidad))
            GraphicsSettings.CambiarCalidad(PlayerPrefs.GetString(ClaveCalidad));

        if (PlayerPrefs.HasKey(ClaveFPS))
            FPSSettings.CambiarFPS(FPS);

        AudioListener.volume = PlayerPrefs.GetFloat(ClaveVolumen, 1f);
    }
}
