using System;

[Serializable]
public struct PlayerDeathInfo
{
    public ulong VictimClientId;
    public ulong KillerClientId;
    public bool HasKiller;
}
