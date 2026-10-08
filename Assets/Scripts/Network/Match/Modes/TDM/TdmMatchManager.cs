using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Adaptador de las reglas TDM al servidor. El reloj y las fases siguen siendo
/// responsabilidad del único NetworkMatchManager de la raíz de sesión.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkLobbySession))]
[RequireComponent(typeof(NetworkMatchManager))]
public class TdmMatchManager : NetworkBehaviour
{
    public static TdmMatchManager Instance { get; private set; }

    private readonly NetworkVariable<int> redEliminations = new(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> blueEliminations = new(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkList<PlayerMatchStats> playerStats;
    private NetworkLobbySession lobby;
    private NetworkMatchManager commonMatch;
    private TdmMatchState serverState;

    public event Action ScoreChanged;
    public event Action PlayerStatsChanged;

    public int RedEliminations => redEliminations.Value;
    public int BlueEliminations => blueEliminations.Value;
    public int PlayerStatsCount => playerStats?.Count ?? 0;
    public bool IsTdm => lobby != null && lobby.SelectedGameModeId == GameModeId.TDM;

    public bool TryGetOfficialScoreLimit(out int limit)
    {
        if (IsTdm)
            return TdmRules.TryGetOfficialEliminationLimit(
                lobby.RequiredPlayerCount, out limit);

        limit = 0;
        return false;
    }

    public bool TryGetPlayerStats(int index, out PlayerMatchStats stats)
    {
        if (playerStats != null && index >= 0 && index < playerStats.Count)
        {
            stats = playerStats[index];
            return true;
        }

        stats = default;
        return false;
    }

    private void Awake()
    {
        lobby = GetComponent<NetworkLobbySession>();
        commonMatch = GetComponent<NetworkMatchManager>();
        playerStats = new NetworkList<PlayerMatchStats>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        redEliminations.OnValueChanged += OnRedScoreChanged;
        blueEliminations.OnValueChanged += OnBlueScoreChanged;
        playerStats.OnListChanged += OnPlayerStatsChanged;

        if (!IsServer || commonMatch == null)
            return;

        commonMatch.PhaseChanged += OnPhaseChanged;
        commonMatch.ServerDeathConfirmed += OnServerDeathConfirmed;
        commonMatch.TimeExpired += OnTimeExpired;
        OnPhaseChanged(commonMatch.Phase);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;

        redEliminations.OnValueChanged -= OnRedScoreChanged;
        blueEliminations.OnValueChanged -= OnBlueScoreChanged;
        playerStats.OnListChanged -= OnPlayerStatsChanged;

        if (commonMatch != null)
        {
            commonMatch.PhaseChanged -= OnPhaseChanged;
            commonMatch.ServerDeathConfirmed -= OnServerDeathConfirmed;
            commonMatch.TimeExpired -= OnTimeExpired;
        }

        serverState = null;
        ScoreChanged = null;
        PlayerStatsChanged = null;
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        if (!IsServer)
            return;

        if (phase == MatchPhase.WaitingForPlayers || phase == MatchPhase.Countdown)
        {
            serverState = null;
            redEliminations.Value = 0;
            blueEliminations.Value = 0;
            playerStats.Clear();
            return;
        }

        if (phase != MatchPhase.Playing || !IsTdm || serverState != null)
            return;

        var initialPlayers = new List<PlayerMatchStats>(lobby.Players.Count);
        foreach (LobbyPlayerData player in lobby.Players)
            initialPlayers.Add(new PlayerMatchStats(player.ClientId, player.TeamId));

        try
        {
            serverState = new TdmMatchState(initialPlayers);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError($"[TDM] No se pudo iniciar el marcador: {exception.Message}");
            return;
        }

        PublishStats();
        Debug.Log($"[TDM] Marcador iniciado con {initialPlayers.Count} jugadores.");
    }

    private void OnServerDeathConfirmed(PlayerDeathInfo death)
    {
        if (!IsServer || !IsTdm || serverState == null ||
            commonMatch.Phase != MatchPhase.Playing)
            return;

        TdmDeathResult applied = TdmRules.ApplyDeath(
            serverState, death, commonMatch.Phase);
        if (!applied.WasCounted)
            return;

        serverState = applied.State;
        PublishStats();
        redEliminations.Value = serverState.RedEliminations;
        blueEliminations.Value = serverState.BlueEliminations;

        Debug.Log($"[TDM] Eliminaciones: Rojo {RedEliminations} - Azul {BlueEliminations}. " +
                  $"Muerte #{death.EventId}: {death.Cause}.");

        if (TdmRules.TryResolveScoreLimit(
                serverState, lobby.RequiredPlayerCount,
                commonMatch.Phase, out MatchResultData result))
            commonMatch.TryFinishMatch(result);
    }

    private void OnTimeExpired()
    {
        if (!IsServer || !IsTdm || serverState == null ||
            !TdmRules.TryResolveTimeExpired(
                serverState, commonMatch.Phase, out MatchResultData result))
            return;

        commonMatch.TryFinishMatch(result);
    }

    private void PublishStats()
    {
        if (serverState == null)
            return;

        IReadOnlyList<PlayerMatchStats> players = serverState.Players;
        if (playerStats.Count != players.Count)
        {
            playerStats.Clear();
            for (int i = 0; i < players.Count; i++)
                playerStats.Add(players[i]);
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (playerStats[i] != players[i])
                playerStats[i] = players[i];
        }
    }

    private void OnRedScoreChanged(int previous, int current) => ScoreChanged?.Invoke();
    private void OnBlueScoreChanged(int previous, int current) => ScoreChanged?.Invoke();
    private void OnPlayerStatsChanged(NetworkListEvent<PlayerMatchStats> change) =>
        PlayerStatsChanged?.Invoke();

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }
}
