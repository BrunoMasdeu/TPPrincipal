using System;
using System.Collections.Generic;

/// <summary>Estado de puntuación TDM. Cada muerte válida produce una copia nueva.</summary>
public sealed class TdmMatchState
{
    private readonly PlayerMatchStats[] players;
    private readonly HashSet<ulong> processedEventIds;

    public int RedEliminations { get; }
    public int BlueEliminations { get; }
    public IReadOnlyList<PlayerMatchStats> Players => Array.AsReadOnly(players);

    public TdmMatchState(IReadOnlyList<PlayerMatchStats> initialPlayers)
        : this(initialPlayers, 0, 0, new HashSet<ulong>())
    {
    }

    private TdmMatchState(
        IReadOnlyList<PlayerMatchStats> initialPlayers,
        int redEliminations,
        int blueEliminations,
        HashSet<ulong> eventIds)
    {
        if (initialPlayers == null)
            throw new ArgumentNullException(nameof(initialPlayers));

        players = new PlayerMatchStats[initialPlayers.Count];
        var clientIds = new HashSet<ulong>();
        for (int i = 0; i < initialPlayers.Count; i++)
        {
            PlayerMatchStats player = initialPlayers[i];
            if (!TdmRules.IsPlayableTeam(player.TeamId) ||
                player.Kills < 0 || player.Deaths < 0 ||
                !clientIds.Add(player.ClientId))
                throw new ArgumentException("Los jugadores TDM deben tener equipo válido, estadísticas no negativas e ID único.", nameof(initialPlayers));

            players[i] = player;
        }

        RedEliminations = redEliminations;
        BlueEliminations = blueEliminations;
        processedEventIds = new HashSet<ulong>(eventIds);
    }

    public bool HasProcessedEvent(ulong eventId) => processedEventIds.Contains(eventId);

    public bool TryGetPlayer(ulong clientId, out PlayerMatchStats player)
    {
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ClientId == clientId)
            {
                player = players[i];
                return true;
            }
        }

        player = default;
        return false;
    }

    internal TdmMatchState WithDeath(PlayerDeathInfo death, bool enemyKill)
    {
        var nextPlayers = (PlayerMatchStats[])players.Clone();
        for (int i = 0; i < nextPlayers.Length; i++)
        {
            if (nextPlayers[i].ClientId == death.VictimClientId)
                nextPlayers[i].Deaths++;
            if (enemyKill && nextPlayers[i].ClientId == death.AttackerClientId)
                nextPlayers[i].Kills++;
        }

        var nextIds = new HashSet<ulong>(processedEventIds) { death.EventId };
        int red = RedEliminations + (enemyKill && death.AttackerTeamId == TeamId.Red ? 1 : 0);
        int blue = BlueEliminations + (enemyKill && death.AttackerTeamId == TeamId.Blue ? 1 : 0);
        return new TdmMatchState(nextPlayers, red, blue, nextIds);
    }
}

public enum TdmDeathDecision : byte
{
    Counted,
    NotPlaying,
    Duplicate,
    Invalid
}

public readonly struct TdmDeathResult
{
    public TdmMatchState State { get; }
    public TdmDeathDecision Decision { get; }
    public TeamId ScoringTeam { get; }
    public bool WasCounted => Decision == TdmDeathDecision.Counted;

    public TdmDeathResult(TdmMatchState state, TdmDeathDecision decision, TeamId scoringTeam)
    {
        State = state;
        Decision = decision;
        ScoringTeam = scoringTeam;
    }
}

/// <summary>Foto fija del resultado; una muerte posterior no altera este resumen.</summary>
public sealed class TdmFinalSummary
{
    private readonly PlayerMatchStats[] players;

    public MatchResultData Result { get; }
    public int RedEliminations { get; }
    public int BlueEliminations { get; }
    public IReadOnlyList<PlayerMatchStats> Players => Array.AsReadOnly(players);

    internal TdmFinalSummary(TdmMatchState state, MatchResultData result)
    {
        Result = result;
        RedEliminations = state.RedEliminations;
        BlueEliminations = state.BlueEliminations;
        players = new PlayerMatchStats[state.Players.Count];
        for (int i = 0; i < players.Length; i++)
            players[i] = state.Players[i];
    }
}

