using UnityEngine;

[CreateAssetMenu(
    fileName = "GameModeDefinition",
    menuName = "Game/Network/Game Mode Definition"
)]
public class GameModeDefinition : ScriptableObject
{
    [SerializeField] private GameModeId gameModeId;
    [SerializeField] private string sceneName;
    [SerializeField, Min(0f)] private float durationSeconds = 120f;
    [SerializeField, Min(1)] private int scoreLimit = 1;

    public GameModeId GameModeId => gameModeId;
    public string SceneName => sceneName;
    public float DurationSeconds => durationSeconds;
    public int ScoreLimit => scoreLimit;
}
