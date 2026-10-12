using System.Collections.Generic;

public static class MapCatalogRules
{
    public static bool IsSelectableForMode(
        MapDefinition map,
        GameModeId gameModeId)
    {
        return map != null &&
            map.Selectable &&
            map.IsValid(out _) &&
            map.SupportsMode(gameModeId);
    }

    public static MapDefinition FindById(
        IReadOnlyList<MapDefinition> maps,
        MapId mapId)
    {
        if (maps == null || mapId == MapId.None)
            return null;

        for (int i = 0; i < maps.Count; i++)
        {
            MapDefinition map = maps[i];

            if (map != null && map.MapId == mapId)
                return map;
        }

        return null;
    }

    public static MapDefinition FindDefault(
        IReadOnlyList<MapDefinition> maps,
        GameModeId gameModeId)
    {
        if (maps == null)
            return null;

        for (int i = 0; i < maps.Count; i++)
        {
            if (IsSelectableForMode(maps[i], gameModeId))
                return maps[i];
        }

        return null;
    }

    public static bool TryResolveSelection(
        IReadOnlyList<MapDefinition> maps,
        MapId mapId,
        GameModeId gameModeId,
        out MapDefinition map,
        out string validationError)
    {
        map = FindById(maps, mapId);

        if (map == null)
        {
            validationError = "El mapa seleccionado no está registrado.";
            return false;
        }

        if (!map.IsValid(out validationError))
            return false;

        if (!map.Selectable)
        {
            validationError = "El mapa seleccionado no está habilitado.";
            return false;
        }

        if (!map.SupportsMode(gameModeId))
        {
            validationError =
                $"El mapa {map.DisplayName} no admite el modo {gameModeId}.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    public static bool IsCatalogValid(
        IReadOnlyList<MapDefinition> maps,
        out string validationError)
    {
        if (maps == null || maps.Count == 0)
        {
            validationError = string.Empty;
            return true;
        }

        for (int i = 0; i < maps.Count; i++)
        {
            MapDefinition map = maps[i];

            if (map == null)
            {
                validationError = "El catálogo contiene una referencia vacía.";
                return false;
            }

            if (!map.IsValid(out validationError))
                return false;

            for (int j = i + 1; j < maps.Count; j++)
            {
                if (maps[j] != null && maps[j].MapId == map.MapId)
                {
                    validationError =
                        $"El identificador de mapa {map.MapId} está repetido.";
                    return false;
                }
            }
        }

        validationError = string.Empty;
        return true;
    }
}
