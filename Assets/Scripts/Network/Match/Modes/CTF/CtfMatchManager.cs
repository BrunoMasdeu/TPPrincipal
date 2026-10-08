using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Estado y decisiones de CTF en el servidor. Usa el único reloj y ciclo de
/// fases de NetworkMatchManager; los clientes sólo reciben vistas del estado.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkLobbySession))]
[RequireComponent(typeof(NetworkMatchManager))]
public class CtfMatchManager : NetworkBehaviour
{
    public static CtfMatchManager Instance { get; private set; }

    private readonly NetworkVariable<int> redCaptures = new(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> blueCaptures = new(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<CtfFlagSnapshot> redFlag = new(
        default, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<CtfFlagSnapshot> blueFlag = new(
        default, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> baseZonesReady = new(
        false, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly HashSet<ulong> insideRedBase = new();
    private readonly HashSet<ulong> insideBlueBase = new();
    private NetworkLobbySession lobby;
    private NetworkMatchManager commonMatch;
    private CtfMatchState serverState;
    private CtfBaseZone redBase;
    private CtfBaseZone blueBase;

    public event Action ScoreChanged;
    public event Action FlagsChanged;

    public bool IsCtf => lobby != null &&
        lobby.SelectedGameModeId == GameModeId.CTF;
    public int RedCaptures => redCaptures.Value;
    public int BlueCaptures => blueCaptures.Value;
    public CtfFlagSnapshot RedFlag => redFlag.Value;
    public CtfFlagSnapshot BlueFlag => blueFlag.Value;
    public bool HasBaseZones => baseZonesReady.Value;

    public bool TryGetCaptureLimit(out int limit)
    {
        if (IsCtf)
            return CtfRules.TryGetCaptureLimit(lobby.RequiredPlayerCount,
                out limit);

        limit = 0;
        return false;
    }

    public float GetReturnRemaining(TeamId flagTeam)
    {
        CtfFlagSnapshot flag = flagTeam == TeamId.Red
            ? redFlag.Value : blueFlag.Value;
        return flag.State == FlagState.Dropped && NetworkManager != null
            ? Mathf.Max(0f, (float)(flag.ReturnAt - NetworkManager.ServerTime.Time))
            : 0f;
    }

    private void Awake()
    {
        lobby = GetComponent<NetworkLobbySession>();
        commonMatch = GetComponent<NetworkMatchManager>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        redCaptures.OnValueChanged += OnScoreChanged;
        blueCaptures.OnValueChanged += OnScoreChanged;
        redFlag.OnValueChanged += OnFlagChanged;
        blueFlag.OnValueChanged += OnFlagChanged;
        if (!IsServer || commonMatch == null)
            return;

        commonMatch.PhaseChanged += OnPhaseChanged;
        commonMatch.ServerDeathConfirmed += OnServerDeathConfirmed;
        commonMatch.TimeExpired += OnTimeExpired;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        OnPhaseChanged(commonMatch.Phase);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;

        redCaptures.OnValueChanged -= OnScoreChanged;
        blueCaptures.OnValueChanged -= OnScoreChanged;
        redFlag.OnValueChanged -= OnFlagChanged;
        blueFlag.OnValueChanged -= OnFlagChanged;

        if (commonMatch != null)
        {
            commonMatch.PhaseChanged -= OnPhaseChanged;
            commonMatch.ServerDeathConfirmed -= OnServerDeathConfirmed;
            commonMatch.TimeExpired -= OnTimeExpired;
        }
        if (NetworkManager != null && IsServer)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        ClearSceneState();
        ScoreChanged = null;
        FlagsChanged = null;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || !IsCtf || serverState == null ||
            commonMatch.Phase != MatchPhase.Playing)
            return;

        double now = NetworkManager.ServerTime.Time;
        if (CtfRules.TryReturnExpiredFlags(serverState, now,
                commonMatch.Phase, out CtfMatchState returned))
            SetState(returned);

        foreach (LobbyPlayerData player in lobby.Players)
        {
            if (!TryGetAlivePlayer(player.ClientId, out TeamId team,
                    out NetworkObject playerObject))
            {
                insideRedBase.Remove(player.ClientId);
                insideBlueBase.Remove(player.ClientId);
                continue;
            }

            Collider playerCollider = playerObject.GetComponent<Collider>();
            CheckBaseEntry(redBase, insideRedBase, player.ClientId,
                team, playerCollider, now);
            CheckBaseEntry(blueBase, insideBlueBase, player.ClientId,
                team, playerCollider, now);

            if (commonMatch.Phase != MatchPhase.Playing)
                return;

            CheckDroppedFlag(TeamId.Red, player.ClientId, team,
                playerObject.transform.position, now);
            CheckDroppedFlag(TeamId.Blue, player.ClientId, team,
                playerObject.transform.position, now);
        }
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        if (!IsServer)
            return;

        if (phase == MatchPhase.WaitingForPlayers ||
            phase == MatchPhase.Countdown)
        {
            ClearSceneState();
            baseZonesReady.Value = false;
            redCaptures.Value = 0;
            blueCaptures.Value = 0;
            redFlag.Value = new CtfFlagSnapshot(CtfFlagData.AtBase());
            blueFlag.Value = new CtfFlagSnapshot(CtfFlagData.AtBase());
            return;
        }

        if (phase != MatchPhase.Playing || !IsCtf || serverState != null)
            return;

        serverState = new CtfMatchState();
        DiscoverBaseZones();
        PublishState();
        Debug.Log("[CTF] Estado iniciado: ambas banderas en sus bases.");
    }

    private void DiscoverBaseZones()
    {
        redBase = null;
        blueBase = null;
        CtfBaseZone[] zones = FindObjectsByType<CtfBaseZone>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CtfBaseZone zone in zones)
        {
            if (!zone.IsConfigured)
            {
                Debug.LogWarning($"[CTF] Zona {zone.name} sin equipo válido o Box Collider Trigger.");
                continue;
            }

            if (zone.TeamId == TeamId.Red)
                redBase = zone;
            else
                blueBase = zone;
        }

        baseZonesReady.Value = redBase != null && blueBase != null;
        if (!HasBaseZones)
            Debug.LogError("[CTF] Faltan Zone_Red/Zone_Blue con CtfBaseZone y Box Collider Trigger.");
    }

    private bool TryGetAlivePlayer(ulong clientId, out TeamId team,
        out NetworkObject playerObject)
    {
        team = TeamId.None;
        playerObject = null;
        if (!lobby.TryGetPlayerData(clientId, out LobbyPlayerData player) ||
            (player.TeamId != TeamId.Red && player.TeamId != TeamId.Blue) ||
            !NetworkManager.ConnectedClients.TryGetValue(clientId,
                out NetworkClient client) || client.PlayerObject == null)
            return false;

        NetworkPlayerHealth health =
            client.PlayerObject.GetComponent<NetworkPlayerHealth>();
        if (health == null || !health.IsSpawned ||
            health.LifeState != PlayerLifeState.Alive)
            return false;

        team = player.TeamId;
        playerObject = client.PlayerObject;
        return true;
    }

    private void CheckBaseEntry(CtfBaseZone zone, HashSet<ulong> inside,
        ulong playerId, TeamId playerTeam, Collider playerCollider, double now)
    {
        bool isInside = zone != null && zone.Contains(playerCollider);
        if (!isInside)
        {
            inside.Remove(playerId);
            return;
        }

        // Sólo el cruce de fuera hacia dentro cuenta como interacción.
        if (!inside.Add(playerId))
            return;

        CtfMatchState next;
        if (playerTeam == zone.TeamId)
        {
            if (!CtfRules.TryCapture(serverState, zone.TeamId, playerTeam,
                    playerId, commonMatch.Phase, out next))
                return;

            SetState(next);
            Debug.Log($"[CTF] {playerId} capturó para {playerTeam}: " +
                $"Rojo {RedCaptures} - Azul {BlueCaptures}.");
            if (CtfRules.TryResolveCaptureLimit(serverState,
                    lobby.RequiredPlayerCount, commonMatch.Phase,
                    out MatchResultData result))
                commonMatch.TryFinishMatch(result);
        }
        else
        {
            // La base no recoge una bandera que está caída en otro lugar.
            if (serverState.GetFlag(zone.TeamId).State != FlagState.AtBase ||
                !CtfRules.TryPickUpFlag(serverState, zone.TeamId,
                    playerTeam, playerId, commonMatch.Phase, now, out next))
                return;

            SetState(next);
            Debug.Log($"[CTF] {playerId} tomó la bandera {zone.TeamId}.");
        }
    }

    private void CheckDroppedFlag(TeamId flagTeam, ulong playerId,
        TeamId playerTeam, Vector3 playerPosition, double now)
    {
        CtfFlagData flag = serverState.GetFlag(flagTeam);
        if (flag.State != FlagState.Dropped || now >= flag.ReturnAt ||
            (playerPosition - flag.DropPosition).sqrMagnitude >
                CtfRules.DroppedFlagPickupRadius *
                CtfRules.DroppedFlagPickupRadius)
            return;

        CtfMatchState next;
        bool changed = playerTeam == flagTeam
            ? CtfRules.TryReturnOwnDroppedFlag(serverState, flagTeam,
                playerTeam, commonMatch.Phase, out next)
            : CtfRules.TryPickUpFlag(serverState, flagTeam, playerTeam,
                playerId, commonMatch.Phase, now, out next);
        if (!changed)
            return;

        SetState(next);
        Debug.Log(playerTeam == flagTeam
            ? $"[CTF] {playerId} devolvió su bandera {flagTeam}."
            : $"[CTF] {playerId} recuperó la bandera {flagTeam} caída.");
    }

    private void OnServerDeathConfirmed(PlayerDeathInfo death)
    {
        if (!IsServer || !IsCtf || serverState == null ||
            commonMatch.Phase != MatchPhase.Playing ||
            !NetworkManager.ConnectedClients.TryGetValue(death.VictimClientId,
                out NetworkClient victim) || victim.PlayerObject == null ||
            !CtfRules.TryDropCarriedFlag(serverState, death,
                victim.PlayerObject.transform.position,
                NetworkManager.ServerTime.Time, commonMatch.Phase,
                out CtfMatchState next))
            return;

        SetState(next);
        Debug.Log($"[CTF] Muerte #{death.EventId}: bandera caída en " +
            $"{next.GetFlag(death.VictimTeamId == TeamId.Red ? TeamId.Blue : TeamId.Red).DropPosition}.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        insideRedBase.Remove(clientId);
        insideBlueBase.Remove(clientId);
        if (!IsServer || !IsCtf || serverState == null ||
            !CtfRules.TryReturnDisconnectedCarrier(serverState, clientId,
                commonMatch.Phase, out CtfMatchState next))
            return;

        SetState(next);
        Debug.Log($"[CTF] Portador {clientId} desconectado: bandera devuelta a su base.");
    }

    private void SetState(CtfMatchState next)
    {
        serverState = next;
        PublishState();
    }

    private void PublishState()
    {
        redCaptures.Value = serverState.RedCaptures;
        blueCaptures.Value = serverState.BlueCaptures;
        redFlag.Value = new CtfFlagSnapshot(serverState.RedFlag);
        blueFlag.Value = new CtfFlagSnapshot(serverState.BlueFlag);
    }

    private void ClearSceneState()
    {
        serverState = null;
        redBase = null;
        blueBase = null;
        insideRedBase.Clear();
        insideBlueBase.Clear();
    }

    private void OnScoreChanged(int previous, int current) =>
        ScoreChanged?.Invoke();
    private void OnFlagChanged(CtfFlagSnapshot previous,
        CtfFlagSnapshot current) => FlagsChanged?.Invoke();

    private void OnTimeExpired()
    {
        if (!IsServer || !IsCtf || serverState == null ||
            !CtfRules.TryResolveTimeExpired(serverState, commonMatch.Phase,
                out MatchResultData result))
            return;

        commonMatch.TryFinishMatch(result);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }
}
