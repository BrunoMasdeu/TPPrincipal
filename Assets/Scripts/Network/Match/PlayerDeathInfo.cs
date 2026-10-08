using System;
using Unity.Netcode;

public enum PlayerDeathCause : byte
{
    Unknown,
    PlayerAttack,
    Suicide,
    Environmental
}

[Serializable]
public struct PlayerDeathInfo : INetworkSerializable, IEquatable<PlayerDeathInfo>
{
    public ulong EventId;
    public ulong VictimClientId;
    public TeamId VictimTeamId;
    public bool HasAttacker;
    public ulong AttackerClientId;
    public TeamId AttackerTeamId;
    public PlayerDeathCause Cause;

    public PlayerDeathInfo(
        ulong eventId,
        ulong victimClientId,
        TeamId victimTeamId,
        bool hasAttacker,
        ulong attackerClientId,
        TeamId attackerTeamId,
        PlayerDeathCause cause)
    {
        EventId = eventId;
        VictimClientId = victimClientId;
        VictimTeamId = victimTeamId;
        HasAttacker = hasAttacker;
        AttackerClientId = attackerClientId;
        AttackerTeamId = attackerTeamId;
        Cause = cause;
    }

    public bool IsSameEventAs(PlayerDeathInfo other)
    {
        return EventId == other.EventId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref EventId);
        serializer.SerializeValue(ref VictimClientId);
        serializer.SerializeValue(ref VictimTeamId);
        serializer.SerializeValue(ref HasAttacker);
        serializer.SerializeValue(ref AttackerClientId);
        serializer.SerializeValue(ref AttackerTeamId);
        serializer.SerializeValue(ref Cause);
    }

    public bool Equals(PlayerDeathInfo other)
    {
        return EventId == other.EventId &&
            VictimClientId == other.VictimClientId &&
            VictimTeamId == other.VictimTeamId &&
            HasAttacker == other.HasAttacker &&
            AttackerClientId == other.AttackerClientId &&
            AttackerTeamId == other.AttackerTeamId &&
            Cause == other.Cause;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerDeathInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = EventId.GetHashCode();
            hashCode = (hashCode * 397) ^ VictimClientId.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)VictimTeamId;
            hashCode = (hashCode * 397) ^ HasAttacker.GetHashCode();
            hashCode = (hashCode * 397) ^ AttackerClientId.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)AttackerTeamId;
            hashCode = (hashCode * 397) ^ (int)Cause;
            return hashCode;
        }
    }

    public static bool operator ==(PlayerDeathInfo left, PlayerDeathInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PlayerDeathInfo left, PlayerDeathInfo right)
    {
        return !left.Equals(right);
    }
}
