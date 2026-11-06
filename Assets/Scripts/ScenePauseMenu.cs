using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menú de pausa compartido por las escenas de parkour y zombies.</summary>
public sealed class ScenePauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanelPrefab;

    private GameObject pausePanel;
    private PlayerNetworkSetup pausedPlayer;
    private bool returningToMenu;
    private bool changedTimeScale;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (pausePanelPrefab == null)
        {
            Debug.LogError("Falta asignar PausaPanel 1 al menú de pausa.", this);
            enabled = false;
            return;
        }

        GameObject canvasObject = new GameObject("PauseCanvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        pausePanel = Instantiate(pausePanelPrefab, canvasObject.transform, false);
        pausePanel.SetActive(false);

        foreach (Button button in pausePanel.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "ButtonVolverMenu")
                button.onClick.AddListener(ReturnToMainMenu);
            else if (button.name == "ButtonPermanecer")
                button.onClick.AddListener(TogglePause);
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }

    private void Update()
    {
        if (!returningToMenu && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();

        // El PlayerObject puede aparecer después de que se abrió el panel.
        if (pausePanel != null && pausePanel.activeSelf && pausedPlayer == null)
            LockLocalPlayer();
    }

    private void LockLocalPlayer()
    {
        foreach (PlayerNetworkSetup candidate in FindObjectsByType<PlayerNetworkSetup>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate.IsSpawned || !candidate.IsOwner)
                continue;

            pausedPlayer = candidate;
            pausedPlayer.SetPauseControlsEnabled(false);
            break;
        }
    }

    public void TogglePause()
    {
        if (returningToMenu || pausePanel == null)
            return;

        if (pausePanel.activeSelf)
        {
            ClosePause();
            return;
        }

        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousTimeScale = Time.timeScale;

        // En una sesión de red, detener el tiempo local también detendría al host.
        changedTimeScale = NetworkManager.Singleton == null ||
                           !NetworkManager.Singleton.IsListening;
        if (changedTimeScale)
            Time.timeScale = 0f;

        LockLocalPlayer();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        pausePanel.SetActive(true);
    }

    private void ClosePause()
    {
        pausePanel.SetActive(false);
        if (changedTimeScale)
            Time.timeScale = previousTimeScale;
        changedTimeScale = false;
        if (pausedPlayer != null)
            pausedPlayer.SetPauseControlsEnabled(true);
        pausedPlayer = null;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    public void ReturnToMainMenu()
    {
        if (!returningToMenu)
            StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        returningToMenu = true;
        if (pausePanel.activeSelf)
            ClosePause();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            if (networkManager.IsListening)
                networkManager.Shutdown();
            Destroy(networkManager.gameObject);
            yield return null;
        }

        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (changedTimeScale)
            Time.timeScale = previousTimeScale;
        if (pausedPlayer != null)
            pausedPlayer.SetPauseControlsEnabled(true);
    }
}
