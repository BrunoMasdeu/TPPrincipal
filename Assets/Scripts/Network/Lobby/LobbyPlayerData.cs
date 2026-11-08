using System;
using Unity.Collections;
using Unity.Netcode;

[Serializable]
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public TeamId TeamId;
    public bool IsReady;

    // [COG-587] Nombre que eligió el jugador. Se usa FixedString porque la
    // lista de jugadores de red (NetworkList) no acepta string.
    // 13 caracteres (la ñ ocupa 2 bytes) entran de sobra en 32 bytes.
    public FixedString32Bytes Nombre;

    public LobbyPlayerData(ulong clientId, TeamId teamId, bool isReady, string nombre = "")
    {
        ClientId = clientId;
        TeamId = teamId;
        IsReady = isReady;
        Nombre = default;
        Nombre.CopyFromTruncated(nombre ?? string.Empty);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref Nombre);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
            TeamId == other.TeamId &&
            IsReady == other.IsReady &&
            Nombre.Equals(other.Nombre);
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
            hashCode = (hashCode * 397) ^ Nombre.GetHashCode();
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
