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
    [SerializeField] private GameObject canvasLobby;
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
        ShowConnectionMenu();
    }

    public void PlaySinglePlayer()
    {
        if (connectionManager == null)
        {
            Debug.LogError("Falta configurar ConnectionManager en el menú.");
            return;
        }

        if (!connectionManager.StartLocalTutorial())
            return;

        HideMenu();
    }

    public void PlayParkour()
    {
        if (connectionManager == null || raceDefinition == null)
        {
            Debug.LogError("Falta configurar la carrera de parkour en el menú.");
            return;
        }

        if (!connectionManager.StartLocalGame(raceDefinition))
            return;

        HideMenu();
    }

    public void Settings()
    {
        ShowCanvas(canvasSettings);
    }

    public void OpenRules()
    {
        if (canvasRules != null)
            ShowCanvas(canvasRules);
    }

    public void CloseRules()
    {
        ShowMainMenu();
    }

    public void ReturnMenu()
    {
        ShowMainMenu();
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
        ShowConnectionMenu();
    }

    public void CloseMultiplayer()
    {
        connectionManager?.CancelConnection();
        ShowMainMenu();
    }

    public void ShowMainMenu() => ShowCanvas(canvasMain);
    public void ShowConnectionMenu() => ShowCanvas(canvasMultiplayer);
    public void ShowLobbyMenu() => ShowCanvas(canvasLobby);
    public void HideMenu() => ShowCanvas(null);

    private void ShowCanvas(GameObject selected)
    {
        SetCanvasActive(canvasMain, selected);
        SetCanvasActive(canvasSettings, selected);
        SetCanvasActive(canvasRules, selected);
        SetCanvasActive(canvasMultiplayer, selected);
        SetCanvasActive(canvasLobby, selected);
        if (playModesLayout != null && playModesLayout != canvasMultiplayer)
            playModesLayout.SetActive(false);
        if (panelExit != null)
            panelExit.SetActive(false);
    }

    private static void SetCanvasActive(GameObject canvas, GameObject selected)
    {
        if (canvas != null)
            canvas.SetActive(canvas == selected);
    }
}
