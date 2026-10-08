using System;
using UnityEngine;

/// <summary>
/// Reglas sin RPC, escenas ni reloj propio. El servidor aporta el tiempo
/// sincronizado y valida al jugador conectado antes de invocarlas.
/// </summary>
public static class CtfRules
{
    public const double DropReturnDelaySeconds = 5d;
    public const float DroppedFlagPickupRadius = 2f;

    public static bool TryPickUpFlag(CtfMatchState state, TeamId flagTeam,
        TeamId playerTeam, ulong playerClientId, MatchPhase phase,
        double serverNow, out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing || !IsPlayableTeam(flagTeam) ||
            !IsPlayableTeam(playerTeam) || flagTeam == playerTeam ||
            !IsValidTime(serverNow))
            return false;

        CtfFlagData flag = state.GetFlag(flagTeam);
        if (flag.State == FlagState.Carried ||
            (flag.State == FlagState.Dropped && serverNow >= flag.ReturnAt))
            return false;

        next = state.WithFlag(flagTeam, CtfFlagData.CarriedBy(playerClientId));
        return true;
    }

    public static bool TryReturnOwnDroppedFlag(CtfMatchState state,
        TeamId flagTeam, TeamId playerTeam, MatchPhase phase,
        out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing || !IsPlayableTeam(flagTeam) ||
            flagTeam != playerTeam ||
            state.GetFlag(flagTeam).State != FlagState.Dropped)
            return false;

        next = state.WithFlag(flagTeam, CtfFlagData.AtBase());
        return true;
    }

    /// <summary>La bandera propia no necesita estar en la base para capturar.</summary>
    public static bool TryCapture(CtfMatchState state, TeamId baseTeam,
        TeamId playerTeam, ulong playerClientId, MatchPhase phase,
        out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing || !IsPlayableTeam(baseTeam) ||
            baseTeam != playerTeam)
            return false;

        TeamId enemyFlagTeam = OpponentOf(baseTeam);
        CtfFlagData enemyFlag = state.GetFlag(enemyFlagTeam);
        if (enemyFlag.State != FlagState.Carried ||
            enemyFlag.CarrierClientId != playerClientId)
            return false;

        next = state.WithCapture(baseTeam);
        return true;
    }

    public static bool TryDropCarriedFlag(CtfMatchState state,
        PlayerDeathInfo death, Vector3 position, double serverNow,
        MatchPhase phase, out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing || death.EventId == 0UL ||
            state.HasProcessedDeath(death.EventId) ||
            death.Cause == PlayerDeathCause.Unknown ||
            !IsPlayableTeam(death.VictimTeamId) ||
            !IsFinite(position) || !IsValidTime(serverNow))
            return false;

        TeamId flagTeam = OpponentOf(death.VictimTeamId);
        CtfFlagData flag = state.GetFlag(flagTeam);
        if (flag.State != FlagState.Carried ||
            flag.CarrierClientId != death.VictimClientId)
            return false;

        next = state.WithFlag(flagTeam,
            CtfFlagData.DroppedAt(position, serverNow + DropReturnDelaySeconds),
            death.EventId);
        return true;
    }

    public static bool TryReturnExpiredFlags(CtfMatchState state,
        double serverNow, MatchPhase phase, out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing || !IsValidTime(serverNow))
            return false;

        if (state.RedFlag.State == FlagState.Dropped &&
            serverNow >= state.RedFlag.ReturnAt)
            next = next.WithFlag(TeamId.Red, CtfFlagData.AtBase());

        if (state.BlueFlag.State == FlagState.Dropped &&
            serverNow >= state.BlueFlag.ReturnAt)
            next = next.WithFlag(TeamId.Blue, CtfFlagData.AtBase());

        return !ReferenceEquals(next, state);
    }

    /// <summary>Un portador desconectado no puede dejar una bandera bloqueada.</summary>
    public static bool TryReturnDisconnectedCarrier(CtfMatchState state,
        ulong clientId, MatchPhase phase, out CtfMatchState next)
    {
        next = state ?? throw new ArgumentNullException(nameof(state));
        if (phase != MatchPhase.Playing)
            return false;

        if (state.RedFlag.State == FlagState.Carried &&
            state.RedFlag.CarrierClientId == clientId)
            next = next.WithFlag(TeamId.Red, CtfFlagData.AtBase());

        if (state.BlueFlag.State == FlagState.Carried &&
            state.BlueFlag.CarrierClientId == clientId)
            next = next.WithFlag(TeamId.Blue, CtfFlagData.AtBase());

        return !ReferenceEquals(next, state);
    }

    /// <summary>Dos jugadores usan un límite provisional sólo para pruebas.</summary>
    public static bool TryGetCaptureLimit(int playerCount, out int limit)
    {
        switch (playerCount)
        {
            case 2: limit = 3; return true;
            case 4: limit = 5; return true;
            case 6: limit = 7; return true;
            case 8: limit = 9; return true;
            default: limit = 0; return false;
        }
    }

    public static bool TryResolveCaptureLimit(CtfMatchState state,
        int playerCount, MatchPhase phase, out MatchResultData result)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        result = default;
        if (phase != MatchPhase.Playing ||
            !TryGetCaptureLimit(playerCount, out int limit))
            return false;

        bool redReached = state.RedCaptures >= limit;
        bool blueReached = state.BlueCaptures >= limit;
        if (redReached == blueReached)
            return false;

        result = new MatchResultData(
            redReached ? TeamId.Red : TeamId.Blue,
            MatchEndReason.ScoreLimit, false);
        return true;
    }

    public static bool TryResolveTimeExpired(CtfMatchState state,
        MatchPhase phase, out MatchResultData result)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        result = default;
        if (phase != MatchPhase.Playing)
            return false;

        bool draw = state.RedCaptures == state.BlueCaptures;
        TeamId winner = draw ? TeamId.None :
            state.RedCaptures > state.BlueCaptures ? TeamId.Red : TeamId.Blue;
        result = new MatchResultData(winner, MatchEndReason.TimeExpired, draw);
        return true;
    }

    private static bool IsPlayableTeam(TeamId team) =>
        team == TeamId.Red || team == TeamId.Blue;

    private static TeamId OpponentOf(TeamId team) =>
        team == TeamId.Red ? TeamId.Blue : TeamId.Red;

    private static bool IsValidTime(double time) =>
        !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0d;

    private static bool IsFinite(Vector3 position) =>
        !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
        !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
        !float.IsNaN(position.z) && !float.IsInfinity(position.z);
}
