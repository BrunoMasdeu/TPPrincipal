using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConnectionManager : MonoBehaviour
{
    [Header("Connection UI")]
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Lobby UI")]
    [SerializeField] private TMP_Text codeDisplay;
    [SerializeField] private TMP_Text statusDisplay;
    [SerializeField] private TMP_Text playerCountDisplay;
    [SerializeField] private Button startGameButton;

    [Header("Game")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField, Min(2)] private int requiredPlayerCount = 2;

    private Task initializationTask;
    private string currentJoinCode;

    private void Awake()
    {
        Debug.Log("[Relay] ConnectionManager iniciado.");
        initializationTask = InitializeUnityServices();
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Relay] No existe NetworkManager.Singleton.");
            SetStatus("NETWORK MANAGER NOT FOUND");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        ShowConnectionPanel();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            Debug.Log("[Relay] Inicializando Unity Services...");

            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log(
                $"[Relay] Autenticación completada. PlayerId: " +
                $"{AuthenticationService.Instance.PlayerId}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay] Error inicializando servicios: {exception.Message}");
            Debug.LogException(exception);
            SetStatus("SERVICES ERROR");
        }
    }

    public async void StartHostWithRelay()
    {
        Debug.Log("[Relay][HOST] Botón CREATE GAME pulsado.");
        SetStatus("CREATING ROOM...");

        try
        {
            await initializationTask;

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                throw new InvalidOperationException("NetworkManager no encontrado.");

            LobbyPlayerSpawner playerSpawner =
                networkManager.GetComponent<LobbyPlayerSpawner>();

            if (playerSpawner == null)
            {
                throw new InvalidOperationException(
                    "LobbyPlayerSpawner no encontrado."
                );
            }

            playerSpawner.ConfigureRequiredPlayerCount(requiredPlayerCount);

            UnityTransport transport =
                networkManager.GetComponent<UnityTransport>();

            if (transport == null)
                throw new InvalidOperationException("Unity Transport no encontrado.");

            Debug.Log(
                $"[Relay][HOST] Creando allocation para " +
                $"{requiredPlayerCount - 1} cliente(s) adicional(es)."
            );

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    requiredPlayerCount - 1
                );

            currentJoinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[Relay][HOST] JOIN CODE GENERADO: {currentJoinCode}");

            // También lo copia automáticamente al portapapeles del host.
            GUIUtility.systemCopyBuffer = currentJoinCode;
            Debug.Log("[Relay][HOST] Código copiado al portapapeles.");

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls")
            );

            Debug.Log("[Relay][HOST] Unity Transport configurado.");

            bool started = networkManager.StartHost();

            if (!started)
            {
                Debug.LogError("[Relay][HOST] NetworkManager.StartHost devolvió false.");
                SetStatus("HOST COULD NOT START");
                return;
            }

            Debug.Log(
                $"[Relay][HOST] Host iniciado. LocalClientId: " +
                $"{networkManager.LocalClientId}"
            );

            ShowLobby(true);
            UpdatePlayerCount();

            if (codeDisplay != null)
                codeDisplay.text = $"JOIN CODE: {currentJoinCode}";

            SetStatus("WAITING FOR PLAYERS...");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay][HOST] Error creando la sala: {exception.Message}");
            Debug.LogException(exception);
            SetStatus("ERROR CREATING ROOM");
        }
    }

    public async void StartClientWithRelay()
    {
        Debug.Log("[Relay][CLIENT] Botón JOIN GAME pulsado.");

        string joinCode = joinCodeInput != null
            ? joinCodeInput.text.Trim().ToUpperInvariant()
            : string.Empty;

        Debug.Log($"[Relay][CLIENT] Código introducido: '{joinCode}'");

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Relay][CLIENT] No se introdujo ningún código.");
            SetStatus("ENTER A JOIN CODE");
            return;
        }

        SetStatus("JOINING ROOM...");

        try
        {
            await initializationTask;

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                throw new InvalidOperationException("NetworkManager no encontrado.");

            UnityTransport transport =
                networkManager.GetComponent<UnityTransport>();

            if (transport == null)
                throw new InvalidOperationException("Unity Transport no encontrado.");

            Debug.Log($"[Relay][CLIENT] Buscando sala con código {joinCode}...");

            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            Debug.Log(
                $"[Relay][CLIENT] Sala encontrada. AllocationId: " +
                $"{joinAllocation.AllocationId}"
            );

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(joinAllocation, "dtls")
            );

            Debug.Log("[Relay][CLIENT] Unity Transport configurado.");

            ShowLobby(false);
            SetStatus("CONNECTING...");

            bool started = networkManager.StartClient();

            if (!started)
            {
                Debug.LogError("[Relay][CLIENT] NetworkManager.StartClient devolvió false.");
                ShowConnectionPanel();
                SetStatus("CLIENT COULD NOT START");
                return;
            }

            Debug.Log(
                "[Relay][CLIENT] Conexión iniciada. Esperando confirmación del servidor..."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay][CLIENT] Error entrando a la sala: {exception.Message}");
            Debug.LogException(exception);

            ShowConnectionPanel();
            SetStatus("COULD NOT JOIN ROOM");
        }
    }

    public void StartGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsHost)
        {
            Debug.LogWarning(
                "[Relay] StartGame ignorado porque esta instancia no es el host."
            );
            return;
        }

        int playerCount = networkManager.ConnectedClientsIds.Count;

        if (playerCount != requiredPlayerCount)
        {
            Debug.LogWarning(
                $"[Relay] StartGame requiere {requiredPlayerCount} jugadores; " +
                $"actualmente hay {playerCount}."
            );
            SetStatus("WAITING FOR PLAYERS...");
            UpdatePlayerCount();
            return;
        }

        Debug.Log(
            $"[Relay][HOST] Iniciando {gameSceneName} con " +
            $"{playerCount} jugador(es)."
        );

        networkManager.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
    }

    public void CancelConnection()
    {
        Debug.Log("[Relay] Cancelando conexión y cerrando NetworkManager.");

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        currentJoinCode = string.Empty;
        ShowConnectionPanel();
    }

    private void OnClientConnected(ulong clientId)
    {
        int playerCount =
            NetworkManager.Singleton.ConnectedClientsIds.Count;

        Debug.Log(
            $"[Netcode] CLIENTE CONECTADO. ClientId: {clientId}. " +
            $"Jugadores conectados: {playerCount}"
        );

        if (NetworkManager.Singleton.IsHost)
        {
            UpdatePlayerCount();
            SetStatus("PLAYER CONNECTED");
        }
        else if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("CONNECTED TO ROOM");
            Debug.Log("[Relay][CLIENT] ACCESO A LA SALA CONFIRMADO.");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        int playerCount = networkManager != null
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        string reason = networkManager != null
            ? networkManager.DisconnectReason
            : string.Empty;

        Debug.LogWarning(
            $"[Netcode] CLIENTE DESCONECTADO. ClientId: {clientId}. " +
            $"Jugadores restantes: {playerCount}. Razón: {reason}"
        );

        if (networkManager != null && networkManager.IsHost)
        {
            UpdatePlayerCount();
            SetStatus("WAITING FOR PLAYERS...");
        }
        else if (networkManager != null &&
                 clientId == networkManager.LocalClientId)
        {
            SetStatus("DISCONNECTED");
        }
    }

    private void UpdatePlayerCount()
    {
        if (NetworkManager.Singleton == null)
            return;

        int currentPlayers =
            NetworkManager.Singleton.ConnectedClientsIds.Count;

        if (playerCountDisplay != null)
        {
            playerCountDisplay.text =
                $"PLAYERS: {currentPlayers}/{requiredPlayerCount}";
        }

        if (startGameButton != null)
        {
            startGameButton.interactable =
                NetworkManager.Singleton.IsHost &&
                currentPlayers == requiredPlayerCount;
        }

        Debug.Log(
            $"[Netcode] Jugadores conectados: " +
            $"{currentPlayers}/{requiredPlayerCount}"
        );
    }

    private void ShowConnectionPanel()
    {
        if (connectionPanel != null)
            connectionPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    private void ShowLobby(bool isHost)
    {
        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = false;
        }

        if (codeDisplay != null)
        {
            codeDisplay.gameObject.SetActive(isHost);

            if (isHost)
                codeDisplay.text = $"JOIN CODE: {currentJoinCode}";
        }

        if (playerCountDisplay != null)
            playerCountDisplay.gameObject.SetActive(isHost);
    }

    private void SetStatus(string message)
    {
        if (statusDisplay != null)
            statusDisplay.text = message;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}
