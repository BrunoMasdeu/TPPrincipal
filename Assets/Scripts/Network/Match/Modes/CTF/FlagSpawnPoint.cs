using UnityEngine;

public class FlagSpawnPoint : MonoBehaviour
{
    [SerializeField] private TeamId teamId;

    public TeamId TeamId => teamId;
}
