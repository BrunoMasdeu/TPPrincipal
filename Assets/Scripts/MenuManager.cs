using UnityEngine;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject playModesLayout;

    [Header("Network flow")]
    [SerializeField] private ConnectionManager connectionManager;
    [SerializeField] private GameModeDefinition raceDefinition;

    [Header("Texturas de Cursor")]
    [Tooltip("Cursor por defecto del juego")]
    [SerializeField] private Texture2D cursorDefault;

    [Tooltip("Cursor al pasar sobre un botón/interactuable")]
    [SerializeField] private Texture2D cursorHover;

    [Header("Puntos de Contacto (Hotspots)")]
    [SerializeField] private Vector2 hotSpotDefault = Vector2.zero;
    [SerializeField] private Vector2 hotSpotHover = Vector2.zero;

    [Header("Canvas")]
    [SerializeField] private GameObject canvasMain;
    [SerializeField] private GameObject canvasSettings;
    [SerializeField] private GameObject canvasRules;
    [SerializeField] private GameObject panelExit;
    [SerializeField] private GameObject canvasMultiplayer;
    private bool sobreBoton = false;
    private void Start()
    {
        // Establecer el cursor personalizado al iniciar la escena
        SetCursorDefault();
    }

    private void Update()
    {
        // Si no hay EventSystem activo, mantener default
        if (EventSystem.current == null) return;

        // Comprobar si el puntero está sobre algún elemento de la UI
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // Si quieres que cambie sobre CUALQUIER elemento UI o botón:
            if (!sobreBoton)
            {
                SetCursorHover();
                sobreBoton = true;
            }
        }
        else
        {
            if (sobreBoton)
            {
                SetCursorDefault();
                sobreBoton = false;
            }
        }
    }

    public void SetCursorDefault()
    {
        Cursor.SetCursor(cursorDefault, hotSpotDefault, CursorMode.Auto);
    }

    public void SetCursorHover()
    {
        Cursor.SetCursor(cursorHover, hotSpotHover, CursorMode.Auto);
    }

    private void OnDisable()
    {
        // Restaurar cursor del SO al cerrar o cambiar de escena si fuera necesario
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    public void Play()
    {
        playModesLayout.SetActive(!playModesLayout.activeSelf);
    }

    public void PlaySinglePlayer()
    {
        if (connectionManager == null || raceDefinition == null)
        {
            Debug.LogError("Falta configurar la carrera individual en el menú.");
            return;
        }

        if (!connectionManager.StartLocalGame(raceDefinition))
            return;

        playModesLayout.SetActive(false);
        canvasMain.SetActive(false);
    }

    public void Settings()
    {
        canvasMain.SetActive(false);
        canvasSettings.SetActive(true);
    }

    public void OpenRules()
    {
        canvasMain.SetActive(false);
        canvasRules.SetActive(true);
    }

    public void CloseRules()
    {
        canvasMain.SetActive(true);
        canvasRules.SetActive(false);
    }

    public void ReturnMenu()
    {
        canvasMain.SetActive(true);
        canvasSettings.SetActive(false);
    }

    public void SalirDelJuego()
    {
        panelExit.SetActive(true);
    }

    public void CancelarSalir()
    {
        panelExit.SetActive(false);
    }
    public void ConfirmarSalir()
    {
        // Cierra la aplicación (funciona en la build final)
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }

    public void OpenMultiplayer()
    {
        canvasMain.SetActive(false);
        canvasSettings.SetActive(false);
        canvasMultiplayer.SetActive(true);
    }

    public void CloseMultiplayer()
    {
        canvasMultiplayer.SetActive(false);
        canvasMain.SetActive(true);
    }

}
