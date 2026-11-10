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

    // [COG-607] Ping en milisegundos, medido por el servidor durante la
    // partida. El host queda en 0 porque es la PC que hace de servidor.
    public ushort Ping;

    // [COG-620] Si este jugador ya votó que quiere la revancha.
    public bool QuiereRevancha;

    public LobbyPlayerData(ulong clientId, TeamId teamId, bool isReady, string nombre = "")
    {
        ClientId = clientId;
        TeamId = teamId;
        IsReady = isReady;
        Nombre = default;
        Nombre.CopyFromTruncated(nombre ?? string.Empty);
        Ping = 0;
        QuiereRevancha = false;
    }

    /// <summary>
    /// [COG-607][COG-620] Compara dos estados del mismo jugador ignorando el
    /// ping y el voto de revancha. Sirve para saber si lo que cambió es
    /// configuración del lobby (equipo, nombre, listo) o solo esos dos datos,
    /// que se actualizan solos durante la partida.
    /// </summary>
    public static bool SoloCambioPingOVoto(
        LobbyPlayerData anterior,
        LobbyPlayerData actual)
    {
        anterior.Ping = actual.Ping;
        anterior.QuiereRevancha = actual.QuiereRevancha;
        return anterior.Equals(actual);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref Nombre);
        serializer.SerializeValue(ref Ping);
        serializer.SerializeValue(ref QuiereRevancha);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
            TeamId == other.TeamId &&
            IsReady == other.IsReady &&
            Nombre.Equals(other.Nombre) &&
            Ping == other.Ping &&
            QuiereRevancha == other.QuiereRevancha;
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
            hashCode = (hashCode * 397) ^ Ping;
            hashCode = (hashCode * 397) ^ QuiereRevancha.GetHashCode();
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
