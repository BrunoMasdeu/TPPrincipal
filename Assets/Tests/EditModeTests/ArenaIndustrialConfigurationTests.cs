using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ArenaIndustrialConfigurationTests
{
    private const string ArenaScenePath =
        "Assets/Scenes/ArenaIndustrial.unity";

    [Test]
    public void EscenasMultijugadorEstanRegistradasEnBuildProfiles()
    {
        string[] enabledScenePaths = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        Assert.That(enabledScenePaths, Does.Contain(ArenaScenePath));
        Assert.That(
            enabledScenePaths,
            Does.Contain("Assets/Scenes/tdm_scene_prueba.unity")
        );
        Assert.That(
            enabledScenePaths,
            Does.Contain("Assets/Scenes/ctf_scene_prueba.unity")
        );
    }

    [Test]
    public void ArenaNoContieneSistemasLocalesDuplicados()
    {
        WithArena(scene =>
        {
            Assert.That(FindInScene<Camera>(scene), Is.Empty);
            Assert.That(FindInScene<AudioListener>(scene), Is.Empty);
            Assert.That(FindInScene<NetworkManager>(scene), Is.Empty);
            Assert.That(FindInScene<EventSystem>(scene), Is.Empty);
        });
    }

    [Test]
    public void ArenaTieneCuatroSpawnsPorEquipo()
    {
        WithArena(scene =>
        {
            TeamSpawnPoint[] spawnPoints = FindInScene<TeamSpawnPoint>(scene);

            Assert.That(
                spawnPoints.Count(point => point.TeamId == TeamId.Red),
                Is.EqualTo(4),
                "Arena Industrial necesita cuatro spawns rojos."
            );
            Assert.That(
                spawnPoints.Count(point => point.TeamId == TeamId.Blue),
                Is.EqualTo(4),
                "Arena Industrial necesita cuatro spawns azules."
            );
            Assert.That(
                spawnPoints.Count(point => point.TeamId == TeamId.None),
                Is.Zero,
                "El mapa de equipos no debe tener spawns TeamId.None."
            );
        });
    }

    [Test]
    public void ArenaTieneUnaBaseYUnaBanderaPorEquipo()
    {
        WithArena(scene =>
        {
            FlagSpawnPoint[] flags = FindInScene<FlagSpawnPoint>(scene);
            CtfBaseZone[] zones = FindInScene<CtfBaseZone>(scene);

            Assert.That(
                flags.Count(point => point.TeamId == TeamId.Red),
                Is.EqualTo(1)
            );
            Assert.That(
                flags.Count(point => point.TeamId == TeamId.Blue),
                Is.EqualTo(1)
            );
            Assert.That(
                zones.Count(zone => zone.TeamId == TeamId.Red),
                Is.EqualTo(1)
            );
            Assert.That(
                zones.Count(zone => zone.TeamId == TeamId.Blue),
                Is.EqualTo(1)
            );

            foreach (CtfBaseZone zone in zones)
            {
                BoxCollider collider = zone.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null, $"{zone.name} necesita BoxCollider.");
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.isTrigger, Is.True,
                    $"{zone.name} debe tener Is Trigger activado.");
            }
        });
    }

    private static void WithArena(System.Action<Scene> assertion)
    {
        Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;

        if (openedForTest)
        {
            scene = EditorSceneManager.OpenScene(
                ArenaScenePath,
                OpenSceneMode.Additive
            );
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
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            results.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return results.ToArray();
    }
}
