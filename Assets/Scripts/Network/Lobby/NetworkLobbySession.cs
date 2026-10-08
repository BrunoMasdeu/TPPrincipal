using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fuente autoritativa del estado compartido entre el lobby y la carga de partida.
/// Los clientes solicitan cambios; solamente el servidor modifica los datos replicados.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkLobbySession : NetworkBehaviour
{
    public static NetworkLobbySession Instance { get; private set; }

    [Header("Game modes")]
    [SerializeField] private GameModeDefinition tdmDefinition;
    [SerializeField] private GameModeDefinition ctfDefinition;
    [SerializeField] private GameModeDefinition raceDefinition;

    [Header("Session")]
    [SerializeField] private string lobbySceneName = "MenuScene";
    [SerializeField] private bool requireBalancedTeams = true;

    private readonly NetworkVariable<GameModeId> selectedGameModeId = new(
        GameModeId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> requiredPlayerCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<SessionPhase> sessionPhase = new(
        SessionPhase.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkList<LobbyPlayerData> players;
    private GameModeDefinition preparedDefinition;
    private int preparedPlayerCount;
    private bool hasPreparedConfiguration;
    private bool gameplaySceneLoaded;
    private bool returnToLobbyRequested;
    private string activeGameplaySceneName = string.Empty;

    public event Action LobbyStateChanged;
    public event Action<NetworkListEvent<LobbyPlayerData>> PlayersChanged;

    public NetworkList<LobbyPlayerData> Players => players;
    public GameModeId SelectedGameModeId => selectedGameModeId.Value;
    public int RequiredPlayerCount => requiredPlayerCount.Value;
    public SessionPhase Phase => sessionPhase.Value;
    public GameModeDefinition SelectedDefinition =>
        ResolveDefinition(selectedGameModeId.Value);

    private void Awake()
    {
        players = new NetworkList<LobbyPlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // La sesión debe sobrevivir a LoadSceneMode.Single en host y clientes.
        // NetworkSceneManager moverá este objeto dinámico a DontDestroyOnLoad.
        NetworkObject.DestroyWithScene = false;

        players.OnListChanged += OnPlayersChanged;
        selectedGameModeId.OnValueChanged += OnGameModeChanged;
        requiredPlayerCount.OnValueChanged += OnRequiredPlayerCountChanged;
        sessionPhase.OnValueChanged += OnSessionPhaseChanged;

        if (!IsServer)
            return;

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

        ResetServerState();

        if (hasPreparedConfiguration)
            ApplyPreparedConfiguration();

        IReadOnlyList<ulong> connectedClientIds =
            NetworkManager.ConnectedClientsIds;

        for (int i = 0; i < connectedClientIds.Count; i++)
            RegisterPlayer(connectedClientIds[i]);
    }

    public override void OnNetworkDespawn()
    {
        if (sessionPhase.Value == SessionPhase.Loading)
        {
            Debug.LogError(
                "[Session] NetworkSessionRoot fue despawneado durante la carga."
            );
        }

        if (Instance == this)
            Instance = null;

        players.OnListChanged -= OnPlayersChanged;
        selectedGameModeId.OnValueChanged -= OnGameModeChanged;
        requiredPlayerCount.OnValueChanged -= OnRequiredPlayerCountChanged;
        sessionPhase.OnValueChanged -= OnSessionPhaseChanged;

        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -=
                    OnLoadEventCompleted;
            }

            ResetServerState();
        }
    }

    public bool ConfigureSession(
        GameModeDefinition definition,
        int playerCount,
        out string validationError)
    {
        if (IsSpawned && !IsServer)
        {
            validationError =
                "Solamente el servidor puede configurar la sesión.";
            return false;
        }

        if (IsSpawned && sessionPhase.Value != SessionPhase.Lobby)
        {
            validationError =
                "La sesión solamente puede configurarse desde el lobby.";
            return false;
        }

        if (definition == null)
        {
            validationError = "No se indicó una definición de modo.";
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

        if (ResolveDefinition(definition.GameModeId) != definition)
        {
            validationError =
                "La definición no está registrada en NetworkLobbySession.";
            return false;
        }

        preparedDefinition = definition;
        preparedPlayerCount = playerCount;
        hasPreparedConfiguration = true;

        if (IsSpawned)
            ApplyPreparedConfiguration();

        validationError = string.Empty;
        return true;
    }

    public void RequestTeamChange(TeamId requestedTeam)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryChangeTeam(NetworkManager.LocalClientId, requestedTeam);
            return;
        }

        RequestTeamChangeRpc(requestedTeam);
    }

    public void RequestReadyChange(bool isReady)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryChangeReady(NetworkManager.LocalClientId, isReady);
            return;
        }

        RequestReadyChangeRpc(isReady);
    }

    public void RequestStartMatch()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryStartMatch();
            return;
        }

        RequestStartMatchRpc();
    }

    public bool CanStartMatch()
    {
        if (!IsServer || sessionPhase.Value != SessionPhase.Lobby)
            return false;

        GameModeDefinition definition = SelectedDefinition;

        return definition != null &&
            definition.IsValid(out _) &&
            definition.AllowsPlayerCount(requiredPlayerCount.Value) &&
            LobbyValidationRules.CanStartMatch(
                selectedGameModeId.Value,
                requiredPlayerCount.Value,
                CreatePlayerSnapshot(),
                requireBalancedTeams,
                definition.RequiresTeams
            );
    }

    public bool TryStartMatch()
    {
        GameModeDefinition definition = SelectedDefinition;

        if (!CanStartMatch())
        {
            Debug.LogWarning(
                "No se puede iniciar: revise modo, cantidad, equipos y ready."
            );
            return false;
        }

        sessionPhase.Value = SessionPhase.Loading;
        gameplaySceneLoaded = false;
        returnToLobbyRequested = false;
        activeGameplaySceneName = definition.SceneName;

        SceneEventProgressStatus status =
            NetworkManager.SceneManager.LoadScene(
                activeGameplaySceneName,
                LoadSceneMode.Single
            );

        if (status == SceneEventProgressStatus.Started)
            return true;

        Debug.LogError(
            $"No se pudo iniciar la carga de {activeGameplaySceneName}: {status}."
        );

        sessionPhase.Value = SessionPhase.Lobby;
        activeGameplaySceneName = string.Empty;
        return false;
    }

    public bool NotifyPlayersSpawned()
    {
        if (!IsServer ||
            sessionPhase.Value != SessionPhase.Loading ||
            returnToLobbyRequested ||
            SceneManager.GetActiveScene().name != activeGameplaySceneName)
        {
            return false;
        }

        gameplaySceneLoaded = true;
        sessionPhase.Value = SessionPhase.InMatch;
        return true;
    }

    public bool NotifySpawnFailed(string reason)
    {
        if (!IsServer || sessionPhase.Value != SessionPhase.Loading)
            return false;

        CancelLoading(reason);
        return true;
    }

    public bool TryGetPlayerData(
        ulong clientId,
        out LobbyPlayerData playerData)
    {
        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex >= 0)
        {
            playerData = players[playerIndex];
            return true;
        }

        playerData = default;
        return false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTeamChangeRpc(
        TeamId requestedTeam,
        RpcParams rpcParams = default)
    {
        TryChangeTeam(rpcParams.Receive.SenderClientId, requestedTeam);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReadyChangeRpc(
        bool isReady,
        RpcParams rpcParams = default)
    {
        TryChangeReady(rpcParams.Receive.SenderClientId, isReady);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStartMatchRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning(
                $"El cliente {rpcParams.Receive.SenderClientId} intentó iniciar la partida."
            );
            return;
        }

        TryStartMatch();
    }

    private bool TryChangeTeam(ulong clientId, TeamId requestedTeam)
    {
        if (!IsServer || sessionPhase.Value != SessionPhase.Lobby ||
            SelectedDefinition == null || !SelectedDefinition.RequiresTeams)
            return false;

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex < 0 ||
            players[playerIndex].TeamId == requestedTeam ||
            !LobbyValidationRules.CanAssignPlayerToTeam(
                CreatePlayerSnapshot(),
                clientId,
                requestedTeam,
                requiredPlayerCount.Value))
        {
            return false;
        }

        LobbyPlayerData player = players[playerIndex];
        player.TeamId = requestedTeam;
        player.IsReady = false;
        players[playerIndex] = player;
        return true;
    }

    private bool TryChangeReady(ulong clientId, bool isReady)
    {
        if (!IsServer || sessionPhase.Value != SessionPhase.Lobby)
            return false;

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex < 0)
            return false;

        LobbyPlayerData player = players[playerIndex];

        if (isReady && SelectedDefinition != null &&
            SelectedDefinition.RequiresTeams &&
            !LobbyValidationRules.IsValidTeam(player.TeamId))
            return false;

        if (player.IsReady == isReady)
            return false;

        player.IsReady = isReady;
        players[playerIndex] = player;
        return true;
    }

    private void ApplyPreparedConfiguration()
    {
        selectedGameModeId.Value = preparedDefinition.GameModeId;
        requiredPlayerCount.Value = preparedPlayerCount;
        sessionPhase.Value = SessionPhase.Lobby;
        ResetAllPlayersReady();
    }

    private void RegisterPlayer(ulong clientId)
    {
        if (!IsServer || FindPlayerIndex(clientId) >= 0)
            return;

        bool autoReady = SelectedDefinition != null &&
            !SelectedDefinition.RequiresTeams;
        players.Add(new LobbyPlayerData(clientId, TeamId.None, autoReady));
    }

    private void OnClientConnected(ulong clientId)
    {
        RegisterPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex >= 0)
            players.RemoveAt(playerIndex);

        if (sessionPhase.Value == SessionPhase.Loading)
        {
            CancelLoading(
                $"Se canceló la carga porque el cliente {clientId} se desconectó."
            );
        }
    }

    private void CancelLoading(string reason)
    {
        Debug.LogWarning(reason);

        sessionPhase.Value = SessionPhase.Lobby;
        gameplaySceneLoaded = false;
        returnToLobbyRequested = true;
        ResetAllPlayersReady();
        TryReturnToLobby();
    }

    private void TryReturnToLobby()
    {
        if (!IsServer ||
            !returnToLobbyRequested ||
            string.IsNullOrWhiteSpace(lobbySceneName))
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == lobbySceneName)
        {
            returnToLobbyRequested = false;
            activeGameplaySceneName = string.Empty;
            return;
        }

        SceneEventProgressStatus status =
            NetworkManager.SceneManager.LoadScene(
                lobbySceneName,
                LoadSceneMode.Single
            );

        if (status != SceneEventProgressStatus.Started &&
            status != SceneEventProgressStatus.SceneEventInProgress)
        {
            Debug.LogError(
                $"No se pudo regresar al lobby después de la desconexión: {status}."
            );
        }
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!IsServer)
            return;

        if (returnToLobbyRequested)
        {
            if (sceneName == lobbySceneName)
            {
                returnToLobbyRequested = false;
                activeGameplaySceneName = string.Empty;
            }
            else
            {
                TryReturnToLobby();
            }

            return;
        }

        if (sessionPhase.Value != SessionPhase.Loading ||
            sceneName != activeGameplaySceneName)
        {
            return;
        }

        gameplaySceneLoaded = clientsTimedOut.Count == 0 &&
            clientsCompleted.Count == NetworkManager.ConnectedClientsIds.Count;

        if (!gameplaySceneLoaded)
        {
            CancelLoading(
                "Se canceló la carga porque faltan clientes o hubo timeouts."
            );
        }
    }

    private int FindPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
                return i;
        }

        return -1;
    }

    private List<LobbyPlayerData> CreatePlayerSnapshot()
    {
        List<LobbyPlayerData> snapshot = new(players.Count);

        for (int i = 0; i < players.Count; i++)
            snapshot.Add(players[i]);

        return snapshot;
    }

    private void ResetAllPlayersReady()
    {
        if (!IsServer)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData player = players[i];

            if (!player.IsReady)
                continue;

            player.IsReady = false;
            players[i] = player;
        }
    }

    private void ResetServerState()
    {
        if (!IsServer)
            return;

        players.Clear();
        selectedGameModeId.Value = GameModeId.None;
        requiredPlayerCount.Value = 0;
        sessionPhase.Value = SessionPhase.Lobby;
        gameplaySceneLoaded = false;
        returnToLobbyRequested = false;
        activeGameplaySceneName = string.Empty;
    }

    private GameModeDefinition ResolveDefinition(GameModeId gameModeId)
    {
        return gameModeId switch
        {
            GameModeId.TDM => tdmDefinition,
            GameModeId.CTF => ctfDefinition,
            GameModeId.Race => raceDefinition,
            _ => null
        };
    }

    private void OnPlayersChanged(
        NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        PlayersChanged?.Invoke(changeEvent);
        LobbyStateChanged?.Invoke();
    }

    private void OnGameModeChanged(GameModeId previous, GameModeId current)
    {
        LobbyStateChanged?.Invoke();
    }

    private void OnRequiredPlayerCountChanged(int previous, int current)
    {
        LobbyStateChanged?.Invoke();
    }

    private void OnSessionPhaseChanged(
        SessionPhase previous,
        SessionPhase current)
    {
        LobbyStateChanged?.Invoke();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        players?.Dispose();
        base.OnDestroy();
    }
}
