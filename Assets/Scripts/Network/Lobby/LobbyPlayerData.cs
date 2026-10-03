using System;

[Serializable]
public struct LobbyPlayerData
{
    public ulong ClientId;
    public TeamId TeamId;
    public bool IsReady;
}
