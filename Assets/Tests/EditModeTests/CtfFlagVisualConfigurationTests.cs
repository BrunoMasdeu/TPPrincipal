using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CtfFlagVisualConfigurationTests
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/ctf_scene_prueba.unity",
        "Assets/Scenes/ArenaIndustrial.unity"
    };

    [TestCaseSource(nameof(ScenePaths))]
    public void EscenaCtfTieneUnControladorVisualConfigurado(string scenePath)
    {
        WithScene(scenePath, scene =>
        {
            CtfFlagVisualController[] controllers =
                FindInScene<CtfFlagVisualController>(scene);

            Assert.That(controllers, Has.Length.EqualTo(1),
                $"{scenePath} necesita un único controlador visual CTF.");
            Assert.That(controllers[0].HasRequiredPrefabs, Is.True,
                $"{scenePath} no tiene asignados ambos prefabs de bandera.");
        });
    }

    [TestCaseSource(nameof(ScenePaths))]
    public void EscenaCtfTieneUnSpawnDeBanderaPorEquipo(string scenePath)
    {
        WithScene(scenePath, scene =>
        {
            FlagSpawnPoint[] spawnPoints = FindInScene<FlagSpawnPoint>(scene);
            int redCount = 0;
            int blueCount = 0;

            foreach (FlagSpawnPoint spawnPoint in spawnPoints)
            {
                if (spawnPoint.TeamId == TeamId.Red)
                    redCount++;
                else if (spawnPoint.TeamId == TeamId.Blue)
                    blueCount++;
            }

            Assert.That(redCount, Is.EqualTo(1));
            Assert.That(blueCount, Is.EqualTo(1));
        });
    }

    private static void WithScene(
        string scenePath,
        System.Action<Scene> assertion)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;

        if (openedForTest)
        {
            scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            assertion(scene);
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));

        return results.ToArray();
    }
}
