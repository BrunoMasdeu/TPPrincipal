using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SinglePlayerFlowTests
{
    private const string RespawnPrefabPath =
        "Assets/Scenes/llegada_respawn/Respawn.prefab";
    private const string TutorialScenePath =
        "Assets/Scenes/CentroEntrenamiento.unity";
    private const string ParkourScenePath =
        "Assets/Scenes/GameScene.unity";
    private const string RaceDefinitionPath =
        "Assets/Settings/GameModes/RaceGameModeDefinition.asset";

    [Test]
    public void RespawnDelTutorialAdmiteJugadorSinEquipo()
    {
        GameObject respawn =
            AssetDatabase.LoadAssetAtPath<GameObject>(RespawnPrefabPath);

        Assert.That(respawn, Is.Not.Null);

        TeamSpawnPoint spawnPoint = respawn.GetComponent<TeamSpawnPoint>();
        Assert.That(spawnPoint, Is.Not.Null,
            "El punto Respawn debe participar del spawner de red.");
        Assert.That(spawnPoint.TeamId, Is.EqualTo(TeamId.None),
            "Single player necesita un spawn sin equipo.");
    }

    [Test]
    public void RaceCargaElMapaDeParkour()
    {
        GameModeDefinition race =
            AssetDatabase.LoadAssetAtPath<GameModeDefinition>(
                RaceDefinitionPath);

        Assert.That(race, Is.Not.Null);
        Assert.That(race.GameModeId, Is.EqualTo(GameModeId.Race));
        Assert.That(race.SceneName, Is.EqualTo("GameScene"),
            "Race debe cargar el mapa de parkour, no el tutorial.");
    }

    [Test]
    public void TutorialNoContieneGameManagerDeCarrera()
    {
        Scene scene = EditorSceneManager.OpenScene(
            TutorialScenePath,
            OpenSceneMode.Additive);

        try
        {
            Assert.That(
                CountComponentsInScene<GameManager>(scene),
                Is.Zero,
                "CentroEntrenamiento no debe tener cronómetro ni reglas Race."
            );
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MapaParkourConservaGameManagerDeCarrera()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ParkourScenePath,
            OpenSceneMode.Additive);

        try
        {
            Assert.That(
                CountComponentsInScene<GameManager>(scene),
                Is.EqualTo(1),
                "GameScene debe conservar un único GameManager de Race."
            );
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int CountComponentsInScene<T>(Scene scene)
        where T : Component
    {
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            count += roots[i].GetComponentsInChildren<T>(true).Length;

        return count;
    }
}
