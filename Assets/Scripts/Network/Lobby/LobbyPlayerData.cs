using System;
using Unity.Netcode;

[Serializable]
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public TeamId TeamId;
    public bool IsReady;

    public LobbyPlayerData(ulong clientId, TeamId teamId, bool isReady)
    {
        ClientId = clientId;
        TeamId = teamId;
        IsReady = isReady;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
            TeamId == other.TeamId &&
            IsReady == other.IsReady;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayerData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = ClientId.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)TeamId;
            hashCode = (hashCode * 397) ^ IsReady.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(LobbyPlayerData left, LobbyPlayerData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LobbyPlayerData left, LobbyPlayerData right)
    {
        return !left.Equals(right);
    }
}
