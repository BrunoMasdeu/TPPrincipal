using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta las secciones VIDEO y AUDIO del panel de Opciones (MenuScene)
/// con ConfiguracionVideo.
///
/// - Las opciones de cada desplegable las llena este script (no hace falta
///   escribirlas en el Inspector).
/// - Cada vez que se abre Opciones, muestra lo que está aplicado.
/// - Los cambios se aplican y se guardan al instante.
/// </summary>
public class PanelVideo : MonoBehaviour
{
    [Header("VIDEO")]
    [SerializeField] private TMP_Dropdown modoVentana;
    [SerializeField] private TMP_Dropdown resolucion;
    [SerializeField] private TMP_Dropdown limiteFPS;
    [SerializeField] private TMP_Dropdown calidad;
    [SerializeField] private TMP_Dropdown mostrarFPS;

    [Header("AUDIO")]
    [SerializeField] private Slider volumen;

    // Las resoluciones que se muestran, en el mismo orden que el desplegable.
    private readonly List<Vector2Int> resoluciones = new List<Vector2Int>();

    private void Awake()
    {
        if (modoVentana != null)
            modoVentana.onValueChanged.AddListener(indice =>
                ConfiguracionVideo.CambiarModoVentana(indice == 1));

        if (resolucion != null)
            resolucion.onValueChanged.AddListener(indice =>
                ConfiguracionVideo.CambiarResolucion(
                    resoluciones[indice].x, resoluciones[indice].y));

        if (limiteFPS != null)
            limiteFPS.onValueChanged.AddListener(indice =>
                ConfiguracionVideo.CambiarFPS(ConfiguracionVideo.OpcionesFPS[indice]));

        if (calidad != null)
            calidad.onValueChanged.AddListener(indice =>
                ConfiguracionVideo.CambiarCalidad(ConfiguracionVideo.OpcionesCalidad[indice]));

        if (mostrarFPS != null)
            mostrarFPS.onValueChanged.AddListener(indice =>
                ConfiguracionVideo.CambiarMostrarFPS(indice == 0));

        if (volumen != null)
            volumen.onValueChanged.AddListener(ConfiguracionVideo.CambiarVolumen);
    }

    private void OnEnable()
    {
        MostrarValoresActuales();
    }

    private void MostrarValoresActuales()
    {
        // Modo ventana: 0 = Ventana, 1 = Pantalla completa
        Llenar(modoVentana,
            new List<string> { "Ventana", "Pantalla completa" },
            ConfiguracionVideo.PantallaCompleta ? 1 : 0);

        // Resolución: las que soporta el monitor de esta PC
        Llenar(resolucion, ArmarResoluciones(), IndiceResolucionActual());

        // Límite de FPS: 30, 60, 75, 144, 250 y Sin límite
        List<string> textosFPS = new List<string>();
        foreach (int fps in ConfiguracionVideo.OpcionesFPS)
            textosFPS.Add(fps == -1 ? "Sin límite" : fps.ToString());

        int indiceFPS = System.Array.IndexOf(ConfiguracionVideo.OpcionesFPS, ConfiguracionVideo.FPS);
        Llenar(limiteFPS, textosFPS, indiceFPS);

        // Calidad: Bajo, Medio, Alto (si no coincide, se muestra Medio)
        int indiceCalidad = System.Array.IndexOf(ConfiguracionVideo.OpcionesCalidad, ConfiguracionVideo.Calidad);
        Llenar(calidad,
            new List<string>(ConfiguracionVideo.OpcionesCalidad),
            indiceCalidad >= 0 ? indiceCalidad : 1);

        // Mostrar FPS: 0 = Sí, 1 = No
        Llenar(mostrarFPS,
            new List<string> { "Sí", "No" },
            ConfiguracionVideo.MostrarFPS ? 0 : 1);

        if (volumen != null)
            volumen.SetValueWithoutNotify(ConfiguracionVideo.Volumen);
    }

    private List<string> ArmarResoluciones()
    {
        resoluciones.Clear();

        // Screen.resolutions repite cada tamaño por frecuencia (60 Hz, 144 Hz...):
        // se deja cada ancho x alto una sola vez.
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int tamano = new Vector2Int(r.width, r.height);
            if (!resoluciones.Contains(tamano))
                resoluciones.Add(tamano);
        }

        // Si la actual no está en la lista (pasa en el editor), se agrega.
        Vector2Int actual = new Vector2Int(Screen.width, Screen.height);
        if (!resoluciones.Contains(actual))
            resoluciones.Add(actual);

        List<string> textos = new List<string>();
        foreach (Vector2Int tamano in resoluciones)
            textos.Add(tamano.x + " x " + tamano.y);

        return textos;
    }

    private int IndiceResolucionActual()
    {
        return resoluciones.IndexOf(new Vector2Int(Screen.width, Screen.height));
    }

    // Reemplaza las opciones del desplegable y marca la elegida sin
    // disparar el cambio (para no volver a aplicar lo mismo).
    private static void Llenar(TMP_Dropdown desplegable, List<string> opciones, int elegida)
    {
        if (desplegable == null)
            return;

        desplegable.ClearOptions();
        desplegable.AddOptions(opciones);
        desplegable.SetValueWithoutNotify(Mathf.Max(0, elegida));
        desplegable.RefreshShownValue();
    }
}
