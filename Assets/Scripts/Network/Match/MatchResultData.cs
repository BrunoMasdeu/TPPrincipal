using System;
using Unity.Netcode;

[Serializable]
public struct MatchResultData : INetworkSerializable, IEquatable<MatchResultData>
{
    public TeamId WinningTeam;
    public MatchEndReason EndReason;
    public bool IsDraw;

    public MatchResultData(
        TeamId winningTeam,
        MatchEndReason endReason,
        bool isDraw)
    {
        WinningTeam = winningTeam;
        EndReason = endReason;
        IsDraw = isDraw;
    }

    public bool IsPending => EndReason == MatchEndReason.None;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref WinningTeam);
        serializer.SerializeValue(ref EndReason);
        serializer.SerializeValue(ref IsDraw);
    }

    public bool Equals(MatchResultData other)
    {
        return WinningTeam == other.WinningTeam &&
            EndReason == other.EndReason &&
            IsDraw == other.IsDraw;
    }

    public override bool Equals(object obj)
    {
        return obj is MatchResultData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (int)WinningTeam;
            hashCode = (hashCode * 397) ^ (int)EndReason;
            hashCode = (hashCode * 397) ^ IsDraw.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(MatchResultData left, MatchResultData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MatchResultData left, MatchResultData right)
    {
        return !left.Equals(right);
    }
}
