using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Sprint3ConfigurationTests
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string NetworkPlayerPath = "Assets/Personajes-objetos/Jugador.prefab";

    private readonly List<Scene> openedScenes = new List<Scene>();
    private readonly List<GameObject> instances = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject instance in instances)
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
        }

        instances.Clear();

        foreach (Scene scene in openedScenes)
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        openedScenes.Clear();
    }

    [Test]
    [Category("TST_S3_003")]
    public void TST_S3_003_SesionHostClientTieneEscenasRedYPrefabConfigurados()
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        CollectionAssert.Contains(enabledScenes, MenuScenePath);
        CollectionAssert.Contains(enabledScenes, GameScenePath);

        Scene menuScene = OpenScene(MenuScenePath);
        NetworkManager[] managers = FindComponentsInScene<NetworkManager>(menuScene);

        Assert.That(
            managers.Length,
            Is.EqualTo(1),
            "MenuScene debe contener exactamente un NetworkManager."
        );

        bool hasTransport = managers[0]
            .GetComponents<MonoBehaviour>()
            .Any(component => component != null && component.GetType().Name == "UnityTransport");

        Assert.IsTrue(hasTransport, "El NetworkManager no tiene UnityTransport configurado.");

        LobbyPlayerSpawner spawner = FindComponentInScene<LobbyPlayerSpawner>(menuScene);
        Assert.IsNotNull(spawner, "MenuScene no contiene LobbyPlayerSpawner.");

        GameObject configuredPrefab = GetObjectReference<GameObject>(
            spawner,
            "networkPlayerPrefab"
        );

        Assert.AreEqual(
            LoadNetworkPlayerPrefab(),
            configuredPrefab,
            "LobbyPlayerSpawner no usa el prefab NetworkPlayer esperado."
        );
    }

    [Test]
    [Category("TST_S3_004")]
    public void TST_S3_004_SpawnUsaPuntoValidoSeparacionYOwnershipDeJugador()
    {
        GameObject prefab = LoadNetworkPlayerPrefab();

        Assert.IsNotNull(prefab.GetComponent<NetworkObject>());
        Assert.IsNotNull(prefab.GetComponent<Rigidbody>());
        Assert.IsNotNull(prefab.GetComponent<Collider>());
        Assert.IsNotNull(prefab.GetComponent<PlayerNetworkSetup>());

        Scene menuScene = OpenScene(MenuScenePath);
        LobbyPlayerSpawner spawner = FindComponentInScene<LobbyPlayerSpawner>(menuScene);
        Assert.IsNotNull(spawner);

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        string spawnPointName = RequireProperty(
            serializedSpawner,
            "spawnPointName"
        ).stringValue;
        float playerSpacing = RequireProperty(
            serializedSpawner,
            "playerSpacing"
        ).floatValue;

        Assert.That(spawnPointName, Is.EqualTo("Respawn"));
        Assert.That(
            playerSpacing,
            Is.GreaterThan(0f),
            "Dos jugadores recibirían exactamente la misma posición de spawn."
        );

        Scene gameScene = OpenScene(GameScenePath);
        GameObject spawn = FindGameObjectInScene(gameScene, spawnPointName);

        Assert.IsNotNull(spawn, "GameScene no contiene el punto de spawn configurado.");
        Assert.That(spawn.transform.position.y, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    [Category("TST_S3_006")]
    public void TST_S3_006_NetworkTransformSincronizaLosTresEjesDePosicion()
    {
        SerializedObject serializedTransform = GetSerializedNetworkTransform();

        AssertSerializedBool(serializedTransform, "SyncPositionX");
        AssertSerializedBool(serializedTransform, "SyncPositionY");
        AssertSerializedBool(serializedTransform, "SyncPositionZ");
        AssertSerializedBool(serializedTransform, "Interpolate");
    }

    [Test]
    [Category("TST_S3_007")]
    public void TST_S3_007_NetworkTransformSincronizaLosTresEjesDeRotacion()
    {
        SerializedObject serializedTransform = GetSerializedNetworkTransform();

        AssertSerializedBool(serializedTransform, "SyncRotAngleX");
        AssertSerializedBool(serializedTransform, "SyncRotAngleY");
        AssertSerializedBool(serializedTransform, "SyncRotAngleZ");
        AssertSerializedBool(serializedTransform, "Interpolate");
    }

    [Test]
    [Category("TST_S3_009")]
    public void TST_S3_009_SaltoDelPrefabTieneFisicaSueloYSincronizacionVertical()
    {
        GameObject prefab = LoadNetworkPlayerPrefab();
        Move move = prefab.GetComponent<Move>();

        Assert.IsNotNull(move, "NetworkPlayer no tiene el controlador Move.");
        Assert.IsNotNull(move.groundCheck, "Move no tiene GroundCheck configurado.");
        Assert.That(move.jumpForce, Is.GreaterThan(0f));
        Assert.IsNotNull(prefab.GetComponent<Rigidbody>());
        AssertSerializedBool(GetSerializedNetworkTransform(), "SyncPositionY");
    }

    [Test]
    [Category("TST_S3_010")]
    public void TST_S3_010_CaidaYRespawnConservanUnJugadorSincronizable()
    {
        Scene gameScene = OpenScene(GameScenePath);
        ZonaMuerte deathZone = FindComponentInScene<ZonaMuerte>(gameScene);

        Assert.IsNotNull(deathZone, "GameScene no contiene ZonaMuerte.");
        Assert.IsNotNull(
            deathZone.puntoDeRespawn,
            "ZonaMuerte no tiene punto de respawn configurado."
        );

        GameObject prefab = LoadNetworkPlayerPrefab();
        Assert.IsNotNull(prefab.GetComponent<NetworkObject>());
        Assert.IsNotNull(prefab.GetComponent<NetworkTransform>());
        Assert.IsNotNull(prefab.GetComponent<Rigidbody>());
        AssertSerializedBool(GetSerializedNetworkTransform(), "SyncPositionY");
    }

    [Test]
    [Category("TST_S3_011")]
    public void TST_S3_011_UnJugadorRemotoNoMantieneControlesLocalesActivos()
    {
        GameObject instance = Object.Instantiate(LoadNetworkPlayerPrefab());
        instances.Add(instance);

        PlayerNetworkSetup setup = instance.GetComponent<PlayerNetworkSetup>();
        Assert.IsNotNull(setup);

        SerializedObject serializedSetup = new SerializedObject(setup);
        string[] localOnlyFields =
        {
            "playerCamera",
            "audioListener",
            "cameraMovement",
            "move",
            "wallRun",
            "grappling"
        };

        foreach (string fieldName in localOnlyFields)
        {
            Object reference = RequireProperty(
                serializedSetup,
                fieldName
            ).objectReferenceValue;

            Assert.IsNotNull(reference, $"PlayerNetworkSetup no tiene asignado {fieldName}.");
        }

        Assert.IsFalse(setup.IsOwner, "El fixture sin spawn debe representar un jugador remoto.");
        setup.OnNetworkSpawn();

        foreach (string fieldName in localOnlyFields)
        {
            Behaviour behaviour = RequireProperty(
                serializedSetup,
                fieldName
            ).objectReferenceValue as Behaviour;

            Assert.IsNotNull(behaviour);
            Assert.IsFalse(
                behaviour.enabled,
                $"El componente local {fieldName} quedó activo en un jugador sin ownership."
            );
        }

        SerializedProperty authorityMode = RequireProperty(
            GetSerializedNetworkTransform(),
            "AuthorityMode"
        );

        Assert.That(
            authorityMode.enumValueIndex,
            Is.EqualTo(1),
            "NetworkTransform debe usar autoridad del propietario para impedir control ajeno."
        );
    }

    [Test]
    [Category("TST_S3_012")]
    public void TST_S3_012_ElEscenarioDeclaraEstadoCompartidoSincronizable()
    {
        Scene gameScene = OpenScene(GameScenePath);
        NetworkObject[] sharedCandidates = FindComponentsInScene<NetworkObject>(gameScene)
            .Where(networkObject => networkObject.GetComponent<PlayerNetworkSetup>() == null)
            .ToArray();

        Assert.That(
            sharedCandidates,
            Is.Not.Empty,
            "No existe en GameScene ningún elemento compartido/exclusivo con NetworkObject; " +
            "el caso de concurrencia no puede garantizar convergencia ni exclusividad."
        );

        bool hasSynchronizedState = sharedCandidates.Any(
            networkObject => networkObject
                .GetComponents<NetworkBehaviour>()
                .Any(behaviour => !(behaviour is PlayerNetworkSetup))
        );

        Assert.IsTrue(
            hasSynchronizedState,
            "Los NetworkObject compartidos no exponen un NetworkBehaviour que sincronice su estado."
        );
    }

    [Test]
    [Category("TST_S3_028")]
    public void TST_S3_028_PrefabVisualConservaColliderOrientacionYGameplay()
    {
        GameObject prefab = LoadNetworkPlayerPrefab();
        Move move = prefab.GetComponent<Move>();
        Rigidbody body = prefab.GetComponent<Rigidbody>();

        Assert.IsNotNull(move);
        Assert.IsNotNull(body);
        Assert.IsNotNull(prefab.GetComponent<Collider>());
        Assert.IsNotNull(move.orientation);
        Assert.IsNotNull(move.groundCheck);
        Assert.IsNotNull(prefab.GetComponent<Grappling>());
        Assert.IsNotNull(prefab.GetComponent<WallRun>());
        Assert.That(
            prefab.GetComponentsInChildren<Renderer>(true),
            Is.Not.Empty,
            "El cambio visual dejó al prefab sin representación renderizable."
        );
    }

    [Test]
    [Category("TST_S3_030")]
    public void TST_S3_030_WorkflowEjecutaEditPlayModeYArchivaResultados()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string workflowPath = Path.Combine(projectRoot, ".github", "workflows", "unity-tests.yml");

        Assert.IsTrue(File.Exists(workflowPath), "No existe .github/workflows/unity-tests.yml.");

        string workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains("develop", workflow);
        StringAssert.Contains("playmode", workflow);
        StringAssert.Contains("editmode", workflow);
        StringAssert.Contains("game-ci/unity-test-runner@v4", workflow);
        StringAssert.Contains("actions/upload-artifact@v4", workflow);
        StringAssert.Contains("if: always()", workflow);
    }

    private Scene OpenScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        openedScenes.Add(scene);
        return scene;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        return FindComponentsInScene<T>(scene).FirstOrDefault();
    }

    private static T[] FindComponentsInScene<T>(Scene scene) where T : Component
    {
        return scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        return scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == objectName);
    }

    private static GameObject LoadNetworkPlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath);
        Assert.IsNotNull(prefab, $"No se encontró {NetworkPlayerPath}.");
        return prefab;
    }

    private static SerializedObject GetSerializedNetworkTransform()
    {
        NetworkTransform networkTransform = LoadNetworkPlayerPrefab()
            .GetComponent<NetworkTransform>();

        Assert.IsNotNull(networkTransform, "NetworkPlayer no tiene NetworkTransform.");
        return new SerializedObject(networkTransform);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.IsNotNull(
            property,
            $"No se encontró la propiedad serializada {propertyName} en " +
            $"{serializedObject.targetObject.name}."
        );
        return property;
    }

    private static T GetObjectReference<T>(Object target, string propertyName)
        where T : Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        return RequireProperty(serializedObject, propertyName).objectReferenceValue as T;
    }

    private static void AssertSerializedBool(
        SerializedObject serializedObject,
        string propertyName)
    {
        Assert.IsTrue(
            RequireProperty(serializedObject, propertyName).boolValue,
            $"NetworkTransform debe tener {propertyName} activado."
        );
    }
}
