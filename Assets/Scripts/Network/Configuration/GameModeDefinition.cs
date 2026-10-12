using System.Collections.Generic;
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
    [SerializeField] private int[] allowedPlayerCounts = { 2, 4, 6, 8 };
    [SerializeField] private bool requiresTeams = true;

    public GameModeId GameModeId => gameModeId;
    public string SceneName => sceneName;
    public float DurationSeconds => durationSeconds;
    public int ScoreLimit => scoreLimit;
    public bool RequiresTeams => requiresTeams;
    public IReadOnlyList<int> AllowedPlayerCounts => allowedPlayerCounts;

    public bool AllowsPlayerCount(int playerCount)
    {
        if (allowedPlayerCounts == null)
            return false;

        for (int i = 0; i < allowedPlayerCounts.Length; i++)
        {
            if (allowedPlayerCounts[i] == playerCount)
                return true;
        }

        return false;
    }

    public bool IsValid(out string validationError)
    {
        if (gameModeId == GameModeId.None)
        {
            validationError = "El modo de juego no está seleccionado.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            validationError = "La escena del modo de juego no está configurada.";
            return false;
        }

        if (durationSeconds <= 0f)
        {
            validationError = "La duración debe ser mayor que cero.";
            return false;
        }

        if (scoreLimit <= 0)
        {
            validationError = "El límite de puntuación debe ser mayor que cero.";
            return false;
        }

        if (allowedPlayerCounts == null || allowedPlayerCounts.Length == 0)
        {
            validationError = "Debe existir al menos una cantidad de jugadores permitida.";
            return false;
        }

        for (int i = 0; i < allowedPlayerCounts.Length; i++)
        {
            int playerCount = allowedPlayerCounts[i];

            if (playerCount < 1 ||
                (requiresTeams && (playerCount < 2 || playerCount % 2 != 0)))
            {
                validationError =
                    requiresTeams
                        ? "Las cantidades con equipos deben ser pares y mayores o iguales a dos."
                        : "Las cantidades permitidas deben ser mayores o iguales a uno.";
                return false;
            }

            for (int j = i + 1; j < allowedPlayerCounts.Length; j++)
            {
                if (allowedPlayerCounts[j] == playerCount)
                {
                    validationError =
                        "Las cantidades de jugadores permitidas no pueden repetirse.";
                    return false;
                }
            }
        }

        validationError = string.Empty;
        return true;
    }
}
