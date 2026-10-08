using UnityEngine;

public class TeamSpawnPoint : MonoBehaviour
{
    [SerializeField] private TeamId teamId;

    public TeamId TeamId => teamId;

    public static int GetDeterministicIndex(
        int assignedPlayerCount,
        int availableSpawnPointCount)
    {
        if (assignedPlayerCount < 0 || availableSpawnPointCount <= 0)
            return -1;

        return assignedPlayerCount % availableSpawnPointCount;
    }
}
