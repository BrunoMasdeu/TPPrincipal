using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class MenuCogIntegrationTests
{
    private const string PrefabPath = "Assets/Prefabs/MenuCOG.prefab";
    private GameObject menu;

    [SetUp]
    public void SetUp()
    {
        menu = PrefabUtility.LoadPrefabContents(PrefabPath);
    }

    [TearDown]
    public void TearDown()
    {
        PrefabUtility.UnloadPrefabContents(menu);
    }

    [Test]
    public void PrefabHasAllConnectionAndLobbyReferences()
    {
        ConnectionManager connection = menu.GetComponent<ConnectionManager>();
        Assert.That(connection, Is.Not.Null);
        var fields = new SerializedObject(connection);
        foreach (string name in new[] {
            "menuManager", "joinCodeInput", "connectionPanel", "lobbyPanel",
            "codeDisplay", "statusDisplay", "playerCountDisplay", "startGameButton",
            "hostButton", "joinButton", "singlePlayerButton", "connectionStatusDisplay",
            "tdmButton", "ctfButton", "configurationDisplay", "nextMapButton",
            "mapNameDisplay", "mapDescriptionDisplay", "mapPreview", "redTeamButton",
            "blueTeamButton", "redTeamDisplay", "blueTeamDisplay", "unassignedPlayersDisplay",
            "readyButton", "lobbyHintDisplay" })
        {
            Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }
        SerializedProperty counts = fields.FindProperty("playerCountControls");
        Assert.That(counts.arraySize, Is.EqualTo(4));
        for (int i = 0; i < counts.arraySize; i++)
        {
            Assert.That(counts.GetArrayElementAtIndex(i).FindPropertyRelative("playerCount").intValue,
                Is.EqualTo((i + 1) * 2));
            Assert.That(counts.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue,
                Is.Not.Null);
        }
    }

    [Test]
    public void EveryEnabledButtonHasValidPersistentTarget()
    {
        foreach (Button button in menu.GetComponentsInChildren<Button>(true).Where(b => b.interactable))
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.GreaterThan(0), button.name);
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                Assert.That(button.onClick.GetPersistentTarget(i), Is.Not.Null, button.name);
        }
    }

    [Test]
    public void NavigationShowsOneCanvasAndCancelRestoresConnectionMenu()
    {
        MenuManager navigation = menu.GetComponent<MenuManager>();
        Assert.That(navigation, Is.Not.Null);
        AssertCanvas("Canv_Main");
        navigation.Play();
        AssertCanvas("Canv_Jugar");
        navigation.ShowLobbyMenu();
        AssertCanvas("Canv_Lobby");
        menu.GetComponent<ConnectionManager>().CancelConnection();
        AssertCanvas("Canv_Jugar");
        navigation.CloseMultiplayer();
        AssertCanvas("Canv_Main");
        navigation.Settings();
        AssertCanvas("Canv_Options");
        navigation.ReturnMenu();
        AssertCanvas("Canv_Main");
    }

    [Test]
    public void MenuSceneUsesPrefabAndExistingNetworkBootstrap()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
            var connections = scene.GetRootGameObjects()
                .Where(root => root.activeInHierarchy)
                .SelectMany(root => root.GetComponentsInChildren<ConnectionManager>())
                .ToArray();
            Assert.That(connections.Length, Is.EqualTo(1));
            ConnectionManager connection = connections[0];
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(connection.gameObject),
                Is.EqualTo(PrefabPath));
            Assert.That(new SerializedObject(connection).FindProperty("sessionBootstrap").objectReferenceValue,
                Is.Not.Null);
            Assert.That(scene.GetRootGameObjects().Where(root => root.activeInHierarchy)
                .SelectMany(root => root.GetComponentsInChildren<MenuManager>()).Count(),
                Is.EqualTo(1));
        }
        finally
        {
            if (previous.Any(s => s.isLoaded && s.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private void AssertCanvas(string active)
    {
        foreach (Canvas canvas in menu.GetComponentsInChildren<Canvas>(true))
            Assert.That(canvas.gameObject.activeSelf, Is.EqualTo(canvas.name == active), canvas.name);
    }
}
