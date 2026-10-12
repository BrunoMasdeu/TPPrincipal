using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LobbyConfigurationRulesTests
{
    [TestCase(true, true, SessionPhase.Lobby, true)]
    [TestCase(true, false, SessionPhase.Lobby, false)]
    [TestCase(false, true, SessionPhase.Lobby, false)]
    [TestCase(true, true, SessionPhase.Loading, false)]
    [TestCase(true, true, SessionPhase.InMatch, false)]
    public void SoloHostServidorPuedeEditarEnLobby(
        bool isServer,
        bool isHost,
        SessionPhase phase,
        bool expected)
    {
        Assert.That(
            LobbyConfigurationRules.CanHostEdit(
                isServer,
                isHost,
                phase),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void SalaMultijugadorSoloOfreceTdmYCtf()
    {
        GameModeDefinition tdm = CreateDefinition(GameModeId.TDM, 2, 4, 6, 8);
        GameModeDefinition ctf = CreateDefinition(GameModeId.CTF, 2, 4, 6, 8);
        GameModeDefinition race = CreateDefinition(GameModeId.Race, 1);

        try
        {
            SetField(race, "requiresTeams", false);
            Assert.That(
                LobbyConfigurationRules.IsSelectableMultiplayerMode(tdm),
                Is.True);
            Assert.That(
                LobbyConfigurationRules.IsSelectableMultiplayerMode(ctf),
                Is.True);
            Assert.That(
                LobbyConfigurationRules.IsSelectableMultiplayerMode(race),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(tdm);
            Object.DestroyImmediate(ctf);
            Object.DestroyImmediate(race);
        }
    }

    [TestCase(2, 1, true)]
    [TestCase(4, 3, true)]
    [TestCase(2, 3, false)]
    [TestCase(6, 2, true)]
    [TestCase(8, 8, true)]
    [TestCase(3, 2, false)]
    public void CantidadDebeSerAdmitidaYNoMenorQueLosConectados(
        int requestedPlayers,
        int connectedPlayers,
        bool expected)
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.TDM,
            2,
            4,
            6,
            8
        );

        try
        {
            Assert.That(
                LobbyConfigurationRules.CanSelectPlayerCount(
                    definition,
                    requestedPlayers,
                    connectedPlayers),
                Is.EqualTo(expected)
            );
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void CambioDeModoConservaArenaYReemplazaMapaIncompatible()
    {
        MapDefinition tdmTest = CreateMap(
            MapId.TdmTest,
            "Prueba TDM",
            GameModeId.TDM);
        MapDefinition ctfTest = CreateMap(
            MapId.CtfTest,
            "Prueba CTF",
            GameModeId.CTF);
        MapDefinition arena = CreateMap(
            MapId.ArenaIndustrial,
            "Arena Industrial",
            GameModeId.TDM,
            GameModeId.CTF);
        MapDefinition[] maps = { tdmTest, ctfTest, arena };

        try
        {
            Assert.That(
                LobbyConfigurationRules.ResolveMapForMode(
                    maps,
                    MapId.ArenaIndustrial,
                    GameModeId.CTF),
                Is.SameAs(arena)
            );
            Assert.That(
                LobbyConfigurationRules.ResolveMapForMode(
                    maps,
                    MapId.TdmTest,
                    GameModeId.CTF),
                Is.SameAs(ctfTest)
            );
        }
        finally
        {
            for (int i = 0; i < maps.Length; i++)
                Object.DestroyImmediate(maps[i]);
        }
    }

    [Test]
    public void ConfiguracionCompletaRequiereModoCantidadYMapaValidos()
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.CTF,
            2,
            4,
            6,
            8
        );

        try
        {
            Assert.That(
                LobbyConfigurationRules.IsConfigurationComplete(
                    definition,
                    2,
                    true),
                Is.True
            );
            Assert.That(
                LobbyConfigurationRules.IsConfigurationComplete(
                    definition,
                    0,
                    true),
                Is.False
            );
            Assert.That(
                LobbyConfigurationRules.IsConfigurationComplete(
                    definition,
                    2,
                    false),
                Is.False
            );
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    private static GameModeDefinition CreateDefinition(
        GameModeId gameModeId,
        params int[] playerCounts)
    {
        GameModeDefinition definition =
            ScriptableObject.CreateInstance<GameModeDefinition>();
        SetField(definition, "gameModeId", gameModeId);
        SetField(definition, "sceneName", "TestScene");
        SetField(definition, "durationSeconds", 120f);
        SetField(definition, "scoreLimit", 1);
        SetField(definition, "allowedPlayerCounts", playerCounts);
        SetField(definition, "requiresTeams", true);
        return definition;
    }

    private static MapDefinition CreateMap(
        MapId mapId,
        string displayName,
        params GameModeId[] modes)
    {
        MapDefinition map = ScriptableObject.CreateInstance<MapDefinition>();
        SetField(map, "mapId", mapId);
        SetField(map, "displayName", displayName);
        SetField(map, "sceneName", displayName.Replace(" ", string.Empty));
        SetField(map, "supportedGameModes", modes);
        SetField(map, "selectable", true);
        return map;
    }

    private static void SetField<T>(
        T target,
        string fieldName,
        object value)
    {
        FieldInfo field = typeof(T).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null, $"No existe el campo {fieldName}.");
        field.SetValue(target, value);
    }
}
