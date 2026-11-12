using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>Instancia local, ligada al PlayerObject propietario y a la raíz de sesión.</summary>
[RequireComponent(typeof(MatchHudView))]
public sealed class MatchHudPresenter : MonoBehaviour
{
    private struct DeathNotice
    {
        public string text;
        public float expiresAt;
    }

    private MatchHudView view;
    private PlayerNetworkSetup player;
    private NetworkPlayerCombat combat;
    private NetworkPlayerHealth health;
    private NetworkLobbySession lobby;
    private NetworkMatchManager match;
    private TdmMatchManager tdm;
    private CtfMatchManager ctf;
    private readonly List<PlayerMatchStats> players = new();
    private readonly List<DeathNotice> deaths = new();
    private ulong lastDeathEventId;
    private bool resultsOpen;
    private bool quitPanelOpen = false;
    private bool visible = true;
    private float nextRefresh;

    public bool IsLeaving { get; private set; }
    public MatchHudView View => view;

    private void Awake()
    {
        view = GetComponent<MatchHudView>();
        view.SetTableVisible(false);
        view.SetResult(false, default, false);
        view.SetDeathFeed(string.Empty);
    }

    public void Initialize(PlayerNetworkSetup localPlayer, NetworkPlayerCombat localCombat,
        NetworkPlayerHealth localHealth, NetworkLobbySession session, NetworkMatchManager activeMatch)
    {
        Detach();
        player = localPlayer;
        combat = localCombat;
        health = localHealth;
        lobby = session;
        match = activeMatch;
        tdm = session.GetComponent<TdmMatchManager>();
        ctf = session.GetComponent<CtfMatchManager>();
        lobby.LobbyStateChanged += RefreshConfiguration;
        // [COG-608] La lista de jugadores cambia cuando el servidor actualiza
        // el ping, así que la tabla se refresca sola.
        lobby.PlayersChanged += OnLobbyPlayersChanged;
        match.PhaseChanged += OnPhaseChanged;
        match.MatchFinished += OnMatchFinished;
        match.DeathAnnounced += OnDeath;
        match.PlayerLeft += OnPlayerLeft;
        combat.MoneyChanged += OnMoneyChanged;
        if (tdm != null)
        {
            tdm.ScoreChanged += RefreshScores;
            tdm.PlayerStatsChanged += RefreshPlayers;
        }
        if (ctf != null)
        {
            ctf.ScoreChanged += RefreshScores;
            ctf.PlayerStatsChanged += RefreshPlayers;
        }
        if (EventSystem.current == null)
        {
            var events = new GameObject("MatchEventSystem", typeof(EventSystem));
            events.transform.SetParent(transform, false);
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        // [COG-620] El botón de revancha se conecta por código, no por el Inspector.
        view.BindRematch(OnRematchClicked);
        RefreshConfiguration();
        OnMoneyChanged(combat.Money);
        OnPhaseChanged(match.Phase);
    }

    public void SetVisible(bool value)
    {
        visible = value;
        view.SetVisible(value);
        if (!value)
            view.SetTableVisible(false);
    }

    private void Update()
    {
        if (IsLeaving || match == null || !match.IsSpawned)
            return;
        bool finished = match.Phase == MatchPhase.Finished;

        // [COG-599] Con la partida terminada el cursor tiene que quedar libre
        // para poder usar los botones. Si algo lo volvió a bloquear (la tienda
        // al cerrarse, una cámara que se prende), se corrige acá.
        if (finished && Cursor.lockState != CursorLockMode.None)
        {
            player.SetPresentationControlsEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // [TECLAS] La tecla del marcador sale de Controles.
        view.SetTableVisible(visible && !finished && !MatchDebugConsole.IsInputCaptured &&
            !NetworkPlayerShop.IsInputCaptured && Keyboard.current != null &&
            Keyboard.current[Controles.Tecla(Accion.Marcador)].isPressed);
        if(visible && !finished && !MatchDebugConsole.IsInputCaptured &&
            !NetworkPlayerShop.IsInputCaptured && !OpcionesEnPausa.Ocupado && // [OPCIONES] Esc cierra Opciones primero
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleQuitPanel(!quitPanelOpen);

        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + .1f;
        RefreshClock();
        if (deaths.RemoveAll(d => d.expiresAt <= Time.unscaledTime) > 0)
            RefreshDeathFeed();
        if (ctf != null && lobby.SelectedGameModeId == GameModeId.CTF)
            view.SetFlags($"Bandera roja: {FlagLabel(ctf.RedFlag.State)} | Bandera azul: {FlagLabel(ctf.BlueFlag.State)}");
    }

    private void RefreshConfiguration()
    {
        view.SetMode(lobby.SelectedGameModeId);
        RefreshScores();
        RefreshPlayers();
    }

    private void RefreshScores()
    {
        bool isCtf = lobby.SelectedGameModeId == GameModeId.CTF;
        int limit = 0;
        if (isCtf && ctf != null)
            ctf.TryGetCaptureLimit(out limit);
        else if (tdm != null)
            tdm.TryGetOfficialScoreLimit(out limit);
        view.SetScores(isCtf ? ctf?.RedCaptures ?? 0 : tdm?.RedEliminations ?? 0,
            isCtf ? ctf?.BlueCaptures ?? 0 : tdm?.BlueEliminations ?? 0, limit, lobby.SelectedGameModeId);
    }

    private void RefreshPlayers()
    {
        players.Clear();
        bool isCtf = lobby.SelectedGameModeId == GameModeId.CTF;
        bool hasStats = isCtf ? ctf != null && ctf.PlayerStatsCount > 0
            : tdm != null && tdm.PlayerStatsCount > 0;
        if (hasStats && isCtf)
        {
            for (int i = 0; i < ctf.PlayerStatsCount; i++)
                if (ctf.TryGetPlayerStats(i, out PlayerMatchStats stats))
                    players.Add(stats);
        }
        else if (hasStats)
        {
            for (int i = 0; i < tdm.PlayerStatsCount; i++)
                if (tdm.TryGetPlayerStats(i, out PlayerMatchStats stats))
                    players.Add(stats);
        }
        else
            foreach (LobbyPlayerData data in lobby.Players)
                players.Add(new PlayerMatchStats(data.ClientId, data.TeamId));
        players.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
        view.SetPlayers(players, player.OwnerClientId, hasStats);
    }

    // [COG-608] Se vuelve a dibujar la tabla cuando cambia algo de un jugador
    // (el ping, el nombre, el voto de revancha, o si alguien se fue).
    private void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> _)
    {
        RefreshPlayers();
        RefreshRematch();
    }

    // [COG-620] Pide la revancha en nombre de este jugador.
    private void OnRematchClicked()
    {
        if (lobby != null)
            lobby.PedirRevancha();
    }

    // [COG-620] Actualiza el botón y el contador de votos.
    private void RefreshRematch()
    {
        if (lobby == null || player == null)
            return;

        bool disponible = lobby.RevanchaDisponible;
        view.SetRematch(
            disponible,
            disponible && lobby.YaVotoRevancha(player.OwnerClientId),
            disponible ? lobby.VotosRevancha : 0,
            disponible ? lobby.VotosParaRevancha : 0);
    }

    private void RefreshClock()
    {
        view.SetTime(match.Phase == MatchPhase.Countdown ? match.CountdownRemaining : match.TimeRemaining);
        string phase = match.Phase == MatchPhase.Countdown ? "La partida comienza en..."
            : match.Phase == MatchPhase.WaitingForPlayers ? "Esperando jugadores..."
            : match.Phase == MatchPhase.Finished ? "Partida finalizada" : string.Empty;
        if (match.Phase == MatchPhase.Playing && health.LifeState != PlayerLifeState.Alive)
            phase = $"Reaparecés en {Mathf.CeilToInt(health.RespawnRemaining)} s";
        view.SetPhase(phase);
    }

    private void OnMoneyChanged(int money) => view.SetMoney(money);
    private void OnMatchFinished(MatchResultData _) => OnPhaseChanged(match.Phase);
    private void OnPhaseChanged(MatchPhase phase)
    {
        bool finished = phase == MatchPhase.Finished;
        view.SetResult(finished, match.Result, player.IsHost);
        RefreshScores();
        RefreshClock();
        RefreshPlayers();
        RefreshRematch();
        if (finished == resultsOpen)
            return;
        resultsOpen = finished;
        player.SetPresentationControlsEnabled(!finished);
        Cursor.lockState = finished ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = finished;
    }

    private void OnDeath(PlayerDeathInfo death)
    {
        if (death.EventId == 0 || death.EventId <= lastDeathEventId)
            return;
        lastDeathEventId = death.EventId;
        // [COG-587] Nombres de los jugadores en el aviso de bajas.
        string victim = NetworkLobbySession.NombreParaMostrar(death.VictimClientId);
        string message = death.HasAttacker && death.AttackerClientId != death.VictimClientId
            ? $"{NetworkLobbySession.NombreParaMostrar(death.AttackerClientId)} eliminó a {victim}"
            : $"{victim} murió";
        deaths.Add(new DeathNotice { text = message, expiresAt = Time.unscaledTime + 5f });
        if (deaths.Count > 3)
            deaths.RemoveAt(0);
        RefreshDeathFeed();
    }

    // [COG-599] El aviso de abandono sale en el mismo lugar que las bajas.
    private void OnPlayerLeft(string nombre)
    {
        deaths.Add(new DeathNotice { text = $"{nombre} abandonó la partida", expiresAt = Time.unscaledTime + 5f });
        if (deaths.Count > 3)
            deaths.RemoveAt(0);
        RefreshDeathFeed();
    }

    private void RefreshDeathFeed() => view.SetDeathFeed(string.Join("\n", deaths.ConvertAll(d => d.text)));
    private static string FlagLabel(FlagState state) => state == FlagState.AtBase ? "en base"
        : state == FlagState.Carried ? "transportada" : "caída";

    public void ReturnToLobby()
    {
        if (!IsLeaving && lobby != null && player != null && player.IsHost)
            lobby.TryReturnToLobbyAfterMatch();
    }

    public void LeaveToMenu()
    {
        if (IsLeaving || match == null)
            return;
        IsLeaving = true;
        view.SetLeaving();
        // La salida debe completar aun cuando Shutdown destruya el PlayerObject.
        DontDestroyOnLoad(gameObject);
        Detach();
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        NetworkManager network = NetworkManager.Singleton;
        network?.Shutdown();
        while (network != null && network.ShutdownInProgress)
            yield return null;
        if (network != null)
        {
            Destroy(network.gameObject);
            yield return null;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return SceneManager.LoadSceneAsync("MenuScene");
        Destroy(gameObject);
    }

    public void Detach()
    {
        if (lobby != null)
        {
            lobby.LobbyStateChanged -= RefreshConfiguration;
            lobby.PlayersChanged -= OnLobbyPlayersChanged;
        }
        if (match != null)
        {
            match.PhaseChanged -= OnPhaseChanged;
            match.MatchFinished -= OnMatchFinished;
            match.DeathAnnounced -= OnDeath;
            match.PlayerLeft -= OnPlayerLeft;
        }
        if (combat != null) combat.MoneyChanged -= OnMoneyChanged;
        if (tdm != null)
        {
            tdm.ScoreChanged -= RefreshScores;
            tdm.PlayerStatsChanged -= RefreshPlayers;
        }
        if (ctf != null)
        {
            ctf.ScoreChanged -= RefreshScores;
            ctf.PlayerStatsChanged -= RefreshPlayers;
        }
        player = null;
        combat = null;
        health = null;
        lobby = null;
        match = null;
        tdm = null;
        ctf = null;
    }
    public void ToggleQuitPanel(bool isQuitOpen)
    {
        view.SetQuitPanel(isQuitOpen);
        if (!isQuitOpen)
            OpcionesEnPausa.Cerrar(); // [OPCIONES]
        if (isQuitOpen == quitPanelOpen)
            return;
        quitPanelOpen = isQuitOpen;
        player.SetPresentationControlsEnabled(!isQuitOpen);
        Cursor.lockState = isQuitOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isQuitOpen;
    }

    private void OnDestroy() => Detach();
}
