using System;
using System.Collections;
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

/// <summary>
/// Administra Unity Services, autenticación, Relay y la presentación de la
/// conexión. Las decisiones de modo, cantidad e inicio pertenecen a
/// NetworkLobbySession.
/// </summary>
public partial class ConnectionManager : MonoBehaviour
{
    [Header("Network session")]
    [SerializeField] private NetworkSessionBootstrap sessionBootstrap;

    [Header("Local tutorial")]
    [SerializeField] private string tutorialSceneName = "CentroEntrenamiento";

    [Header("Connection UI")]
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Lobby UI")]
    [SerializeField] private TMP_Text codeDisplay;
    [SerializeField] private TMP_Text statusDisplay;
    [SerializeField] private TMP_Text playerCountDisplay;
    [SerializeField] private Button startGameButton;

    private Task initializationTask;
    private string currentJoinCode;
    private GameModeDefinition pendingGameModeDefinition;
    private int pendingMatchPlayerCount;
    private NetworkLobbySession lobbySession;
    private bool servicesInitialized;
    private bool isConnecting;
    private bool isLocalGameStarting;
    private bool lastNetworkListening;
    private int connectionOperation;

    public GameModeDefinition PendingGameModeDefinition =>
        pendingGameModeDefinition;

    public int PendingMatchPlayerCount => pendingMatchPlayerCount;
    public bool IsConnecting => isConnecting;
    public bool IsLocalGameStarting => isLocalGameStarting;

    private void Awake()
    {
        Debug.Log("[Relay] ConnectionManager iniciado.");

        ResolveSessionBootstrap();
    }

    private void Start()
    {
        ResolveSessionBootstrap();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Relay] No existe NetworkManager.Singleton.");
            SetStatus("NETWORK MANAGER NOT FOUND");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (NetworkManager.Singleton.IsListening)
        {
            currentJoinCode = sessionBootstrap != null
                ? sessionBootstrap.ActiveJoinCode
                : string.Empty;
            BindLobbySession();
            ShowLobby(NetworkManager.Singleton.IsHost);
            UpdatePlayerCount();
            SetStatus("CONNECTED TO ROOM");
        }
        else
        {
            if (menuManager != null)
                menuManager.ShowMainMenu();
            else
                ShowConnectionPanel();
        }

