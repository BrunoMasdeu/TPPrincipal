using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MapCatalogRulesTests
{
    [Test]
    public void MapaValidoExponeNombreEscenaYModosCompatibles()
    {
        MapDefinition map = CreateMap(
            MapId.ArenaIndustrial,
            "Arena Industrial",
            "ArenaIndustrial",
            true,
            GameModeId.TDM,
            GameModeId.CTF
        );

        try
        {
            Assert.That(map.IsValid(out string error), Is.True, error);
            Assert.That(map.SupportsMode(GameModeId.TDM), Is.True);
            Assert.That(map.SupportsMode(GameModeId.CTF), Is.True);
            Assert.That(map.SupportsMode(GameModeId.Race), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void CatalogoFiltraMapaSegunModoYSeleccionable()
    {
        MapDefinition tdmTest = CreateMap(
            MapId.TdmTest,
            "Prueba TDM",
            "tdm_scene_prueba",
            true,
            GameModeId.TDM
        );
        MapDefinition ctfTest = CreateMap(
            MapId.CtfTest,
            "Prueba CTF",
            "ctf_scene_prueba",
            true,
            GameModeId.CTF
        );
        MapDefinition arena = CreateMap(
            MapId.ArenaIndustrial,
            "Arena Industrial",
            "ArenaIndustrial",
            true,
            GameModeId.TDM,
            GameModeId.CTF
        );
        MapDefinition[] maps = { tdmTest, ctfTest, arena };

        try
        {
            Assert.That(
                MapCatalogRules.FindDefault(maps, GameModeId.TDM),
                Is.SameAs(tdmTest)
            );
            Assert.That(
                MapCatalogRules.FindDefault(maps, GameModeId.CTF),
                Is.SameAs(ctfTest)
            );
            Assert.That(
                MapCatalogRules.TryResolveSelection(
                    maps,
                    MapId.ArenaIndustrial,
                    GameModeId.CTF,
                    out MapDefinition selected,
                    out string error),
                Is.True,
                error
            );
            Assert.That(selected, Is.SameAs(arena));
            Assert.That(
                MapCatalogRules.TryResolveSelection(
                    maps,
                    MapId.TdmTest,
                    GameModeId.CTF,
                    out _,
                    out _),
                Is.False
            );
        }
        finally
        {
            DestroyMaps(maps);
        }
    }

    [Test]
    public void CatalogoRechazaIdsRepetidosYMapasDeshabilitados()
    {
        MapDefinition first = CreateMap(
            MapId.TdmTest,
            "Primero",
            "FirstScene",
            true,
            GameModeId.TDM
        );
        MapDefinition duplicate = CreateMap(
            MapId.TdmTest,
            "Duplicado",
            "SecondScene",
            false,
            GameModeId.TDM
        );
        MapDefinition[] maps = { first, duplicate };

        try
        {
            Assert.That(
                MapCatalogRules.IsCatalogValid(maps, out string error),
                Is.False
            );
            Assert.That(error, Does.Contain("repetido"));
            Assert.That(
                MapCatalogRules.IsSelectableForMode(
                    duplicate,
                    GameModeId.TDM),
                Is.False
            );
        }
        finally
        {
            DestroyMaps(maps);
        }
    }

    private static MapDefinition CreateMap(
        MapId mapId,
        string displayName,
        string sceneName,
        bool selectable,
        params GameModeId[] supportedModes)
    {
        MapDefinition map = ScriptableObject.CreateInstance<MapDefinition>();
        SetField(map, "mapId", mapId);
        SetField(map, "displayName", displayName);
        SetField(map, "sceneName", sceneName);
        SetField(map, "selectable", selectable);
        SetField(map, "supportedGameModes", supportedModes);
        return map;
    }

    private static void SetField<T>(
        MapDefinition map,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(MapDefinition).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null, $"No existe el campo {fieldName}.");
        field.SetValue(map, value);
    }

    private static void DestroyMaps(MapDefinition[] maps)
    {
        for (int i = 0; i < maps.Length; i++)
            Object.DestroyImmediate(maps[i]);
    }
}
