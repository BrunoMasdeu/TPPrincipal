using System.Collections.Generic;

/// <summary>
/// Reglas puras para editar la configuración de la próxima partida sin
/// recrear la sala de red.
/// </summary>
public static class LobbyConfigurationRules
{
    public static bool CanHostEdit(
        bool isServer,
        bool isHostRequest,
        SessionPhase phase)
    {
        return isServer && isHostRequest && phase == SessionPhase.Lobby;
    }

    public static bool IsSelectableMultiplayerMode(
        GameModeDefinition definition)
    {
        return definition != null &&
            definition.IsValid(out _) &&
            (definition.GameModeId == GameModeId.TDM ||
             definition.GameModeId == GameModeId.CTF);
    }

    public static bool CanSelectPlayerCount(
        GameModeDefinition definition,
        int selectedPlayerCount,
        int connectedPlayerCount)
    {
        return IsSelectableMultiplayerMode(definition) &&
            MultiplayerSessionLimits.CanSelectMatchPlayerCount(
                selectedPlayerCount,
                connectedPlayerCount) &&
            definition.AllowsPlayerCount(selectedPlayerCount);
    }

    public static MapDefinition ResolveMapForMode(
        IReadOnlyList<MapDefinition> maps,
        MapId currentMapId,
        GameModeId requestedMode)
    {
        MapDefinition current = MapCatalogRules.FindById(maps, currentMapId);

        return MapCatalogRules.IsSelectableForMode(current, requestedMode)
            ? current
            : MapCatalogRules.FindDefault(maps, requestedMode);
    }

    public static bool IsConfigurationComplete(
        GameModeDefinition definition,
        int selectedPlayerCount,
        bool hasValidSceneSelection)
    {
        return definition != null &&
            definition.IsValid(out _) &&
            MultiplayerSessionLimits.CanSelectMatchPlayerCount(
                selectedPlayerCount) &&
            definition.AllowsPlayerCount(selectedPlayerCount) &&
            hasValidSceneSelection;
    }
}
