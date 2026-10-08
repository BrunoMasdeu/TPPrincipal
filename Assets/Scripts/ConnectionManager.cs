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
using UnityEngine.UI;

/// <summary>
/// Administra Unity Services, autenticación, Relay y la presentación de la
/// conexión. Las decisiones de modo, cantidad e inicio pertenecen a
/// NetworkLobbySession.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    [Header("Network session")]
    [SerializeField] private NetworkSessionBootstrap sessionBootstrap;

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
    private int pendingPlayerCount;
    private NetworkLobbySession lobbySession;
    private bool servicesInitialized;
    private bool isConnecting;
    private int connectionOperation;

    public GameModeDefinition PendingGameModeDefinition =>
        pendingGameModeDefinition;

    public int PendingPlayerCount => pendingPlayerCount;
    public bool IsConnecting => isConnecting;

    private void Awake()
    {
        Debug.Log("[Relay] ConnectionManager iniciado.");

        if (sessionBootstrap == null)
        {
            Debug.LogError(
                "[Relay] ConnectionManager no tiene NetworkSessionBootstrap configurado."
            );
        }

        initializationTask = InitializeUnityServices();
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
            ShowConnectionPanel();
        }
    }

    private void ResolveSessionBootstrap()
    {
        NetworkManager activeManager = NetworkManager.Singleton;
        if (activeManager != null)
            sessionBootstrap = activeManager.GetComponent<NetworkSessionBootstrap>();
    }

    private void Update()
    {
        if (lobbyPanel != null && lobbyPanel.activeSelf && lobbySession == null &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            BindLobbySession();
            if (lobbySession != null)
                UpdatePlayerCount();
        }
    }

    private void OnGUI()
    {
        if (lobbyPanel == null || !lobbyPanel.activeSelf ||
            lobbySession == null || !lobbySession.IsSpawned ||
            NetworkManager.Singleton == null)
            return;

        // Controles temporales de prueba. La UI definitiva puede invocar los
        // mismos métodos públicos desde botones uGUI sin cambiar el lobby.
        GUILayout.BeginArea(new Rect(Screen.width - 250, 15, 235, 245), GUI.skin.box);
        GUILayout.Label($"MODO: {lobbySession.SelectedGameModeId}");
        GUILayout.Label($"FASE: {lobbySession.Phase}");

        for (int i = 0; i < lobbySession.Players.Count; i++)
        {
            LobbyPlayerData player = lobbySession.Players[i];
            GUILayout.Label($"{player.ClientId}: {player.TeamId} / " +
                (player.IsReady ? "READY" : "WAITING"));
        }

        if (lobbySession.Phase == SessionPhase.Lobby)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equipo rojo")) SelectRedTeam();
            if (GUILayout.Button("Equipo azul")) SelectBlueTeam();
            GUILayout.EndHorizontal();

            if (lobbySession.TryGetPlayerData(
                    NetworkManager.Singleton.LocalClientId,
                    out LobbyPlayerData localPlayer) &&
                GUILayout.Button(localPlayer.IsReady ? "Quitar ready" : "Ready"))
            {
                lobbySession.RequestReadyChange(!localPlayer.IsReady);
            }
        }

        GUILayout.EndArea();
    }

    public void SelectRedTeam() => lobbySession?.RequestTeamChange(TeamId.Red);
    public void SelectBlueTeam() => lobbySession?.RequestTeamChange(TeamId.Blue);

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
        int playerCount,
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

        if (!definition.AllowsPlayerCount(playerCount))
        {
            validationError =
                $"El modo {definition.GameModeId} no admite {playerCount} jugadores.";
            return false;
        }

        pendingGameModeDefinition = definition;
        pendingPlayerCount = playerCount;
        validationError = string.Empty;

        Debug.Log(
            $"[Relay] Selección preparada: {definition.GameModeId}, " +
            $"{playerCount} jugadores."
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
            await initializationTask;

            if (operation != connectionOperation)
                return;

            if (!servicesInitialized)
                throw new InvalidOperationException("Unity Services no está disponible.");

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                throw new InvalidOperationException("NetworkManager no encontrado.");

            if (pendingGameModeDefinition == null || pendingPlayerCount <= 0)
            {
                throw new InvalidOperationException(
                    "No hay un modo y una cantidad de jugadores seleccionados."
                );
            }

            if (sessionBootstrap == null)
            {
                throw new InvalidOperationException(
                    "NetworkSessionBootstrap no está configurado."
                );
            }

            if (!sessionBootstrap.PrepareSession(
                    pendingGameModeDefinition,
                    pendingPlayerCount,
                    out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            // El puente sólo conserva la aprobación y el flujo de carrera.
            // La cantidad multijugador proviene de NetworkSessionBootstrap.
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
                $"{pendingPlayerCount - 1} cliente(s) adicional(es)."
            );

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    pendingPlayerCount - 1
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
                codeDisplay.text = $"JOIN CODE: {currentJoinCode}";

            SetStatus("WAITING FOR PLAYERS...");
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
        }
    }

    public void StartLocalGame(GameModeDefinition definition)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (isConnecting || networkManager == null || networkManager.IsListening)
            return;

        if (!ConfigureSessionSelection(definition, 1, out string error) ||
            !sessionBootstrap.PrepareSession(definition, 1, out error))
        {
            Debug.LogError($"No se pudo preparar la partida individual: {error}");
            SetStatus("LOCAL GAME CONFIGURATION ERROR");
            return;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            sessionBootstrap.CancelPreparedSession();
            SetStatus("UNITY TRANSPORT NOT FOUND");
            return;
        }

        ushort localPort = transport.ConnectionData.Port == 0
            ? (ushort)7777
            : transport.ConnectionData.Port;
        transport.SetConnectionData("127.0.0.1", localPort, "127.0.0.1");

        if (!networkManager.StartHost())
        {
            sessionBootstrap.CancelPreparedSession();
            SetStatus("LOCAL HOST COULD NOT START");
            return;
        }

        ShowLobby(true);
        StartCoroutine(StartLocalMatchWhenReady());
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
        SetStatus("LOCAL GAME NOT READY");
        CancelConnection();
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
        isConnecting = true;
        SetStatus("JOINING ROOM...");

        try
        {
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
                isConnecting = false;
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

        string sceneName = definition.SceneName;
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
        UnbindLobbySession();

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        sessionBootstrap?.CancelPreparedSession();

        currentJoinCode = string.Empty;
        sessionBootstrap?.SetActiveJoinCode(string.Empty);
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

        BindLobbySession();

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
            UnbindLobbySession();
            ShowConnectionPanel();
            SetStatus("DISCONNECTED");
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

        int requiredPlayers = lobbySession != null &&
            lobbySession.RequiredPlayerCount > 0
                ? lobbySession.RequiredPlayerCount
                : pendingPlayerCount;

        if (playerCountDisplay != null)
        {
            playerCountDisplay.text =
                $"PLAYERS: {currentPlayers}/{requiredPlayers}";
        }

        if (startGameButton != null)
        {
            startGameButton.interactable =
                networkManager != null &&
                networkManager.IsHost &&
                lobbySession != null &&
                lobbySession.CanStartMatch();
        }

        Debug.Log(
            $"[Netcode] Jugadores conectados: " +
            $"{currentPlayers}/{requiredPlayers}"
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
        connectionOperation++;
        UnbindLobbySession();

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}
