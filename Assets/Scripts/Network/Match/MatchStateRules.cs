public static class MatchStateRules
{
    public static bool CanTransition(
        MatchPhase currentPhase,
        MatchPhase nextPhase,
        bool resetAuthorized = false)
    {
        if (currentPhase == nextPhase)
            return false;

        return currentPhase switch
        {
            MatchPhase.WaitingForPlayers =>
                nextPhase == MatchPhase.Countdown,
            MatchPhase.Countdown =>
                nextPhase == MatchPhase.Playing ||
                nextPhase == MatchPhase.WaitingForPlayers,
            MatchPhase.Playing =>
                nextPhase == MatchPhase.Finished,
            MatchPhase.Finished =>
                resetAuthorized &&
                nextPhase == MatchPhase.WaitingForPlayers,
            _ => false
        };
    }

    public static bool CanStartCountdown(MatchPhase currentPhase)
    {
        return CanTransition(currentPhase, MatchPhase.Countdown);
    }

    public static bool CanCancelCountdown(MatchPhase currentPhase)
    {
        return CanTransition(currentPhase, MatchPhase.WaitingForPlayers);
    }

    public static bool CanBeginMatch(MatchPhase currentPhase)
    {
        return CanTransition(currentPhase, MatchPhase.Playing);
    }

    public static bool CanFinishMatch(MatchPhase currentPhase)
    {
        return CanTransition(currentPhase, MatchPhase.Finished);
    }

    public static bool CanResetForRematch(
        MatchPhase currentPhase,
        bool resetAuthorized)
    {
        return CanTransition(
            currentPhase,
            MatchPhase.WaitingForPlayers,
            resetAuthorized
        );
    }
}
