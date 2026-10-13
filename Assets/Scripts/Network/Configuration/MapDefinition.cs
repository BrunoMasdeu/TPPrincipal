using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MapDefinition",
    menuName = "Game/Network/Map Definition"
)]
public sealed class MapDefinition : ScriptableObject
{
    [SerializeField] private MapId mapId;
    [SerializeField] private string displayName;
    [SerializeField] private string sceneName;
    [SerializeField] private Sprite previewImage;
    [SerializeField] private GameModeId[] supportedGameModes =
    {
        GameModeId.TDM,
        GameModeId.CTF
    };
    [SerializeField] private bool selectable = true;

    public MapId MapId => mapId;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public Sprite PreviewImage => previewImage;
    public IReadOnlyList<GameModeId> SupportedGameModes => supportedGameModes;
    public bool Selectable => selectable;

    public bool SupportsMode(GameModeId gameModeId)
    {
        if (gameModeId == GameModeId.None || supportedGameModes == null)
            return false;

        for (int i = 0; i < supportedGameModes.Length; i++)
        {
            if (supportedGameModes[i] == gameModeId)
                return true;
        }

        return false;
    }

    public bool IsValid(out string validationError)
    {
        if (mapId == MapId.None)
        {
            validationError = "El mapa no tiene un identificador válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            validationError = "El mapa no tiene un nombre visible.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            validationError = "El mapa no tiene una escena configurada.";
            return false;
        }

        if (supportedGameModes == null || supportedGameModes.Length == 0)
        {
            validationError = "El mapa no admite ningún modo de juego.";
            return false;
        }

        for (int i = 0; i < supportedGameModes.Length; i++)
        {
            if (supportedGameModes[i] == GameModeId.None)
            {
                validationError = "El mapa contiene un modo inválido.";
                return false;
            }

            for (int j = i + 1; j < supportedGameModes.Length; j++)
            {
                if (supportedGameModes[i] == supportedGameModes[j])
                {
                    validationError =
                        "Los modos compatibles del mapa no pueden repetirse.";
                    return false;
                }
            }
        }

        validationError = string.Empty;
        return true;
    }
}
