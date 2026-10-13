using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private const string PlayerUiPrefabPath = "Assets/Prefabs/PlayerUI.prefab";
    private const string RedPlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string BluePlayerPrefabPath =
        "Assets/Prefabs/Survivalist (3).prefab";

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

    [Test]
    public void ResultadoRacePuedeRecibirClicksSobreElHud()
    {
        string[] hudPrefabPaths =
        {
            PlayerUiPrefabPath,
            RedPlayerPrefabPath,
            BluePlayerPrefabPath
        };

        foreach (string path in hudPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            PlayerHudView hud = prefab.GetComponentInChildren<PlayerHudView>(true);
            Assert.That(hud, Is.Not.Null, path);
            Assert.That(hud.GetComponent<GraphicRaycaster>().enabled,
                Is.False, $"El HUD de {path} no debe bloquear los botones de Race.");
        }

        GameObject playerUi = AssetDatabase.LoadAssetAtPath<GameObject>(
            PlayerUiPrefabPath);

        Scene scene = EditorSceneManager.OpenScene(
            ParkourScenePath, OpenSceneMode.Additive);

        try
        {
            RaceStatusUI status = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                status = root.GetComponentInChildren<RaceStatusUI>(true);
                if (status != null)
                    break;
            }

            Assert.That(status, Is.Not.Null);
            Canvas raceCanvas = status.GetComponent<Canvas>();
            Assert.That(raceCanvas, Is.Not.Null);
            Assert.That(raceCanvas.sortingOrder,
                Is.GreaterThan(playerUi.GetComponent<Canvas>().sortingOrder));

            int returnButtons = 0;
            foreach (Button button in status.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Btn_ReturnMenu")
                    continue;

                returnButtons++;
                Assert.That(button.interactable, Is.True);
                Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(status));
                Assert.That(button.onClick.GetPersistentMethodName(0),
                    Is.EqualTo(nameof(RaceStatusUI.VolverAlMenu)));
            }

            Assert.That(returnButtons, Is.EqualTo(2),
                "Victoria y derrota necesitan un botón para volver al menú.");
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
