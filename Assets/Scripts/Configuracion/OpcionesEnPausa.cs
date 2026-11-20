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
    private const string NombreBoton = "ButtonOpciones";

    private static OpcionesEnPausa abierto;
    private static int frameCerrado = -1;

    /// <summary>
    /// True si Opciones está abierto o se cerró en este frame con Esc.
    /// Los menús de pausa lo usan para no cerrarse con el mismo Esc.
    /// </summary>
    public static bool Ocupado => abierto != null || frameCerrado == Time.frameCount;

    /// <summary>Agrega el botón OPCIONES al panel de pausa (una sola vez).</summary>
    public static void AgregarBoton(GameObject panel)
    {
        if (panel == null || panel.transform.Find(NombreBoton) != null)
            return;

        Transform modelo = panel.transform.Find("ButtonPermanecer");
        Transform otro = panel.transform.Find("ButtonVolverMenu");
        if (modelo == null)
            return;

        RectTransform rectPanel = (RectTransform)panel.transform;
        RectTransform rectModelo = (RectTransform)modelo;

        // Espacio para una fila más de botones: el alto del botón + un margen.
        float alto = rectModelo.sizeDelta.y;
        float extra = alto * 1.6f;

        // Se agranda el panel hacia abajo (el borde de arriba queda donde estaba).
        rectPanel.sizeDelta += new Vector2(0f, extra);
        rectPanel.anchoredPosition -= new Vector2(0f, extra / 2f);
        foreach (RectTransform hijo in rectPanel)
        {
            if (Mathf.Approximately(hijo.anchorMin.y, 0.5f) && Mathf.Approximately(hijo.anchorMax.y, 0.5f))
                hijo.anchoredPosition += new Vector2(0f, extra / 2f);
        }

        // El botón nuevo va centrado, debajo del botón más bajo.
        float masBajo = rectModelo.anchoredPosition.y;
        if (otro != null)
            masBajo = Mathf.Min(masBajo, ((RectTransform)otro).anchoredPosition.y);

        GameObject boton = Instantiate(modelo.gameObject, rectPanel, false);
        boton.name = NombreBoton;
        RectTransform rectBoton = (RectTransform)boton.transform;
        rectBoton.anchoredPosition = new Vector2(0f, masBajo - extra);

        TMP_Text texto = boton.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
            texto.text = "OPCIONES";

        // La copia trae la acción de "Reanudar": se reemplaza por abrir Opciones.
        Button componente = boton.GetComponent<Button>();
        componente.onClick = new Button.ButtonClickedEvent();
        componente.onClick.AddListener(Abrir);
    }

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
