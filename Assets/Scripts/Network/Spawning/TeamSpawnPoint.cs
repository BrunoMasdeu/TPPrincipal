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

    private void OnDrawGizmos()
    {
        Gizmos.color = teamId switch
        {
            TeamId.Red => new Color(1f, 0.2f, 0.2f, 0.9f),
            TeamId.Blue => new Color(0.2f, 0.5f, 1f, 0.9f),
            _ => Color.white
        };

        Vector3 position = transform.position;
        Gizmos.DrawWireSphere(position, 0.45f);
        Gizmos.DrawLine(position, position + transform.forward * 2f);
    }
}