/// <summary>
/// Reglas puras: no reciben input ni manejan reloj o RPC. El servidor les pasa
/// muertes ya confirmadas y la fase compartida de NetworkMatchManager.
/// </summary>
public static class TdmRules
{
    public static TdmDeathResult ApplyDeath(TdmMatchState state, PlayerDeathInfo death, MatchPhase phase)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing)
            return new TdmDeathResult(state, TdmDeathDecision.NotPlaying, TeamId.None);
        if (death.EventId == 0)
            return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);
        if (state.HasProcessedEvent(death.EventId))
            return new TdmDeathResult(state, TdmDeathDecision.Duplicate, TeamId.None);
        if (!IsRegisteredPlayer(state, death.VictimClientId, death.VictimTeamId))
            return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);

        bool enemyKill = false;
        switch (death.Cause)
        {
            case PlayerDeathCause.PlayerAttack:
                enemyKill = death.HasAttacker &&
                    death.AttackerClientId != death.VictimClientId &&
                    IsRegisteredPlayer(state, death.AttackerClientId, death.AttackerTeamId) &&
                    death.AttackerTeamId != death.VictimTeamId;
                if (!enemyKill)
                    return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);
                break;

            case PlayerDeathCause.Suicide:
                // Un suicidio con atacante explícito solo es coherente si es la víctima.
                if (death.HasAttacker &&
                    (death.AttackerClientId != death.VictimClientId ||
                     death.AttackerTeamId != death.VictimTeamId))
                    return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);
                break;

            case PlayerDeathCause.Environmental:
                if (death.HasAttacker)
                    return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);
                break;

            default:
                return new TdmDeathResult(state, TdmDeathDecision.Invalid, TeamId.None);
        }

        return new TdmDeathResult(
            state.WithDeath(death, enemyKill),
            TdmDeathDecision.Counted,
            enemyKill ? death.AttackerTeamId : TeamId.None);
    }

    /// <summary>Los dos jugadores son solo para pruebas; no tienen límite oficial.</summary>
    public static bool TryGetOfficialEliminationLimit(int playerCount, out int limit)
    {
        switch (playerCount)
        {
            case 4: limit = 30; return true;
            case 6: limit = 40; return true;
            case 8: limit = 60; return true;
            default: limit = 0; return false;
        }
    }

    public static bool TryResolveScoreLimit(
        TdmMatchState state, int playerCount, MatchPhase phase, out MatchResultData result)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        result = default;
        if (phase != MatchPhase.Playing || state.Players.Count != playerCount ||
            !TryGetOfficialEliminationLimit(playerCount, out int limit))
            return false;

        bool redReached = state.RedEliminations >= limit;
        bool blueReached = state.BlueEliminations >= limit;
        if (redReached == blueReached)
            return false; // Nadie llegó o el estado es ambiguo; no inventar ganador.

        result = new MatchResultData(
            redReached ? TeamId.Red : TeamId.Blue, MatchEndReason.ScoreLimit, false);
        return true;
    }

    /// <summary>Invocar solo cuando el reloj común notifique TimeExpired.</summary>
    public static bool TryResolveTimeExpired(
        TdmMatchState state, MatchPhase phase, out MatchResultData result)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        result = default;
        if (phase != MatchPhase.Playing)
            return false;

        bool draw = state.RedEliminations == state.BlueEliminations;
        TeamId winner = draw ? TeamId.None :
            state.RedEliminations > state.BlueEliminations ? TeamId.Red : TeamId.Blue;
        result = new MatchResultData(winner, MatchEndReason.TimeExpired, draw);
        return true;
    }

    public static bool TryCreateFinalSummary(
        TdmMatchState state, MatchResultData result, out TdmFinalSummary summary)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        summary = null;
        if ((result.EndReason != MatchEndReason.TimeExpired &&
             result.EndReason != MatchEndReason.ScoreLimit) ||
            (result.IsDraw && (result.WinningTeam != TeamId.None ||
                               result.EndReason == MatchEndReason.ScoreLimit)) ||
            (!result.IsDraw && !IsPlayableTeam(result.WinningTeam)))
            return false;

        summary = new TdmFinalSummary(state, result);
        return true;
    }

    internal static bool IsPlayableTeam(TeamId team) => team == TeamId.Red || team == TeamId.Blue;

    private static bool IsRegisteredPlayer(TdmMatchState state, ulong clientId, TeamId team) =>
        IsPlayableTeam(team) && state.TryGetPlayer(clientId, out PlayerMatchStats player) &&
        player.TeamId == team;
}
