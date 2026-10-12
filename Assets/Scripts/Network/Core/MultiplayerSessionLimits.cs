/// <summary>
/// Separa la capacidad técnica de una sala de la cantidad elegida para una
/// partida. Relay reserva una sala para ocho personas; cada modo decide qué
/// cantidades de partida admite dentro de ese límite.
/// </summary>
public static class MultiplayerSessionLimits
{
    public const int RoomCapacity = 8;
    public const int RemoteClientCapacity = RoomCapacity - 1;

    public static bool CanAcceptConnection(int connectedPlayerCount)
    {
        return connectedPlayerCount >= 0 &&
            connectedPlayerCount < RoomCapacity;
    }

    public static bool CanSelectMatchPlayerCount(
        int selectedPlayerCount,
        int connectedPlayerCount = 0)
    {
        return connectedPlayerCount >= 0 &&
            connectedPlayerCount <= RoomCapacity &&
            selectedPlayerCount > 0 &&
            selectedPlayerCount <= RoomCapacity &&
            selectedPlayerCount >= connectedPlayerCount;
    }
}
