public static class PlayerSpawnReadinessRules
{
    public static bool CanBeginSpawn(
        bool isLoadingPhase,
        int requiredPlayerCount,
        int connectedPlayerCount,
        int loadedPlayerCount,
        int timedOutPlayerCount)
    {
        return isLoadingPhase &&
            requiredPlayerCount > 0 &&
            timedOutPlayerCount == 0 &&
            connectedPlayerCount == requiredPlayerCount &&
            loadedPlayerCount == requiredPlayerCount;
    }

    public static bool ArePlayersReady(
        int requiredPlayerCount,
        int connectedPlayerCount,
        int loadedPlayerCount,
        int spawnedPlayerCount,
        int timedOutPlayerCount)
    {
        return requiredPlayerCount > 0 &&
            timedOutPlayerCount == 0 &&
            connectedPlayerCount == requiredPlayerCount &&
            loadedPlayerCount == requiredPlayerCount &&
            spawnedPlayerCount == requiredPlayerCount;
    }

    public static bool CanSpawnPlayer(
        bool isServer,
        bool isConnected,
        bool hasLoadedScene,
        bool alreadyHasPlayerObject)
    {
        return isServer &&
            isConnected &&
            hasLoadedScene &&
            !alreadyHasPlayerObject;
    }
}
