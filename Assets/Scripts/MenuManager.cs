using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private GameObject canvasAchieves;
    [SerializeField] private GameObject canvasLoading;
    [SerializeField] private GameObject canvasZombies;


    [Header("Logros")]
    [SerializeField] private Image logroTutorial;
    [SerializeField] private Image logroEasterEgg;
    [SerializeField] private Image logroWinTDM;
    [SerializeField] private Image logroWinCTF;
    [SerializeField] private Image logroZombies;

    private bool sobreBoton = false;
    private void Start()
    {
        // Establecer el cursor personalizado al iniciar la escena
        SetCursorDefault();
        ActualizarMenuLogros();
    }

    private void Update()
    {
        if (canvasZombies != null && canvasZombies.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            ShowConnectionMenu();
        }

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

        ShowCanvas(canvasLoading);

        StartCoroutine(StartTutorialWithDelay());
    }

    private System.Collections.IEnumerator StartTutorialWithDelay()
    {
        // Obliga a Unity a esperar al siguiente fotograma. 
        // Esto garantiza que el Canvas de carga ya esté visible en la pantalla del jugador.
        yield return null;

        // 3. Iniciar la lógica de red local del tutorial
        if (!connectionManager.StartLocalTutorial())
        {
            // Si falla la conexión, volvemos al menú
            ShowMainMenu();
        }
    }

    public void StartGame()
    {
        if (connectionManager == null)
        {
            Debug.LogError("Falta configurar ConnectionManager en el menú.");
            return;
        }

        ShowCanvas(canvasLoading);

        StartCoroutine(StartGameWithDelay());
    }

    private System.Collections.IEnumerator StartGameWithDelay()
    {
        yield return null;

        if (!connectionManager.StartGame())
        {
            // Si falla la conexión, volvemos al menú
            ShowMainMenu();
        }
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

    public void PlayZombies()
    {
        ShowCanvas(canvasZombies);
    }

    public void PlayZombiesEasy() => StartZombiesWithDifficulty(0);
    public void PlayZombiesNormal() => StartZombiesWithDifficulty(1);
    public void PlayZombiesHard() => StartZombiesWithDifficulty(2);

    private void StartZombiesWithDifficulty(int difficulty)
    {
        if (connectionManager == null)
        {
            Debug.LogError("Falta configurar ConnectionManager en el menú.");
            return;
        }

        WaveManager.QueueMenuDifficulty(difficulty);
        ShowCanvas(canvasLoading);
        StartCoroutine(StartZombiesWithDelay());
    }

    private System.Collections.IEnumerator StartZombiesWithDelay()
    {
        yield return null;

        if (!connectionManager.StartLocalZombies())
        {
            WaveManager.ClearQueuedMenuDifficulty();
            ShowCanvas(canvasZombies);
        }
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
    public void ShowAchieves() => ShowCanvas(canvasAchieves);
    public void HideMenu() => ShowCanvas(null);

    private void ShowCanvas(GameObject selected)
    {
        SetCanvasActive(canvasLoading, selected);
        SetCanvasActive(canvasMain, selected);
        SetCanvasActive(canvasSettings, selected);
        SetCanvasActive(canvasRules, selected);
        SetCanvasActive(canvasMultiplayer, selected);
        SetCanvasActive(canvasAchieves, selected);
        SetCanvasActive(canvasLobby, selected);
        SetCanvasActive(canvasZombies, selected);
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

    public void ActualizarMenuLogros()
    {
        Achievement tutorialAch = AchievementManager.Instance.GetAchievement("tutorial_done");
        Achievement zombiesAch = AchievementManager.Instance.GetAchievement("win_zombies");
        Achievement ctfAch = AchievementManager.Instance.GetAchievement("win_ctf");
        Achievement tdmAch = AchievementManager.Instance.GetAchievement("win_tdm");
        Achievement easterEggAch = AchievementManager.Instance.GetAchievement("easter_egg");

        if (tutorialAch != null && tutorialAch.isUnlocked) SetUnlocked(logroTutorial, true);
        if (zombiesAch != null && zombiesAch.isUnlocked) SetUnlocked(logroZombies, true);
        if (ctfAch != null && ctfAch.isUnlocked) SetUnlocked(logroWinCTF, true);
        if (tdmAch != null && tdmAch.isUnlocked) SetUnlocked(logroWinTDM, true);
        if (easterEggAch != null && easterEggAch.isUnlocked) SetUnlocked(logroEasterEgg, true);
    }

    private void SetUnlocked(Image image, bool unlocked)
    {
        if (image == null)
            return;

        Color color = unlocked ? Color.white : Color.black;
        image.color = color;
    }
}
