using UnityEngine;
using UnityEngine.EventSystems;

public class BotonCursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Textura del Cursor")]
    [Tooltip("Arrastra aquí la textura del cursor alternativo (Texture Type: Cursor)")]
    [SerializeField] private Texture2D cursorMano;

    [Header("Textura del Cursor Default")]
    [Tooltip("Arrastra aquí la textura de la cursor default (Texture Type: Cursor)")]
    [SerializeField] private Texture2D cursorDefault;

    [Tooltip("Punto de clic del cursor (Offset). Para una mano con índice suele ser la esquina superior izquierda (0,0)")]
    [SerializeField] private Vector2 hotSpot = Vector2.zero;

    // Se ejecuta cuando el puntero entra sobre el botón
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cursorMano != null)
        {
            Cursor.SetCursor(cursorMano, hotSpot, CursorMode.Auto);
        }
    }

    // Se ejecuta cuando el puntero sale del botón
    public void OnPointerExit(PointerEventData eventData)
    {
        // Restaurar el cursor predeterminado
        Cursor.SetCursor(cursorDefault, Vector2.zero, CursorMode.Auto);
    }

    // Asegurar que si el botón se deshabilita mientras el cursor estaba encima, el cursor vuelva al default
    private void OnDisable()
    {
        Cursor.SetCursor(cursorDefault, Vector2.zero, CursorMode.Auto);
    }
}