using UnityEngine;

public class TeamSpawnPoint : MonoBehaviour
{
    [SerializeField] private TeamId teamId;

    public TeamId TeamId => teamId;
}
