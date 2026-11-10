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
    [SerializeField] private GameModeDefinition zombiesDefinition;

    [Header("Maps")]
    [SerializeField] private MapDefinition[] availableMaps =
        System.Array.Empty<MapDefinition>();

    [Header("Session")]
    [SerializeField] private string lobbySceneName = "MenuScene";
    [SerializeField] private bool requireBalancedTeams = true;

    private readonly NetworkVariable<GameModeId> selectedGameModeId = new(
        GameModeId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedPlayerCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<MapId> selectedMapId = new(
        MapId.None,
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
    private int preparedMatchPlayerCount;
    private MapId preparedMapId;
    private bool hasPreparedConfiguration;
    private bool gameplaySceneLoaded;
    private bool returnToLobbyRequested;
    private string activeGameplaySceneName = string.Empty;

    public event Action LobbyStateChanged;
    public event Action<NetworkListEvent<LobbyPlayerData>> PlayersChanged;

    /// <summary>
    /// [COG-599] Solo en el servidor: un jugador se fue en medio de la partida.
    /// Lo escucha NetworkMatchManager para avisar a todos y revisar los equipos.
    /// </summary>
    public event Action<ulong, string, TeamId> JugadorAbandonoPartida;

    public NetworkList<LobbyPlayerData> Players => players;
    public GameModeId SelectedGameModeId => selectedGameModeId.Value;
    public int SelectedPlayerCount => selectedPlayerCount.Value;
    public int RoomCapacity => MultiplayerSessionLimits.RoomCapacity;
    public MapId SelectedMapId => selectedMapId.Value;
    public SessionPhase Phase => sessionPhase.Value;
    public bool HasCompleteMatchConfiguration
    {
        get
        {
            GameModeDefinition definition = SelectedDefinition;
            bool hasValidScene = definition != null &&
                HasValidSceneSelection(definition, out _);

            return LobbyConfigurationRules.IsConfigurationComplete(
                definition,
                selectedPlayerCount.Value,
                hasValidScene
            );
        }
    }
    public GameModeDefinition SelectedDefinition =>
        ResolveDefinition(selectedGameModeId.Value);
    public MapDefinition SelectedMapDefinition =>
        MapCatalogRules.FindById(availableMaps, selectedMapId.Value);
    public IReadOnlyList<MapDefinition> AvailableMaps => availableMaps;
    public string SelectedSceneName
    {
        get
        {
            MapDefinition selectedMap = SelectedMapDefinition;

            if (selectedMap != null)
                return selectedMap.SceneName;

            // Compatibilidad con sesiones antiguas o prefabs que todavía no
            // tengan el catálogo asignado en el Inspector.
            return SelectedDefinition != null
                ? SelectedDefinition.SceneName
                : string.Empty;
        }
    }

    private void Awake()
    {
        players = new NetworkList<LobbyPlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    // [COG-607] El servidor mide el ping una vez por segundo: más seguido no
    // aporta nada y haría viajar la lista de jugadores todo el tiempo.
    private const float SegundosEntrePings = 1f;
    private float proximaMedicionPing;

    private void Update()
    {
        MedirPings();
    }

    /// <summary>
    /// [COG-607] Solo en el servidor y durante la partida: guarda en cada
    /// jugador cuánto tarda en ir y volver un mensaje a su PC (el ping).
    /// Al estar en la lista replicada, lo ven todos.
    /// </summary>
    private void MedirPings()
    {
        if (!IsSpawned || !IsServer ||
            sessionPhase.Value != SessionPhase.InMatch ||
            Time.unscaledTime < proximaMedicionPing)
        {
            return;
        }

        proximaMedicionPing = Time.unscaledTime + SegundosEntrePings;

        NetworkTransport transporte = NetworkManager != null &&
            NetworkManager.NetworkConfig != null
                ? NetworkManager.NetworkConfig.NetworkTransport
                : null;

        if (transporte == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData jugador = players[i];
            ushort ping = 0;

            // El host no se mide a sí mismo: es la PC que hace de servidor.
            if (jugador.ClientId != NetworkManager.ServerClientId)
            {
                ulong milisegundos = transporte.GetCurrentRtt(jugador.ClientId);
                ping = milisegundos > ushort.MaxValue
                    ? ushort.MaxValue
                    : (ushort)milisegundos;
            }

            if (jugador.Ping == ping)
                continue;

            jugador.Ping = ping;
            players[i] = jugador;
        }
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // La sesión debe sobrevivir a LoadSceneMode.Single en host y clientes.
        // NetworkSceneManager moverá este objeto dinámico a DontDestroyOnLoad.
        NetworkObject.DestroyWithScene = false;

        players.OnListChanged += OnPlayersChanged;
        selectedGameModeId.OnValueChanged += OnGameModeChanged;
        selectedPlayerCount.OnValueChanged += OnSelectedPlayerCountChanged;
        selectedMapId.OnValueChanged += OnSelectedMapChanged;
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
        selectedPlayerCount.OnValueChanged -= OnSelectedPlayerCountChanged;
        selectedMapId.OnValueChanged -= OnSelectedMapChanged;
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
        int matchPlayerCount,
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

        if (!MultiplayerSessionLimits.CanSelectMatchPlayerCount(
                matchPlayerCount))
        {
            validationError =
                $"La partida debe admitir entre 1 y {RoomCapacity} jugadores.";
            return false;
        }

        if (!definition.AllowsPlayerCount(matchPlayerCount))
        {
            validationError =
                $"El modo {definition.GameModeId} no admite {matchPlayerCount} jugadores.";
            return false;
        }

        if (ResolveDefinition(definition.GameModeId) != definition)
        {
            validationError =
                "La definición no está registrada en NetworkLobbySession.";
            return false;
        }

        if (!MapCatalogRules.IsCatalogValid(
                availableMaps,
                out validationError))
        {
            return false;
        }

        MapDefinition defaultMap = MapCatalogRules.FindDefault(
            availableMaps,
            definition.GameModeId
        );

        preparedDefinition = definition;
        preparedMatchPlayerCount = matchPlayerCount;
        preparedMapId = defaultMap != null ? defaultMap.MapId : MapId.None;
        hasPreparedConfiguration = true;

        if (IsSpawned)
            ApplyPreparedConfiguration();

        validationError = string.Empty;
        return true;
    }

    public bool PrepareUnconfiguredRoom(out string validationError)
    {
        if (IsSpawned && !IsServer)
        {
            validationError =
                "Solamente el servidor puede preparar la sala.";
            return false;
        }

        if (IsSpawned && sessionPhase.Value != SessionPhase.Lobby)
        {
            validationError =
                "La sala solamente puede reiniciarse desde el lobby.";
            return false;
        }

        if (!MapCatalogRules.IsCatalogValid(
                availableMaps,
                out validationError))
        {
            return false;
        }

        preparedDefinition = null;
        preparedMatchPlayerCount = 0;
        preparedMapId = MapId.None;
        hasPreparedConfiguration = false;

        if (IsSpawned)
        {
            selectedGameModeId.Value = GameModeId.None;
            selectedPlayerCount.Value = 0;
            selectedMapId.Value = MapId.None;
            ResetAllPlayersReady();
        }

        validationError = string.Empty;
        return true;
    }

    public void RequestGameModeChange(GameModeId requestedMode)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryChangeGameMode(NetworkManager.LocalClientId, requestedMode);
            return;
        }

        RequestGameModeChangeRpc(requestedMode);
    }

    public void RequestPlayerCountChange(int requestedPlayerCount)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryChangePlayerCount(
                NetworkManager.LocalClientId,
                requestedPlayerCount);
            return;
        }

        RequestPlayerCountChangeRpc(requestedPlayerCount);
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

    public void RequestMapChange(MapId requestedMapId)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            TryChangeMap(NetworkManager.LocalClientId, requestedMapId);
            return;
        }

        RequestMapChangeRpc(requestedMapId);
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

        return HasCompleteMatchConfiguration &&
            LobbyValidationRules.CanStartMatch(
                selectedGameModeId.Value,
                selectedPlayerCount.Value,
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
        activeGameplaySceneName = SelectedSceneName;
        // [COG-620] Cada partida empieza sin votos de revancha.
        LimpiarVotosRevancha();

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

    /// <summary>
    /// Inicia Race desde el tutorial offline usando el host local existente.
    /// No expone Race como opción del lobby multijugador.
    /// </summary>
    public bool TryStartLocalRaceFromTutorial(
        string targetSceneName,
        out string validationError)
    {
        if (!IsSpawned || !IsServer || !NetworkManager.IsHost ||
            sessionPhase.Value != SessionPhase.Lobby ||
            selectedGameModeId.Value != GameModeId.None ||
            SceneManager.GetActiveScene().name != "CentroEntrenamiento" ||
            NetworkManager.ConnectedClientsIds.Count != 1 ||
            players.Count != 1 ||
            players[0].ClientId != NetworkManager.LocalClientId ||
            players[0].TeamId != TeamId.None)
        {
            validationError =
                "La carrera sólo puede iniciarse desde el tutorial local de un jugador.";
            return false;
        }

        TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
        if (tutorial == null || !tutorial.IsCompleted)
        {
            validationError = "Completá el tutorial antes de entrar a Race.";
            return false;
        }

        if (raceDefinition == null ||
            raceDefinition.GameModeId != GameModeId.Race ||
            !string.Equals(
                raceDefinition.SceneName,
                targetSceneName,
                StringComparison.Ordinal))
        {
            validationError =
                "La puerta no apunta a la escena configurada para Race.";
            return false;
        }

        if (!ConfigureSession(raceDefinition, 1, out validationError))
            return false;

        if (SelectedSceneName != targetSceneName ||
            !TryChangeReady(NetworkManager.LocalClientId, true) ||
            !TryStartMatch())
        {
            PrepareUnconfiguredRoom(out _);
            validationError =
                "No se pudo preparar o cargar Race desde el tutorial.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    // ---- [COG-620] Votación de revancha ----

    /// <summary>Cuántos jugadores ya votaron la revancha.</summary>
    public int VotosRevancha
    {
        get
        {
            int votos = 0;

            for (int i = 0; i < players.Count; i++)
                if (players[i].QuiereRevancha)
                    votos++;

            return votos;
        }
    }

    public int VotosParaRevancha => RevanchaRules.VotosNecesarios(players.Count);

    /// <summary>
    /// La revancha se ofrece en TDM y CTF, con la partida terminada y con
    /// todos los jugadores conectados: si alguien se fue, no hay revancha.
    /// </summary>
    public bool RevanchaDisponible
    {
        get
        {
            if (!IsSpawned)
                return false;

            NetworkMatchManager match = GetComponent<NetworkMatchManager>();

            return match != null && RevanchaRules.PuedeVotarse(
                selectedGameModeId.Value,
                sessionPhase.Value,
                match.Phase,
                players.Count,
                selectedPlayerCount.Value
            );
        }
    }

    public bool YaVotoRevancha(ulong clientId) =>
        TryGetPlayerData(clientId, out LobbyPlayerData jugador) &&
        jugador.QuiereRevancha;

    /// <summary>Lo llama el HUD cuando el jugador toca JUGAR REVANCHA.</summary>
    public void PedirRevancha()
    {
        if (!IsSpawned)
            return;

        VotarRevanchaRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void VotarRevanchaRpc(RpcParams rpcParams = default)
    {
        RegistrarVotoRevancha(rpcParams.Receive.SenderClientId);
    }

    private void RegistrarVotoRevancha(ulong clientId)
    {
        if (!IsServer || !RevanchaDisponible)
            return;

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex < 0)
            return;

        LobbyPlayerData jugador = players[playerIndex];

        if (jugador.QuiereRevancha)
            return;

        jugador.QuiereRevancha = true;
        players[playerIndex] = jugador;

        Debug.Log(
            $"[Session] {NombreParaMostrar(clientId)} votó la revancha " +
            $"({VotosRevancha}/{VotosParaRevancha})."
        );

        if (RevanchaRules.HayMayoria(VotosRevancha, players.Count))
            TryStartRematch();
    }

    /// <summary>
    /// Vuelve a cargar el mismo mapa sin pasar por el lobby. Es el mismo
    /// camino que TryStartMatch, pero sin volver a pedir modo, equipos y ready.
    /// </summary>
    public bool TryStartRematch()
    {
        if (!IsServer || !RevanchaDisponible ||
            string.IsNullOrEmpty(activeGameplaySceneName))
        {
            return false;
        }

        LimpiarVotosRevancha();

        sessionPhase.Value = SessionPhase.Loading;
        gameplaySceneLoaded = false;
        returnToLobbyRequested = false;

        SceneEventProgressStatus status =
            NetworkManager.SceneManager.LoadScene(
                activeGameplaySceneName,
                LoadSceneMode.Single
            );

        if (status == SceneEventProgressStatus.Started)
            return true;

        Debug.LogError(
            $"No se pudo iniciar la revancha en {activeGameplaySceneName}: {status}."
        );

        sessionPhase.Value = SessionPhase.InMatch;
        return false;
    }

    private void LimpiarVotosRevancha()
    {
        if (!IsServer)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData jugador = players[i];

            if (!jugador.QuiereRevancha)
                continue;

            jugador.QuiereRevancha = false;
            players[i] = jugador;
        }
    }

    /// <summary>
    /// Inicia el modo Zombies (un jugador) desde el menú usando el host local.
    /// Igual que Race, no se expone como opción del lobby multijugador.
    /// </summary>
    public bool TryStartLocalZombies(out string validationError)
    {
        if (!IsSpawned || !IsServer || !NetworkManager.IsHost ||
            sessionPhase.Value != SessionPhase.Lobby ||
            NetworkManager.ConnectedClientsIds.Count != 1 ||
            players.Count != 1 ||
            players[0].ClientId != NetworkManager.LocalClientId)
        {
            validationError =
                "Zombies sólo puede iniciarse desde un host local de un jugador.";
            return false;
        }

        if (zombiesDefinition == null ||
            zombiesDefinition.GameModeId != GameModeId.Zombies)
        {
            validationError =
                "NetworkLobbySession no tiene asignada la definición de Zombies.";
            return false;
        }

        if (!ConfigureSession(zombiesDefinition, 1, out validationError))
            return false;

        if (!TryChangeReady(NetworkManager.LocalClientId, true) ||
            !TryStartMatch())
        {
            PrepareUnconfiguredRoom(out _);
            validationError =
                "No se pudo preparar o cargar la partida de Zombies.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    /// <summary>El host regresa al menú con la misma conexión tras TDM o CTF.</summary>
    public bool TryReturnToLobbyAfterMatch()
    {
        NetworkMatchManager match = GetComponent<NetworkMatchManager>();
        if (!IsServer || !IsSpawned || returnToLobbyRequested ||
            sessionPhase.Value != SessionPhase.InMatch ||
            (selectedGameModeId.Value != GameModeId.TDM &&
             selectedGameModeId.Value != GameModeId.CTF) ||
            match == null || match.Phase != MatchPhase.Finished)
            return false;

        returnToLobbyRequested = true;
        if (!TryReturnToLobby())
        {
            returnToLobbyRequested = false;
            return false;
        }

        gameplaySceneLoaded = false;
        sessionPhase.Value = SessionPhase.Lobby;
        ResetAllPlayersReady();
        // [COG-620] Al volver al lobby se borran los votos de revancha.
        LimpiarVotosRevancha();
        return true;
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

    // ---- [COG-587] Nombres de los jugadores ----

    // Nombres que el servidor ya aprobó en la conexión (LobbyPlayerSpawner)
    // pero que todavía no están en la lista de jugadores. Es static porque la
    // conexión del host se aprueba antes de que exista esta sesión.
    private static readonly Dictionary<ulong, string> nombresAprobados = new();

    public static void GuardarNombreAprobado(ulong clientId, string nombre)
    {
        nombresAprobados[clientId] = NombreJugadorRules.Limpiar(nombre);
    }

    public static void OlvidarNombresAprobados()
    {
        nombresAprobados.Clear();
    }

    /// <summary>¿Alguien de la sala (o que está entrando) ya usa este nombre?</summary>
    public static bool NombreEnUso(string nombre)
    {
        foreach (string usado in nombresAprobados.Values)
        {
            if (NombreJugadorRules.SonIguales(usado, nombre))
                return true;
        }

        NetworkLobbySession sesion = Instance;
        if (sesion == null || !sesion.IsSpawned)
            return false;

        for (int i = 0; i < sesion.players.Count; i++)
        {
            if (NombreJugadorRules.SonIguales(sesion.players[i].Nombre.ToString(), nombre))
                return true;
        }

        return false;
    }

    /// <summary>
    /// El nombre para mostrar en el lobby y en la partida. Si el jugador no
    /// tiene nombre (por ejemplo, en el tutorial), queda "Jugador N" como antes.
    /// </summary>
    public static string NombreParaMostrar(ulong clientId)
    {
        NetworkLobbySession sesion = Instance;

        if (sesion != null && sesion.IsSpawned &&
            sesion.TryGetPlayerData(clientId, out LobbyPlayerData jugador) &&
            jugador.Nombre.Length > 0)
        {
            return jugador.Nombre.ToString();
        }

        return $"Jugador {clientId + 1}";
    }

    /// <summary>
    /// [COG-608] El ping para mostrar en la tabla del marcador. Devuelve "--"
    /// si todavía no se midió (por ejemplo, fuera de una partida en red).
    /// </summary>
    public static string PingParaMostrar(ulong clientId)
    {
        NetworkLobbySession sesion = Instance;

        if (sesion == null || !sesion.IsSpawned ||
            !sesion.TryGetPlayerData(clientId, out LobbyPlayerData jugador))
        {
            return "--";
        }

        return jugador.Ping.ToString();
    }

    // Saca el nombre aprobado de la lista de espera para guardarlo en el jugador.
    private string TomarNombreAprobado(ulong clientId)
    {
        if (nombresAprobados.TryGetValue(clientId, out string nombre))
        {
            nombresAprobados.Remove(clientId);
            return nombre;
        }

        // Por las dudas: el host también puede leer su nombre de su propia conexión.
        if (clientId == NetworkManager.ServerClientId)
            return NombreJugadorRules.DesdeBytes(NetworkManager.NetworkConfig.ConnectionData);

        return string.Empty;
    }

    // ---- [COG-586] Expulsar jugadores ----

    /// <summary>
    /// El host saca a un jugador de la sala. Solo en el lobby y nunca a sí
    /// mismo. Al expulsado le aparece el motivo en la pantalla de Jugar.
    /// </summary>
    public bool ExpulsarJugador(ulong clientId)
    {
        if (!IsServer ||
            sessionPhase.Value != SessionPhase.Lobby ||
            clientId == NetworkManager.ServerClientId ||
            FindPlayerIndex(clientId) < 0)
        {
            return false;
        }

        Debug.Log($"[Session] El host expulsó a {NombreParaMostrar(clientId)}.");
        NetworkManager.DisconnectClient(clientId, "El host te expulsó de la sala.");
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestGameModeChangeRpc(
        GameModeId requestedMode,
        RpcParams rpcParams = default)
    {
        TryChangeGameMode(
            rpcParams.Receive.SenderClientId,
            requestedMode);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayerCountChangeRpc(
        int requestedPlayerCount,
        RpcParams rpcParams = default)
    {
        TryChangePlayerCount(
            rpcParams.Receive.SenderClientId,
            requestedPlayerCount);
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
    private void RequestMapChangeRpc(
        MapId requestedMapId,
        RpcParams rpcParams = default)
    {
        TryChangeMap(rpcParams.Receive.SenderClientId, requestedMapId);
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
            !HasCompleteMatchConfiguration ||
            SelectedDefinition == null || !SelectedDefinition.RequiresTeams)
            return false;

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex < 0 ||
            players[playerIndex].TeamId == requestedTeam ||
            !LobbyValidationRules.CanAssignPlayerToTeam(
                CreatePlayerSnapshot(),
                clientId,
                requestedTeam,
                selectedPlayerCount.Value))
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

        if (isReady)
        {
            if (!HasCompleteMatchConfiguration)
                return false;

            if (SelectedDefinition != null &&
                SelectedDefinition.RequiresTeams &&
                !LobbyValidationRules.IsValidTeam(player.TeamId))
            {
                return false;
            }
        }

        if (player.IsReady == isReady)
            return false;

        player.IsReady = isReady;
        players[playerIndex] = player;
        return true;
    }

    private bool TryChangeMap(ulong clientId, MapId requestedMapId)
    {
        if (!CanHostEditConfiguration(clientId))
        {
            Debug.LogWarning(
                $"El cliente {clientId} intentó cambiar el mapa sin permiso."
            );
            return false;
        }

        if (!MapCatalogRules.TryResolveSelection(
                availableMaps,
                requestedMapId,
                selectedGameModeId.Value,
                out MapDefinition map,
                out string validationError))
        {
            Debug.LogWarning($"No se pudo cambiar el mapa: {validationError}");
            return false;
        }

        if (selectedMapId.Value == map.MapId)
            return true;

        selectedMapId.Value = map.MapId;
        ResetAllPlayersReady();
        Debug.Log(
            $"[Session] El host seleccionó {map.DisplayName} ({map.SceneName})."
        );
        return true;
    }

    private bool TryChangeGameMode(
        ulong clientId,
        GameModeId requestedMode)
    {
        if (!CanHostEditConfiguration(clientId))
        {
            Debug.LogWarning(
                $"El cliente {clientId} intentó cambiar el modo sin permiso."
            );
            return false;
        }

        GameModeDefinition definition = ResolveDefinition(requestedMode);

        if (!LobbyConfigurationRules.IsSelectableMultiplayerMode(definition))
        {
            Debug.LogWarning(
                $"El modo {requestedMode} no está disponible en la sala."
            );
            return false;
        }

        if (!MapCatalogRules.IsCatalogValid(
                availableMaps,
                out string validationError))
        {
            Debug.LogWarning(
                $"No se pudo cambiar el modo: {validationError}"
            );
            return false;
        }

        MapDefinition nextMap = LobbyConfigurationRules.ResolveMapForMode(
            availableMaps,
            selectedMapId.Value,
            requestedMode
        );

        bool changed = selectedGameModeId.Value != requestedMode ||
            selectedMapId.Value != (nextMap != null
                ? nextMap.MapId
                : MapId.None);

        if (selectedPlayerCount.Value > 0 &&
            !definition.AllowsPlayerCount(selectedPlayerCount.Value))
        {
            selectedPlayerCount.Value = 0;
            changed = true;
        }

        selectedGameModeId.Value = requestedMode;
        selectedMapId.Value = nextMap != null
            ? nextMap.MapId
            : MapId.None;

        if (changed)
            ResetAllPlayersReady();

        Debug.Log(
            $"[Session] El host seleccionó el modo {requestedMode}."
        );
        return true;
    }

    private bool TryChangePlayerCount(
        ulong clientId,
        int requestedPlayerCount)
    {
        if (!CanHostEditConfiguration(clientId))
        {
            Debug.LogWarning(
                $"El cliente {clientId} intentó cambiar la cantidad sin permiso."
            );
            return false;
        }

        GameModeDefinition definition = SelectedDefinition;

        if (!LobbyConfigurationRules.CanSelectPlayerCount(
                definition,
                requestedPlayerCount,
                players.Count))
        {
            Debug.LogWarning(
                $"No se puede seleccionar {requestedPlayerCount} jugadores " +
                $"con {players.Count} conectado(s)."
            );
            return false;
        }

        if (selectedPlayerCount.Value == requestedPlayerCount)
            return true;

        selectedPlayerCount.Value = requestedPlayerCount;
        ResetAllPlayersReady();
        Debug.Log(
            $"[Session] El host configuró una partida para " +
            $"{requestedPlayerCount} jugadores."
        );
        return true;
    }

    private bool CanHostEditConfiguration(ulong clientId)
    {
        return NetworkManager != null &&
            LobbyConfigurationRules.CanHostEdit(
                IsServer,
                clientId == NetworkManager.ServerClientId,
                sessionPhase.Value
            );
    }

    private void ApplyPreparedConfiguration()
    {
        selectedGameModeId.Value = preparedDefinition.GameModeId;
        selectedPlayerCount.Value = preparedMatchPlayerCount;
        selectedMapId.Value = preparedMapId;
        sessionPhase.Value = SessionPhase.Lobby;
        ResetAllPlayersReady();
    }

    private void RegisterPlayer(ulong clientId)
    {
        if (!IsServer || FindPlayerIndex(clientId) >= 0)
            return;

        ResetAllPlayersReady();
        bool autoReady = SelectedDefinition != null &&
            !SelectedDefinition.RequiresTeams;
        players.Add(new LobbyPlayerData(
            clientId, TeamId.None, autoReady, TomarNombreAprobado(clientId)));
    }

    private void OnClientConnected(ulong clientId)
    {
        RegisterPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        nombresAprobados.Remove(clientId);

        int playerIndex = FindPlayerIndex(clientId);

        if (playerIndex >= 0)
        {
            // [COG-599] Se guarda quién era antes de sacarlo de la lista.
            LobbyPlayerData jugador = players[playerIndex];
            bool enPartida = sessionPhase.Value == SessionPhase.InMatch;

            players.RemoveAt(playerIndex);
            ResetAllPlayersReady();

            if (enPartida)
            {
                string nombre = jugador.Nombre.Length > 0
                    ? jugador.Nombre.ToString()
                    : $"Jugador {clientId + 1}";
                JugadorAbandonoPartida?.Invoke(clientId, nombre, jugador.TeamId);
            }
        }

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

    private bool TryReturnToLobby()
    {
        if (!IsServer ||
            !returnToLobbyRequested ||
            string.IsNullOrWhiteSpace(lobbySceneName))
        {
            return false;
        }

        if (SceneManager.GetActiveScene().name == lobbySceneName)
        {
            returnToLobbyRequested = false;
            activeGameplaySceneName = string.Empty;
            return true;
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
                $"No se pudo regresar al lobby: {status}."
            );
            return false;
        }

        return true;
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
                GetComponent<NetworkMatchManager>()?.TryResetForRematch();
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
        selectedPlayerCount.Value = 0;
        selectedMapId.Value = MapId.None;
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
            GameModeId.Zombies => zombiesDefinition,
            _ => null
        };
    }

    private bool HasValidSceneSelection(
        GameModeDefinition definition,
        out string validationError)
    {
        if (selectedMapId.Value == MapId.None)
        {
            if (!string.IsNullOrWhiteSpace(definition.SceneName))
            {
                validationError = string.Empty;
                return true;
            }

            validationError = "La sesión no tiene una escena seleccionada.";
            return false;
        }

        return MapCatalogRules.TryResolveSelection(
            availableMaps,
            selectedMapId.Value,
            definition.GameModeId,
            out _,
            out validationError
        );
    }

    private void OnPlayersChanged(
        NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        PlayersChanged?.Invoke(changeEvent);

        // [COG-607][COG-620] El ping y el voto de revancha cambian durante la
        // partida, pero no son configuración del lobby. Si se avisaran como
        // tal, NetworkMatchManager reiniciaría sola la partida terminada.
        if (changeEvent.Type == NetworkListEvent<LobbyPlayerData>.EventType.Value &&
            LobbyPlayerData.SoloCambioPingOVoto(
                changeEvent.PreviousValue, changeEvent.Value))
        {
            return;
        }

        LobbyStateChanged?.Invoke();
    }

    private void OnGameModeChanged(GameModeId previous, GameModeId current)
    {
        LobbyStateChanged?.Invoke();
    }

    private void OnSelectedPlayerCountChanged(int previous, int current)
    {
        LobbyStateChanged?.Invoke();
    }

    private void OnSelectedMapChanged(MapId previous, MapId current)
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