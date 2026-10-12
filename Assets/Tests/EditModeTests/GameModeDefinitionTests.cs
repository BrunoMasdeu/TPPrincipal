using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameModeDefinitionTests
{
    [Test]
    public void ConfiguracionCompletaEsValida()
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.TDM,
            "TdmTestScene",
            300f,
            20,
            new[] { 2, 4, 6, 8 }
        );

        try
        {
            Assert.That(definition.IsValid(out string error), Is.True, error);
            Assert.That(definition.AllowsPlayerCount(2), Is.True);
            Assert.That(definition.AllowsPlayerCount(4), Is.True);
            Assert.That(definition.AllowsPlayerCount(5), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void ModoSinEquiposAceptaUnJugador()
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.Race,
            "RaceTestScene",
            120f,
            1,
            new[] { 1 }
        );

        try
        {
            SetField(definition, "requiresTeams", false);
            Assert.That(definition.IsValid(out string error), Is.True, error);
            Assert.That(definition.AllowsPlayerCount(1), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void DefinicionSinModoOEscenaEsInvalida()
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.None,
            string.Empty,
            300f,
            20,
            new[] { 4 }
        );

        try
        {
            Assert.That(definition.IsValid(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [TestCase(0f, 20)]
    [TestCase(-1f, 20)]
    [TestCase(300f, 0)]
    [TestCase(300f, -1)]
    public void DuracionYPuntuacionDebenSerPositivas(
        float duration,
        int scoreLimit)
    {
        GameModeDefinition definition = CreateDefinition(
            GameModeId.CTF,
            "CtfTestScene",
            duration,
            scoreLimit,
            new[] { 4, 6, 8 }
        );

        try
        {
            Assert.That(definition.IsValid(out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void CantidadesInvalidasSeDetectan()
    {
        int[][] invalidCounts =
        {
            new[] { 3, 4 },
            new[] { 4, 4 },
            new int[0]
        };

        foreach (int[] playerCounts in invalidCounts)
        {
            GameModeDefinition definition = CreateDefinition(
                GameModeId.TDM,
                "TdmTestScene",
                300f,
                20,
                playerCounts
            );

            try
            {
                Assert.That(definition.IsValid(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }

    private static GameModeDefinition CreateDefinition(
        GameModeId gameModeId,
        string sceneName,
        float durationSeconds,
        int scoreLimit,
        int[] allowedPlayerCounts)
    {
        GameModeDefinition definition =
            ScriptableObject.CreateInstance<GameModeDefinition>();

        SetField(definition, "gameModeId", gameModeId);
        SetField(definition, "sceneName", sceneName);
        SetField(definition, "durationSeconds", durationSeconds);
        SetField(definition, "scoreLimit", scoreLimit);
        SetField(definition, "allowedPlayerCounts", allowedPlayerCounts);
        return definition;
    }

    private static void SetField<T>(
        GameModeDefinition definition,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(GameModeDefinition).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null, $"No existe el campo {fieldName}.");
        field.SetValue(definition, value);
    }
}
