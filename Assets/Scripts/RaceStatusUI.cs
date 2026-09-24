using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RaceStatusUI : MonoBehaviour
{
    [Header("Estado de la carrera")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_Text statusText;

    [Header("Pantallas de resultado")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private GameObject defeatByOpponentMessage;
    [SerializeField] private GameObject defeatByTimeMessage;

    [Header("Navegación")]
    [SerializeField] private string menuSceneName = "MenuScene";

    private bool subscribed;
    private bool changingScene;

    private void OnEnable()
    {
        HideResultScreens();
        TrySubscribe();
        Refresh();
    }

    private void Update()
    {
        if (!subscribed)
            TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
            return;

        gameManager.estadoCarrera.OnValueChanged += OnRaceStateChanged;
        gameManager.tiempoActual.OnValueChanged += OnElapsedTimeChanged;
        gameManager.motivoFinalizacion.OnValueChanged += OnFinishReasonChanged;
        gameManager.idGanador.OnValueChanged += OnWinnerChanged;
        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed || gameManager == null)
            return;

        gameManager.estadoCarrera.OnValueChanged -= OnRaceStateChanged;
        gameManager.tiempoActual.OnValueChanged -= OnElapsedTimeChanged;
        gameManager.motivoFinalizacion.OnValueChanged -= OnFinishReasonChanged;
        gameManager.idGanador.OnValueChanged -= OnWinnerChanged;
        subscribed = false;
    }

    private void OnRaceStateChanged(
        EstadoCarrera previousState,
        EstadoCarrera currentState)
    {
        Refresh();
    }

    private void OnElapsedTimeChanged(float previousTime, float currentTime)
    {
        if (gameManager != null && gameManager.CarreraActiva)
            Refresh();
    }

    private void OnFinishReasonChanged(
        MotivoFinalizacionCarrera previousReason,
        MotivoFinalizacionCarrera currentReason)
    {
        Refresh();
    }

    private void OnWinnerChanged(ulong previousWinner, ulong currentWinner)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (gameManager == null)
        {
            ShowStatus("ESPERANDO JUGADORES...");
            return;
        }

        switch (gameManager.estadoCarrera.Value)
        {
            case EstadoCarrera.Activa:
                ShowStatus(
                    $"CARRERA ACTIVA\nTIEMPO: " +
                    $"{gameManager.TiempoRestante:0.0} s"
                );
                break;

            case EstadoCarrera.Finalizada:
                ShowFinalResult();
                break;

            default:
                ShowStatus("ESPERANDO JUGADORES...");
                break;
        }
    }

    private void ShowStatus(string message)
    {
        HideResultScreens();

        if (statusText == null)
            return;

        statusText.gameObject.SetActive(true);
        statusText.text = message;
    }

    private void ShowFinalResult()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            ShowStatus("CARRERA FINALIZADA");
            return;
        }

        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            gameManager.estadoCarrera.Value,
            gameManager.motivoFinalizacion.Value,
            gameManager.idGanador.Value,
            networkManager.LocalClientId
        );

        bool canShowResult = result switch
        {
            ResultadoCarreraLocal.Victoria => winScreen != null,
            ResultadoCarreraLocal.DerrotaPorLlegadaRival => loseScreen != null,
            ResultadoCarreraLocal.DerrotaPorTiempo => loseScreen != null,
            _ => false
        };

        if (!canShowResult)
        {
            ShowStatus("CARRERA FINALIZADA");
            return;
        }

        HideResultScreens();

        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (result == ResultadoCarreraLocal.Victoria)
        {
            winScreen.SetActive(true);
        }
        else
        {
            loseScreen.SetActive(true);

            if (defeatByOpponentMessage != null)
            {
                defeatByOpponentMessage.SetActive(
                    result == ResultadoCarreraLocal.DerrotaPorLlegadaRival
                );
            }

            if (defeatByTimeMessage != null)
            {
                defeatByTimeMessage.SetActive(
                    result == ResultadoCarreraLocal.DerrotaPorTiempo
                );
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideResultScreens()
    {
        if (winScreen != null)
            winScreen.SetActive(false);

        if (loseScreen != null)
            loseScreen.SetActive(false);

        if (defeatByOpponentMessage != null)
            defeatByOpponentMessage.SetActive(false);

        if (defeatByTimeMessage != null)
            defeatByTimeMessage.SetActive(false);
    }

    public void VolverAlMenu()
    {
        if (!changingScene)
            StartCoroutine(ReturnToMenuRoutine());
    }

    public void SalirDelJuego()
    {
        ShutdownNetworkManager();
        Application.Quit();
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        changingScene = true;
        ShutdownNetworkManager();

        // Esperar a que NetworkManager libere el Singleton antes de cargar
        // el NetworkManager configurado en MenuScene.
        yield return null;

        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    private void ShutdownNetworkManager()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        Destroy(networkManager.gameObject);
    }
}
