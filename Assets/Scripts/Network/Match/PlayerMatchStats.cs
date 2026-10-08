using System;
using Unity.Netcode;

[Serializable]
public struct PlayerMatchStats : INetworkSerializable, IEquatable<PlayerMatchStats>
{
    public ulong ClientId;
    public TeamId TeamId;
    public int Kills;
    public int Deaths;

    public PlayerMatchStats(
        ulong clientId,
        TeamId teamId,
        int kills = 0,
        int deaths = 0)
    {
        ClientId = clientId;
        TeamId = teamId;
        Kills = kills;
        Deaths = deaths;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref Kills);
        serializer.SerializeValue(ref Deaths);
    }

    public bool Equals(PlayerMatchStats other)
    {
        return ClientId == other.ClientId &&
            TeamId == other.TeamId &&
            Kills == other.Kills &&
            Deaths == other.Deaths;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerMatchStats other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = ClientId.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)TeamId;
            hashCode = (hashCode * 397) ^ Kills;
            hashCode = (hashCode * 397) ^ Deaths;
            return hashCode;
        }
    }

    public static bool operator ==(PlayerMatchStats left, PlayerMatchStats right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PlayerMatchStats left, PlayerMatchStats right)
    {
        return !left.Equals(right);
    }
}
