using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// [OPCIONES] Permite abrir el panel de Opciones (el mismo del menú principal)
/// desde la pausa, sin volver al menú.
///
/// - El panel es el prefab "Canv_Options" en Assets/Resources (se crea
///   arrastrando Canv_Options de MenuScene a esa carpeta). Así hay un solo panel:
///   lo que se cambie en el menú también se ve en la pausa.
/// - Cada menú de pausa llama a AgregarBoton(panel): copia el botón de
///   "Reanudar/Cancelar", lo llama OPCIONES y agranda el panel para que entre.
/// - VOLVER o Esc cierran Opciones y dejan la pausa abierta.
/// </summary>
public class OpcionesEnPausa : MonoBehaviour
{
    private const string NombrePrefab = "Canv_Options";
    private static OpcionesEnPausa abierto;
    private static int frameCerrado = -1;

    /// <summary>
    /// True si Opciones está abierto o se cerró en este frame con Esc.
    /// Los menús de pausa lo usan para no cerrarse con el mismo Esc.
    /// </summary>
    public static bool Ocupado => abierto != null || frameCerrado == Time.frameCount;

    public static void Abrir()
    {
        if (abierto != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(NombrePrefab);
        if (prefab == null)
        {
            Debug.LogError("[OPCIONES] Falta el prefab Assets/Resources/" + NombrePrefab + ".");
            return;
        }

        GameObject panel = Instantiate(prefab);
        panel.name = NombrePrefab + " (pausa)";
        abierto = panel.AddComponent<OpcionesEnPausa>();

        // Arriba de cualquier menú de pausa.
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
        }

        // VOLVER: en el menú vuelve al menú principal; acá solo cierra Opciones.
        Transform volver = panel.transform.Find("ReturnButton");
        if (volver != null && volver.TryGetComponent(out Button botonVolver))
        {
            botonVolver.onClick = new Button.ButtonClickedEvent();
            botonVolver.onClick.AddListener(Cerrar);
        }

        panel.SetActive(true);
    }

    public static void Cerrar()
    {
        if (abierto == null)
            return;

        Destroy(abierto.gameObject);
        abierto = null;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            frameCerrado = Time.frameCount;
            Cerrar();
        }
    }

    private void OnDestroy()
    {
        if (abierto == this)
            abierto = null;
    }
}
