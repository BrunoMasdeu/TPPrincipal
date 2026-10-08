using System.Collections.Generic;

public static class LobbyValidationRules
{
    public static bool IsPlayerCountAllowed(
        int playerCount,
        int minimumPlayerCount,
        int maximumPlayerCount)
    {
        return minimumPlayerCount > 0 &&
            maximumPlayerCount >= minimumPlayerCount &&
            playerCount >= minimumPlayerCount &&
            playerCount <= maximumPlayerCount;
    }

    public static bool HasCapacity(int currentPlayerCount, int maximumPlayerCount)
    {
        return currentPlayerCount >= 0 &&
            maximumPlayerCount > 0 &&
            currentPlayerCount < maximumPlayerCount;
    }

    public static bool IsValidTeam(TeamId teamId)
    {
        return teamId == TeamId.Red || teamId == TeamId.Blue;
    }

    public static bool HasSelectedMode(GameModeId gameModeId)
    {
        return gameModeId != GameModeId.None;
    }

    public static bool HaveUniqueClientIds(IReadOnlyList<LobbyPlayerData> players)
    {
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                if (players[i].ClientId == players[j].ClientId)
                    return false;
            }
        }

        return true;
    }

    public static bool HaveValidTeams(IReadOnlyList<LobbyPlayerData> players)
    {
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            if (!IsValidTeam(players[i].TeamId))
                return false;
        }

        return true;
    }

    public static bool AreTeamsBalanced(
        IReadOnlyList<LobbyPlayerData> players,
        int maximumDifference = 1)
    {
        if (players == null || maximumDifference < 0)
            return false;

        int redPlayers = 0;
        int bluePlayers = 0;

        for (int i = 0; i < players.Count; i++)
        {
            switch (players[i].TeamId)
            {
                case TeamId.Red:
                    redPlayers++;
                    break;
                case TeamId.Blue:
                    bluePlayers++;
                    break;
                default:
                    return false;
            }
        }

        int difference = redPlayers - bluePlayers;
        return difference >= -maximumDifference &&
            difference <= maximumDifference;
    }

    public static bool AreAllPlayersReady(
        IReadOnlyList<LobbyPlayerData> players,
        int requiredPlayerCount)
    {
        if (players == null ||
            requiredPlayerCount <= 0 ||
            players.Count != requiredPlayerCount)
        {
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].IsReady)
                return false;
        }

        return true;
    }

    public static bool CanStartMatch(
        GameModeId gameModeId,
        int requiredPlayerCount,
        IReadOnlyList<LobbyPlayerData> players,
        bool requireBalancedTeams = true,
        bool requiresTeams = true)
    {
        if (!HasSelectedMode(gameModeId) ||
            !AreAllPlayersReady(players, requiredPlayerCount) ||
            !HaveUniqueClientIds(players))
        {
            return false;
        }

        if (!requiresTeams)
            return true;

        if (!HaveValidTeams(players))
            return false;

        return !requireBalancedTeams || AreTeamsBalanced(players);
    }

    public static bool CanAssignPlayerToTeam(
        IReadOnlyList<LobbyPlayerData> players,
        ulong clientId,
        TeamId requestedTeam,
        int requiredPlayerCount,
        int maximumDifference = 1)
    {
        if (players == null ||
            !IsValidTeam(requestedTeam) ||
            requiredPlayerCount < 2 ||
            requiredPlayerCount % 2 != 0 ||
            players.Count > requiredPlayerCount ||
            maximumDifference < 0)
        {
            return false;
        }

        bool playerExists = false;
        int redPlayers = 0;
        int bluePlayers = 0;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData player = players[i];

            if (player.ClientId == clientId)
            {
                if (playerExists)
                    return false;

                playerExists = true;
                continue;
            }

            switch (player.TeamId)
            {
                case TeamId.None:
                    break;
                case TeamId.Red:
                    redPlayers++;
                    break;
                case TeamId.Blue:
                    bluePlayers++;
                    break;
                default:
                    return false;
            }
        }

        if (!playerExists)
            return false;

        if (requestedTeam == TeamId.Red)
            redPlayers++;
        else
            bluePlayers++;

        int teamCapacity = requiredPlayerCount / 2;

        if (redPlayers > teamCapacity || bluePlayers > teamCapacity)
            return false;

        int difference = redPlayers - bluePlayers;
        return difference >= -maximumDifference &&
            difference <= maximumDifference;
    }
}
