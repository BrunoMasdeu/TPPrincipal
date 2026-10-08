using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject playModesLayout;

    [Header("Multiplayer game modes")]
    [SerializeField] private ConnectionManager connectionManager;
    [SerializeField] private GameModeDefinition tdmDefinition;
    [SerializeField] private GameModeDefinition ctfDefinition;
    [SerializeField] private GameModeDefinition raceDefinition;
    [SerializeField] private GameModeId initialGameMode = GameModeId.TDM;
    [SerializeField, Min(2)] private int selectedPlayerCount = 2;

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
    private GameModeDefinition selectedGameModeDefinition;

    public GameModeDefinition SelectedGameModeDefinition =>
        selectedGameModeDefinition;

    public int SelectedPlayerCount => selectedPlayerCount;

    private void Start()
    {
        // Establecer el cursor personalizado al iniciar la escena
        SetCursorDefault();
        SelectGameMode(initialGameMode);
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

    private void OnGUI()
    {
        if (canvasMultiplayer == null || !canvasMultiplayer.activeSelf ||
            connectionManager == null ||
            connectionManager.IsConnecting ||
            (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
            return;

        // Selector temporal para que el flujo de arquitectura se pueda usar
        // sin editar la interfaz definitiva del equipo.
        GUILayout.BeginArea(new Rect(Screen.width - 250, 15, 235, 145), GUI.skin.box);
        string selectedModeLabel = selectedGameModeDefinition != null
            ? selectedGameModeDefinition.GameModeId.ToString()
            : "Sin elegir";
        GUILayout.Label($"MODO: {selectedModeLabel}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("TDM")) SelectTdm();
        if (GUILayout.Button("CTF")) SelectCtf();
        GUILayout.EndHorizontal();

        GUILayout.Label($"JUGADORES: {selectedPlayerCount}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("2")) SelectPlayerCount(2);
        if (GUILayout.Button("4")) SelectPlayerCount(4);
        if (GUILayout.Button("6")) SelectPlayerCount(6);
        if (GUILayout.Button("8")) SelectPlayerCount(8);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    public void SelectTdm()
    {
        SelectGameMode(GameModeId.TDM);
    }

    public void SelectCtf()
    {
        SelectGameMode(GameModeId.CTF);
    }

    public void SelectPlayerCount(int playerCount)
    {
        if (connectionManager != null && connectionManager.IsConnecting ||
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        if (selectedGameModeDefinition == null)
        {
            Debug.LogError(
                "No se puede elegir la cantidad sin un modo seleccionado."
            );
            return;
        }

        if (!selectedGameModeDefinition.AllowsPlayerCount(playerCount))
        {
            Debug.LogWarning(
                $"{selectedGameModeDefinition.GameModeId} no admite " +
                $"{playerCount} jugadores."
            );
            return;
        }

        selectedPlayerCount = playerCount;
        ApplyMultiplayerSelection();
    }

    public void PlaySinglePlayer()
    {
        if (connectionManager == null || raceDefinition == null)
        {
            Debug.LogError("Falta configurar la carrera individual en el menú.");
            return;
        }

        connectionManager.StartLocalGame(raceDefinition);
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
        ApplyMultiplayerSelection();
        canvasMain.SetActive(false);
        canvasSettings.SetActive(false);
        canvasMultiplayer.SetActive(true);
    }

    public void CloseMultiplayer()
    {
        canvasMultiplayer.SetActive(false);
        canvasMain.SetActive(true);
    }

    private void SelectGameMode(GameModeId gameModeId)
    {
        if (connectionManager != null && connectionManager.IsConnecting ||
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        GameModeDefinition definition = gameModeId switch
        {
            GameModeId.TDM => tdmDefinition,
            GameModeId.CTF => ctfDefinition,
            _ => null
        };

        if (definition == null)
        {
            Debug.LogError(
                $"No existe una definición configurada para {gameModeId}."
            );
            return;
        }

        if (!definition.AllowsPlayerCount(selectedPlayerCount))
        {
            if (definition.AllowedPlayerCounts.Count == 0)
            {
                Debug.LogError(
                    $"{gameModeId} no posee cantidades de jugadores configuradas."
                );
                return;
            }

            selectedPlayerCount = definition.AllowedPlayerCounts[0];
        }

        selectedGameModeDefinition = definition;
        ApplyMultiplayerSelection();
    }

    private bool ApplyMultiplayerSelection()
    {
        if (connectionManager == null)
        {
            Debug.LogError(
                "MenuManager no tiene ConnectionManager configurado."
            );
            return false;
        }

        if (selectedGameModeDefinition == null)
            return false;

        if (connectionManager.ConfigureSessionSelection(
                selectedGameModeDefinition,
                selectedPlayerCount,
                out string validationError))
        {
            return true;
        }

        Debug.LogWarning(
            $"No se pudo aplicar la selección multijugador: {validationError}"
        );
        return false;
    }
}