        RefreshMenuUI();
    }

    private void ResolveSessionBootstrap()
    {
        NetworkManager activeManager = NetworkManager.Singleton;
        if (activeManager != null)
            sessionBootstrap = activeManager.GetComponent<NetworkSessionBootstrap>();
    }

    private void Update()
    {
        bool listening = NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening;
        if (listening != lastNetworkListening)
        {
            lastNetworkListening = listening;
            RefreshMenuUI();
        }

        if (lobbyPanel != null && lobbyPanel.activeSelf && lobbySession == null &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            BindLobbySession();
            if (lobbySession != null)
                UpdatePlayerCount();
        }
    }

    public void SelectRedTeam() => lobbySession?.RequestTeamChange(TeamId.Red);
    public void SelectBlueTeam() => lobbySession?.RequestTeamChange(TeamId.Blue);
    public void SelectTdm() =>
        lobbySession?.RequestGameModeChange(GameModeId.TDM);
    public void SelectCtf() =>
        lobbySession?.RequestGameModeChange(GameModeId.CTF);
    public void SelectPlayerCount(int playerCount) =>
        lobbySession?.RequestPlayerCountChange(playerCount);
    public void SelectMap(MapId mapId) => lobbySession?.RequestMapChange(mapId);

    public void ToggleReady()
    {
        if (lobbySession != null && NetworkManager.Singleton != null &&
            lobbySession.TryGetPlayerData(NetworkManager.Singleton.LocalClientId,
                out LobbyPlayerData player))
        {
            lobbySession.RequestReadyChange(!player.IsReady);
        }
    }

    public bool ConfigureSessionSelection(
        GameModeDefinition definition,
        int matchPlayerCount,
        out string validationError)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (isConnecting || (networkManager != null && networkManager.IsListening))
        {
            validationError =
                "No se puede cambiar el modo ni la cantidad durante la conexión.";
            return false;
        }

        if (sessionBootstrap == null)
        {
            validationError =
                "ConnectionManager no tiene NetworkSessionBootstrap configurado.";
            return false;
        }

        if (definition == null)
        {
            validationError = "No se seleccionó un modo de juego.";
            return false;
        }

        if (!definition.IsValid(out validationError))
            return false;

        if (!MultiplayerSessionLimits.CanSelectMatchPlayerCount(
                matchPlayerCount))
        {
            validationError =
                $"La partida debe admitir entre 1 y " +
                $"{MultiplayerSessionLimits.RoomCapacity} jugadores.";
            return false;
        }

        if (!definition.AllowsPlayerCount(matchPlayerCount))
        {
            validationError =
                $"El modo {definition.GameModeId} no admite {matchPlayerCount} jugadores.";
            return false;
        }

        pendingGameModeDefinition = definition;
        pendingMatchPlayerCount = matchPlayerCount;
        validationError = string.Empty;

        Debug.Log(
            $"[Relay] Selección preparada: {definition.GameModeId}, " +
            $"{matchPlayerCount} jugadores."
        );

        UpdatePlayerCount();
        return true;
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

            servicesInitialized = true;

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
        if (isConnecting || (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening))
            return;

        int operation = ++connectionOperation;
        isConnecting = true;
        Debug.Log("[Relay][HOST] Botón CREATE GAME pulsado.");
        SetStatus("CREATING ROOM...");

        try
        {
            ResolveSessionBootstrap();
            initializationTask ??= InitializeUnityServices();
            await initializationTask;

            if (operation != connectionOperation)
                return;

            if (!servicesInitialized)
                throw new InvalidOperationException("Unity Services no está disponible.");

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                throw new InvalidOperationException("NetworkManager no encontrado.");

            if (sessionBootstrap == null)
            {
                throw new InvalidOperationException(
                    "NetworkSessionBootstrap no está configurado."
                );
            }

            if (!sessionBootstrap.PrepareRoom(
                    out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            // El puente conserva la aprobación y el flujo de carrera. La sala
            // multijugador siempre reserva ocho lugares; la cantidad de la
            // partida se elige después dentro del lobby.
            if (networkManager.GetComponent<LobbyPlayerSpawner>() == null)
            {
                throw new InvalidOperationException(
                    "LobbyPlayerSpawner no encontrado."
                );
            }

            UnityTransport transport =
                networkManager.GetComponent<UnityTransport>();

            if (transport == null)
                throw new InvalidOperationException("Unity Transport no encontrado.");

            Debug.Log(
                $"[Relay][HOST] Creando allocation para " +
                $"{MultiplayerSessionLimits.RemoteClientCapacity} " +
                $"cliente(s) adicional(es)."
            );

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    MultiplayerSessionLimits.RemoteClientCapacity
                );

            if (operation != connectionOperation)
                return;

            currentJoinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (operation != connectionOperation)
                return;

            Debug.Log($"[Relay][HOST] JOIN CODE GENERADO: {currentJoinCode}");
            sessionBootstrap.SetActiveJoinCode(currentJoinCode);

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
                sessionBootstrap.CancelPreparedSession();
                return;
            }

            Debug.Log(
                $"[Relay][HOST] Host iniciado. LocalClientId: " +
                $"{networkManager.LocalClientId}"
            );

            BindLobbySession();
            ShowLobby(true);
            UpdatePlayerCount();

            if (codeDisplay != null)
                codeDisplay.text = $"CÓDIGO: {currentJoinCode}";

            SetStatus("CONFIGURE MATCH");
        }
        catch (Exception exception)
        {
            if (operation != connectionOperation)
                return;

            Debug.LogError($"[Relay][HOST] Error creando la sala: {exception.Message}");
            Debug.LogException(exception);
            SetStatus("ERROR CREATING ROOM");
            sessionBootstrap?.CancelPreparedSession();
        }
        finally
        {
            if (operation == connectionOperation)
                isConnecting = false;
            RefreshMenuUI();
        }
    }

    public bool StartLocalTutorial()
    {
        ResolveSessionBootstrap();
        NetworkManager networkManager = NetworkManager.Singleton;

        if (isConnecting || networkManager == null || networkManager.IsListening)
            return false;

        if (sessionBootstrap == null)
        {
            Debug.LogError(
                "ConnectionManager no tiene NetworkSessionBootstrap configurado."
            );
            SetStatus("LOCAL TUTORIAL CONFIGURATION ERROR");
            return false;
        }

        if (string.IsNullOrWhiteSpace(tutorialSceneName))
        {
            Debug.LogError("No se configuró la escena del tutorial.");
            SetStatus("TUTORIAL SCENE NOT CONFIGURED");
            return false;
        }

        if (!sessionBootstrap.PrepareRoom(out string error))
        {
            Debug.LogError($"No se pudo preparar el tutorial local: {error}");
            SetStatus("LOCAL TUTORIAL CONFIGURATION ERROR");
            return false;
        }

        pendingGameModeDefinition = null;
        pendingMatchPlayerCount = 0;
        isLocalGameStarting = true;

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            isLocalGameStarting = false;
            sessionBootstrap.CancelPreparedSession();
            SetStatus("UNITY TRANSPORT NOT FOUND");
            return false;
        }

        ushort localPort = transport.ConnectionData.Port == 0
            ? (ushort)7777
            : transport.ConnectionData.Port;
        transport.SetConnectionData("127.0.0.1", localPort, "127.0.0.1");

        if (!networkManager.StartHost())
        {
            isLocalGameStarting = false;
            sessionBootstrap.CancelPreparedSession();
            SetStatus("LOCAL HOST COULD NOT START");
            return false;
        }

        HideConnectionPanels();
        StartCoroutine(LoadLocalTutorialWhenReady());
        return true;
    }

    private IEnumerator LoadLocalTutorialWhenReady()
    {
        float deadline = Time.realtimeSinceStartup + 5f;

        while (Time.realtimeSinceStartup < deadline)
        {
            ResolveSessionBootstrap();

            NetworkManager networkManager = NetworkManager.Singleton;
            NetworkLobbySession session = sessionBootstrap != null
                ? sessionBootstrap.CurrentSession
                : null;

            if (networkManager != null && networkManager.IsHost &&
                networkManager.SceneManager != null &&
                session != null && session.IsSpawned)
            {
                SceneEventProgressStatus status =
                    networkManager.SceneManager.LoadScene(
                        tutorialSceneName,
                        LoadSceneMode.Single
                    );

                if (status == SceneEventProgressStatus.Started)
                    yield break;

                Debug.LogError(
                    $"No se pudo cargar el tutorial {tutorialSceneName}: {status}."
                );
                SetStatus("LOCAL TUTORIAL COULD NOT START");
                CancelConnection();
                yield break;
            }

            yield return null;
        }

        Debug.LogError("La sesión local del tutorial no quedó lista para cargar.");
        SetStatus("LOCAL TUTORIAL NOT READY");
        CancelConnection();
    }

    public bool StartLocalGame(GameModeDefinition definition)
    {
        ResolveSessionBootstrap();
        NetworkManager networkManager = NetworkManager.Singleton;

        if (isConnecting || networkManager == null || networkManager.IsListening)
            return false;

        if (!ConfigureSessionSelection(definition, 1, out string error) ||
            !sessionBootstrap.PrepareSession(definition, 1, out error))
        {
            Debug.LogError($"No se pudo preparar la partida individual: {error}");
            SetStatus("LOCAL GAME CONFIGURATION ERROR");
            return false;
        }

        isLocalGameStarting = true;

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            isLocalGameStarting = false;
            sessionBootstrap.CancelPreparedSession();
            SetStatus("UNITY TRANSPORT NOT FOUND");
            return false;
        }

        ushort localPort = transport.ConnectionData.Port == 0
            ? (ushort)7777
            : transport.ConnectionData.Port;
        transport.SetConnectionData("127.0.0.1", localPort, "127.0.0.1");

        if (!networkManager.StartHost())
        {
            isLocalGameStarting = false;
            sessionBootstrap.CancelPreparedSession();
            SetStatus("LOCAL HOST COULD NOT START");
            return false;
        }

        HideConnectionPanels();
        StartCoroutine(StartLocalMatchWhenReady());
        return true;
    }

    private IEnumerator StartLocalMatchWhenReady()
    {
        float deadline = Time.realtimeSinceStartup + 5f;

        while (Time.realtimeSinceStartup < deadline)
        {
            BindLobbySession();

            if (lobbySession != null && lobbySession.IsSpawned &&
                lobbySession.CanStartMatch())
            {
                StartGame();
                yield break;
            }

            yield return null;
        }

        Debug.LogError("La sesión individual no quedó lista para empezar.");
        CancelConnection();
        SetStatus("LOCAL GAME NOT READY");
    }

    public async void StartClientWithRelay()
    {
        if (isConnecting || (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening))
            return;

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

        int operation = ++connectionOperation;
        bool clientStarted = false;
        isConnecting = true;
        SetStatus("JOINING ROOM...");

        try
        {
            ResolveSessionBootstrap();
            initializationTask ??= InitializeUnityServices();
            await initializationTask;

            if (operation != connectionOperation)
                return;

            if (!servicesInitialized)
                throw new InvalidOperationException("Unity Services no está disponible.");

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

            if (operation != connectionOperation)
                return;

            Debug.Log(
                $"[Relay][CLIENT] Sala encontrada. AllocationId: " +
                $"{joinAllocation.AllocationId}"
            );

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(joinAllocation, "dtls")
            );

            Debug.Log("[Relay][CLIENT] Unity Transport configurado.");

            currentJoinCode = joinCode;
            sessionBootstrap?.SetActiveJoinCode(joinCode);
            ShowLobby(false);
            SetStatus("CONNECTING...");

            bool started = networkManager.StartClient();
            clientStarted = started;

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
            if (operation != connectionOperation)
                return;

            Debug.LogError($"[Relay][CLIENT] Error entrando a la sala: {exception.Message}");
            Debug.LogException(exception);

            ShowConnectionPanel();
            SetStatus("COULD NOT JOIN ROOM");
        }
        finally
        {
            if (operation == connectionOperation)
                isConnecting = clientStarted && NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsListening &&
                    !NetworkManager.Singleton.IsConnectedClient;
            RefreshMenuUI();
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

        BindLobbySession();

        if (lobbySession == null)
        {
            Debug.LogError(
                "[Relay] No existe NetworkLobbySession para iniciar la partida."
            );
            SetStatus("SESSION NOT AVAILABLE");
            return;
        }

        NetworkLobbySession session = lobbySession;
        GameModeDefinition definition = session.SelectedDefinition;

        if (definition == null)
        {
            Debug.LogError("[Relay] La sesión no tiene una definición de modo válida.");
            SetStatus("GAME MODE NOT AVAILABLE");
            return;
        }

        string sceneName = session.SelectedSceneName;
        SetStatus("LOADING MATCH...");

        if (!session.TryStartMatch())
        {
            Debug.LogWarning(
                "[Relay] NetworkLobbySession rechazó el inicio de la partida."
            );
            SetStatus("PLAYERS MUST CHOOSE TEAM AND READY");
            UpdatePlayerCount();
            return;
        }

        Debug.Log(
            $"[Relay][HOST] Carga solicitada para {sceneName} " +
            $"mediante NetworkLobbySession."
        );
    }

    public void CancelConnection()
    {
        Debug.Log("[Relay] Cancelando conexión y cerrando NetworkManager.");

        connectionOperation++;
        isConnecting = false;
        isLocalGameStarting = false;
        UnbindLobbySession();

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        sessionBootstrap?.CancelPreparedSession();

        currentJoinCode = string.Empty;
        pendingGameModeDefinition = null;
        pendingMatchPlayerCount = 0;
        sessionBootstrap?.SetActiveJoinCode(string.Empty);
        ShowConnectionPanel();
        SetStatus(string.Empty);
        RefreshMenuUI();
    }

    private void OnClientConnected(ulong clientId)
    {
        int playerCount =
            NetworkManager.Singleton.ConnectedClientsIds.Count;

        Debug.Log(
            $"[Netcode] CLIENTE CONECTADO. ClientId: {clientId}. " +
            $"Jugadores conectados: {playerCount}"
        );

        BindLobbySession();
        if (clientId == NetworkManager.Singleton.LocalClientId)
            isConnecting = false;

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
        RefreshMenuUI();
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
            connectionOperation++;
            isConnecting = false;
            currentJoinCode = string.Empty;
            sessionBootstrap?.SetActiveJoinCode(string.Empty);
            UnbindLobbySession();
            ShowConnectionPanel();
            SetStatus(string.IsNullOrEmpty(reason) ? "DISCONNECTED" : reason);
        }
    }

    private void UpdatePlayerCount()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        int currentPlayers = lobbySession != null && lobbySession.IsSpawned
            ? lobbySession.Players.Count
            : networkManager != null
                ? networkManager.ConnectedClientsIds.Count
                : 0;

        int selectedPlayers = lobbySession != null &&
            lobbySession.SelectedPlayerCount > 0
                ? lobbySession.SelectedPlayerCount
                : pendingMatchPlayerCount;

        if (playerCountDisplay != null)
        {
            playerCountDisplay.text = selectedPlayers > 0
                ? $"JUGADORES: {currentPlayers}/{selectedPlayers}"
                : $"SALA: {currentPlayers}/" +
                  $"{MultiplayerSessionLimits.RoomCapacity}";
        }

        if (startGameButton != null)
        {
            startGameButton.interactable =
                networkManager != null &&
                networkManager.IsHost &&
                lobbySession != null &&
                lobbySession.CanStartMatch();
        }

        RefreshMenuUI();
        Debug.Log(
            $"[Netcode] Jugadores conectados: " +
            $"{currentPlayers}; partida: {selectedPlayers}; " +
            $"sala: {MultiplayerSessionLimits.RoomCapacity}."
        );
    }

    private void BindLobbySession()
    {
        NetworkLobbySession session = sessionBootstrap != null
            ? sessionBootstrap.CurrentSession
            : null;

        if (session == null)
            session = NetworkLobbySession.Instance;

        if (lobbySession == session)
            return;

        UnbindLobbySession();
        lobbySession = session;

        if (lobbySession != null)
            lobbySession.LobbyStateChanged += OnLobbyStateChanged;
    }

    private void UnbindLobbySession()
    {
        if (lobbySession != null)
            lobbySession.LobbyStateChanged -= OnLobbyStateChanged;

        lobbySession = null;
    }

    private void OnLobbyStateChanged()
    {
        UpdatePlayerCount();
    }

    private void ShowConnectionPanel()
    {
        menuManager?.ShowConnectionMenu();
        if (connectionPanel != null)
            connectionPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    private void HideConnectionPanels()
    {
        menuManager?.HideMenu();
        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    private void ShowLobby(bool isHost)
    {
        menuManager?.ShowLobbyMenu();
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
            codeDisplay.gameObject.SetActive(true);
            codeDisplay.text = $"CÓDIGO: {currentJoinCode}";
        }

        if (playerCountDisplay != null)
            playerCountDisplay.gameObject.SetActive(true);
        RefreshMenuUI();
    }

    private void SetStatus(string message)
    {
        message = TranslateStatus(message);
        if (statusDisplay != null)
            statusDisplay.text = message;
        if (connectionStatusDisplay != null)
            connectionStatusDisplay.text = message;
        RefreshMenuUI();
    }

    private void OnDestroy()
    {
        connectionOperation++;
        UnbindLobbySession();

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}
