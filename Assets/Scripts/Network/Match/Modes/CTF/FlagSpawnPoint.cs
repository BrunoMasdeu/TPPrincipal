using UnityEngine;

public class FlagSpawnPoint : MonoBehaviour
{
    [SerializeField] private TeamId teamId;

    public TeamId TeamId => teamId;

    private void OnDrawGizmos()
    {
        Gizmos.color = teamId switch
        {
            TeamId.Red => new Color(1f, 0.15f, 0.15f, 0.95f),
            TeamId.Blue => new Color(0.15f, 0.45f, 1f, 0.95f),
            _ => Color.white
        };

        Vector3 position = transform.position;
        Gizmos.DrawWireCube(position + Vector3.up * 0.75f,
            new Vector3(0.35f, 1.5f, 0.35f));
        Gizmos.DrawLine(position, position + Vector3.up * 2f);
    }
}
