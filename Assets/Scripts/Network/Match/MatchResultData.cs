using System;

[Serializable]
public struct MatchResultData
{
    public TeamId WinningTeam;
    public MatchEndReason EndReason;
    public bool IsDraw;
}
