public static class PlayerSpawnReadinessRules
{
    public static bool CanBeginSpawn(
        bool isLoadingPhase,
        int selectedPlayerCount,
        int connectedPlayerCount,
        int loadedPlayerCount,
        int timedOutPlayerCount)
    {
        return isLoadingPhase &&
            selectedPlayerCount > 0 &&
            timedOutPlayerCount == 0 &&
            connectedPlayerCount == selectedPlayerCount &&
            loadedPlayerCount == selectedPlayerCount;
    }

    public static bool ArePlayersReady(
        int selectedPlayerCount,
        int connectedPlayerCount,
        int loadedPlayerCount,
        int spawnedPlayerCount,
        int timedOutPlayerCount)
    {
        return selectedPlayerCount > 0 &&
            timedOutPlayerCount == 0 &&
            connectedPlayerCount == selectedPlayerCount &&
            loadedPlayerCount == selectedPlayerCount &&
            spawnedPlayerCount == selectedPlayerCount;
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
