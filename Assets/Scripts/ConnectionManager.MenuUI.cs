using System;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public partial class ConnectionManager
{
    [Serializable]
    private struct PlayerCountControl
    {
        public int playerCount;
        public Button button;
    }

    [Header("MenuCOG")]
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private TMP_Text connectionStatusDisplay;
    [SerializeField] private Button tdmButton;
    [SerializeField] private Button ctfButton;
    [SerializeField] private PlayerCountControl[] playerCountControls = Array.Empty<PlayerCountControl>();
    [SerializeField] private TMP_Text configurationDisplay;
    [SerializeField] private Button nextMapButton;
    [SerializeField] private TMP_Text mapNameDisplay;
    [SerializeField] private TMP_Text mapDescriptionDisplay;
    [SerializeField] private Image mapPreview;
    [SerializeField] private Button redTeamButton;
    [SerializeField] private Button blueTeamButton;
    [SerializeField] private TMP_Text redTeamDisplay;
    [SerializeField] private TMP_Text blueTeamDisplay;
    [SerializeField] private TMP_Text unassignedPlayersDisplay;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text lobbyHintDisplay;

    public void SelectNextMap()
    {
        if (lobbySession == null || !lobbySession.IsSpawned ||
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost ||
            lobbySession.Phase != SessionPhase.Lobby)
            return;

        var maps = lobbySession.AvailableMaps;
        int current = -1;
        for (int i = 0; i < maps.Count; i++)
            if (maps[i] != null && maps[i].MapId == lobbySession.SelectedMapId)
                current = i;

        for (int step = 1; step <= maps.Count; step++)
        {
            MapDefinition map = maps[(current + step) % maps.Count];
            if (MapCatalogRules.IsSelectableForMode(map, lobbySession.SelectedGameModeId))
            {
                SelectMap(map.MapId);
                return;
            }
        }
    }

    private void RefreshMenuUI()
    {
        NetworkManager manager = NetworkManager.Singleton;
        bool listening = manager != null && manager.IsListening;
        bool canConnect = !isConnecting && !isLocalGameStarting && !listening;
        SetInteractable(hostButton, canConnect);
        SetInteractable(joinButton, canConnect);
        SetInteractable(singlePlayerButton, canConnect);
        if (joinCodeInput != null)
            joinCodeInput.interactable = canConnect;

        bool hasSession = lobbySession != null && lobbySession.IsSpawned;
        bool inLobby = hasSession && lobbySession.Phase == SessionPhase.Lobby;
        bool hostCanConfigure = inLobby && manager != null && manager.IsHost;
        GameModeDefinition definition = hasSession ? lobbySession.SelectedDefinition : null;
        SetInteractable(tdmButton, hostCanConfigure);
        SetInteractable(ctfButton, hostCanConfigure);
        SetButtonLabel(tdmButton, hasSession && lobbySession.SelectedGameModeId == GameModeId.TDM ? "[TDM]" : "TDM");
        SetButtonLabel(ctfButton, hasSession && lobbySession.SelectedGameModeId == GameModeId.CTF ? "[CTF]" : "CTF");

        foreach (PlayerCountControl control in playerCountControls)
        {
            SetInteractable(control.button, hostCanConfigure && definition != null &&
                definition.AllowsPlayerCount(control.playerCount) &&
                MultiplayerSessionLimits.CanSelectMatchPlayerCount(control.playerCount, lobbySession.Players.Count));
            SetButtonLabel(control.button, hasSession && lobbySession.SelectedPlayerCount == control.playerCount
                ? $"[{control.playerCount}]" : control.playerCount.ToString());
        }

        if (configurationDisplay != null)
        {
            string mode = definition != null ? definition.GameModeId.ToString() : "Sin elegir";
            string count = hasSession && lobbySession.SelectedPlayerCount > 0
                ? lobbySession.SelectedPlayerCount.ToString() : "Sin elegir";
            configurationDisplay.text = $"Modo: {mode}\nJugadores: {count}\n" +
                (hostCanConfigure ? "Configurá la partida:" : "El host configura la partida");
        }

        MapDefinition map = hasSession ? lobbySession.SelectedMapDefinition : null;
        SetInteractable(nextMapButton, hostCanConfigure && definition != null);
        if (mapNameDisplay != null)
            mapNameDisplay.text = map != null ? map.DisplayName : "Elegí un modo";
        if (mapDescriptionDisplay != null)
            mapDescriptionDisplay.text = map == null ? "El host selecciona el mapa para la partida."
                : map.MapId == MapId.ArenaIndustrial
                    ? "Combate en un complejo industrial con vagones, edificaciones y múltiples rutas."
                    : "Escenario de prueba de combate por equipos.";
        if (mapPreview != null)
            mapPreview.enabled = map != null && map.MapId == MapId.ArenaIndustrial;

        bool complete = inLobby && lobbySession.HasCompleteMatchConfiguration;
        LobbyPlayerData local = default;
        bool hasLocalPlayer = hasSession && manager != null &&
            lobbySession.TryGetPlayerData(manager.LocalClientId, out local);
        bool needsTeam = definition != null && definition.RequiresTeams;
        SetInteractable(redTeamButton, complete && hasLocalPlayer && needsTeam);
        SetInteractable(blueTeamButton, complete && hasLocalPlayer && needsTeam);
        SetInteractable(readyButton, complete && hasLocalPlayer &&
            (!needsTeam || local.TeamId != TeamId.None));
        SetButtonLabel(readyButton, hasLocalPlayer && local.IsReady ? "QUITAR LISTO" : "ESTOY LISTO");
        SetButtonLabel(redTeamButton, hasLocalPlayer && local.TeamId == TeamId.Red ? "ROJO (TU EQUIPO)" : "ELEGIR ROJO");
        SetButtonLabel(blueTeamButton, hasLocalPlayer && local.TeamId == TeamId.Blue ? "AZUL (TU EQUIPO)" : "ELEGIR AZUL");
        SetInteractable(startGameButton, hostCanConfigure && lobbySession.CanStartMatch());

        if (redTeamDisplay != null)
            redTeamDisplay.text = BuildRoster(TeamId.Red);
        if (blueTeamDisplay != null)
            blueTeamDisplay.text = BuildRoster(TeamId.Blue);
        if (unassignedPlayersDisplay != null)
            unassignedPlayersDisplay.text = "SIN EQUIPO\n" + BuildRoster(TeamId.None);
        if (lobbyHintDisplay != null)
        {
            lobbyHintDisplay.text = !hasSession ? "Conectando con la sala..."
                : !inLobby ? "Cargando partida..."
                : !complete ? "El host debe elegir modo, mapa y cantidad de jugadores."
                : lobbySession.Players.Count != lobbySession.SelectedPlayerCount
                    ? $"Se necesitan {lobbySession.SelectedPlayerCount} jugadores conectados."
                    : "Elegí tu equipo y marcá ESTOY LISTO. El host inicia la partida.";
        }
    }

    private string BuildRoster(TeamId team)
    {
        if (lobbySession == null || !lobbySession.IsSpawned)
            return "Sin jugadores";

        var text = new StringBuilder();
        for (int i = 0; i < lobbySession.Players.Count; i++)
        {
            LobbyPlayerData player = lobbySession.Players[i];
            if (player.TeamId != team)
                continue;
            bool local = NetworkManager.Singleton != null && player.ClientId == NetworkManager.Singleton.LocalClientId;
            if (text.Length > 0)
                text.Append(team == TeamId.None ? "  |  " : "\n");
            text.Append($"Jugador {player.ClientId + 1}{(local ? " (vos)" : "")}");
            if (team != TeamId.None)
            {
                text.Append('\n');
                text.Append(player.IsReady ? "LISTO" : "Esperando");
            }
        }
        return text.Length > 0 ? text.ToString() : "Sin jugadores";
    }

    private static void SetInteractable(Button button, bool value)
    {
        if (button != null)
            button.interactable = value;
    }

    private static string TranslateStatus(string message) => message switch
    {
        "NETWORK MANAGER NOT FOUND" => "No se encontró el administrador de red",
        "CONNECTED TO ROOM" => "Conectado a la sala",
        "SERVICES ERROR" => "No se pudo conectar con Unity Services",
        "CREATING ROOM..." => "Creando sala...",
        "HOST COULD NOT START" => "No se pudo iniciar la sala",
        "CONFIGURE MATCH" => "Configurá la partida",
        "ERROR CREATING ROOM" => "Error al crear la sala",
        "LOCAL GAME CONFIGURATION ERROR" => "No se pudo preparar el entrenamiento",
        "UNITY TRANSPORT NOT FOUND" => "No se encontró el transporte de red",
        "LOCAL HOST COULD NOT START" => "No se pudo iniciar el entrenamiento",
        "LOCAL GAME NOT READY" => "El entrenamiento no está disponible",
        "ENTER A JOIN CODE" => "Ingresá el código de la sala",
        "JOINING ROOM..." => "Buscando sala...",
        "CONNECTING..." => "Conectando...",
        "CLIENT COULD NOT START" => "No se pudo iniciar la conexión",
        "COULD NOT JOIN ROOM" => "No se pudo entrar. Revisá el código e intentá otra vez",
        "SESSION NOT AVAILABLE" => "La sesión no está disponible",
        "GAME MODE NOT AVAILABLE" => "Elegí un modo de juego",
        "LOADING MATCH..." => "Cargando partida...",
        "PLAYERS MUST CHOOSE TEAM AND READY" => "Todos deben elegir equipo y estar listos",
        "PLAYER CONNECTED" => "Jugador conectado",
        "WAITING FOR PLAYERS..." => "Esperando jugadores...",
        "DISCONNECTED" => "Desconectado de la sala",
        _ => message
    };

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
            return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = value;
    }
}
