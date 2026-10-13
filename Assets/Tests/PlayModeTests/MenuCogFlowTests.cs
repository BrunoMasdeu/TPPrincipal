using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class MenuCogFlowTests
{
    private Mouse testMouse;
    private Keyboard testKeyboard;

    [UnitySetUp]
    public IEnumerator LoadMenu()
    {
        // Batch Mode no siempre expone dispositivos físicos al Input System.
        if (Mouse.current == null)
            testMouse = InputSystem.AddDevice<Mouse>();
        if (Keyboard.current == null)
            testKeyboard = InputSystem.AddDevice<Keyboard>();
        yield return SceneManager.LoadSceneAsync("MenuScene");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator CloseSession()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network != null)
        {
            network.Shutdown();
            yield return null;
            Object.Destroy(network.gameObject);
            yield return null;
        }
        yield return SceneManager.LoadSceneAsync("MenuScene");
        if (testMouse != null)
            InputSystem.RemoveDevice(testMouse);
        if (testKeyboard != null)
            InputSystem.RemoveDevice(testKeyboard);
        testMouse = null;
        testKeyboard = null;
    }

    [UnityTest]
    public IEnumerator EmptyJoinCodeShowsErrorAndBackReturnsToMainMenu()
    {
        ConnectionManager connection = Object.FindFirstObjectByType<ConnectionManager>();
        Transform menu = connection.transform;
        Assert.That(menu.Find("Canv_Main").gameObject.activeSelf, Is.True);
        menu.Find("Canv_Main/HorizontalLayout/Btn_Play").GetComponent<Button>().onClick.Invoke();
        Assert.That(menu.Find("Canv_Jugar").gameObject.activeSelf, Is.True);
        menu.Find("Canv_Jugar/Panel_Connection/Btn_Join").GetComponent<Button>().onClick.Invoke();
        Assert.That(menu.Find("Canv_Jugar/Panel_Connection/Txt_ConnectionStatus").GetComponent<TMP_Text>().text,
            Is.EqualTo("Ingresá el código de la sala"));
        Assert.That(connection.IsConnecting, Is.False);
        menu.Find("Canv_Jugar/Panel_Connection/Btn_Back").GetComponent<Button>().onClick.Invoke();
        Assert.That(menu.Find("Canv_Main").gameObject.activeSelf, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LobbyControlsReflectAuthoritativeStateAndCancelReenablesConnection()
    {
        ConnectionManager connection = Object.FindFirstObjectByType<ConnectionManager>();
        Transform menu = connection.transform;
        NetworkManager network = NetworkManager.Singleton;
        Assert.That(network.GetComponent<NetworkSessionBootstrap>().PrepareRoom(out string error), Is.True, error);
        network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
        Assert.That(network.StartHost(), Is.True);
        connection.GetComponent<MenuManager>().ShowLobbyMenu();
        yield return null;
        yield return null;
        NetworkLobbySession session = NetworkLobbySession.Instance;
        Assert.That(session, Is.Not.Null);
        menu.Find("Canv_Lobby/Panel_Conf/Btn_TDM").GetComponent<Button>().onClick.Invoke();
        menu.Find("Canv_Lobby/Panel_Conf/Btn_Count2").GetComponent<Button>().onClick.Invoke();
        Assert.That(session.SelectedGameModeId, Is.EqualTo(GameModeId.TDM));
        Assert.That(session.SelectedPlayerCount, Is.EqualTo(2));
        Assert.That(menu.Find("Canv_Lobby/Panel_Seleccion/PanelMapa/MapName").GetComponent<TMP_Text>().text,
            Is.EqualTo(session.SelectedMapDefinition.DisplayName));
        menu.Find("Canv_Lobby/Panel_Seleccion/Button").GetComponent<Button>().onClick.Invoke();
        Assert.That(session.SelectedMapId, Is.EqualTo(MapId.ArenaIndustrial));
        menu.Find("Canv_Lobby/Panel_EquipoAzul/Selectable").GetComponent<Button>().onClick.Invoke();
        menu.Find("Canv_Lobby/Btn_Ready").GetComponent<Button>().onClick.Invoke();
        Assert.That(session.TryGetPlayerData(network.LocalClientId, out LobbyPlayerData player), Is.True);
        Assert.That(player.TeamId, Is.EqualTo(TeamId.Blue));
        Assert.That(player.IsReady, Is.True);
        Assert.That(menu.Find("Canv_Lobby/Btn_StartGame").GetComponent<Button>().interactable, Is.False,
            "Una partida de dos jugadores no debe iniciarse con uno solo.");
        menu.Find("Canv_Lobby/Panel_Conf/Btn_CTF").GetComponent<Button>().onClick.Invoke();
        session.TryGetPlayerData(network.LocalClientId, out player);
        Assert.That(player.IsReady, Is.False, "Cambiar modo requiere confirmar listo otra vez.");
        Assert.That(menu.Find("Canv_Lobby/Btn_Ready/Label").GetComponent<TMP_Text>().text, Is.EqualTo("ESTOY LISTO"));
        menu.Find("Canv_Lobby/Btn_Cancel").GetComponent<Button>().onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(menu.Find("Canv_Jugar").gameObject.activeSelf, Is.True);
        Assert.That(menu.Find("Canv_Jugar/Panel_Connection/Btn_Host").GetComponent<Button>().interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator TrainingButtonLoadsLocalGameWithoutUnityServices()
    {
        ConnectionManager connection = Object.FindFirstObjectByType<ConnectionManager>();
        connection.GetComponent<MenuManager>().Play();
        connection.transform.Find("Canv_Jugar/Btn_SinglePlayer").GetComponent<Button>().onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 20;
        while ((SceneManager.GetActiveScene().name != "CentroEntrenamiento" ||
                NetworkLobbySession.Instance == null ||
                NetworkManager.Singleton.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CentroEntrenamiento"));
        Assert.That(NetworkManager.Singleton.IsHost, Is.True);
        Assert.That(NetworkLobbySession.Instance.SelectedGameModeId, Is.EqualTo(GameModeId.None));
        Assert.That(NetworkLobbySession.Instance.SelectedPlayerCount, Is.EqualTo(0));
        Assert.That(NetworkLobbySession.Instance.Phase, Is.EqualTo(SessionPhase.Lobby));
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject, Is.Not.Null);
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerHudPresenter>().MatchHudInstance,
            Is.Null, "CanvasTDM sólo pertenece a partidas de combate.");
    }

    [UnityTest]
    public IEnumerator EasterEggDoorStartsRaceOnlyAfterTutorialCompletion()
    {
        ConnectionManager connection =
            Object.FindFirstObjectByType<ConnectionManager>();
        connection.GetComponent<MenuManager>().Play();
        connection.transform.Find("Canv_Jugar/Btn_SinglePlayer")
            .GetComponent<Button>().onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + 20f;
        while ((SceneManager.GetActiveScene().name != "CentroEntrenamiento" ||
                NetworkLobbySession.Instance == null ||
                NetworkManager.Singleton.LocalClient.PlayerObject == null) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(SceneManager.GetActiveScene().name,
            Is.EqualTo("CentroEntrenamiento"));
        TutorialManager tutorial =
            Object.FindFirstObjectByType<TutorialManager>();
        EasterEggDoor door =
            Object.FindFirstObjectByType<EasterEggDoor>();
        Assert.That(tutorial, Is.Not.Null);
        Assert.That(door, Is.Not.Null);
        Assert.That(tutorial.IsCompleted, Is.False);

        FieldInfo nearbyField = typeof(EasterEggDoor).GetField(
            "playerIsNearby", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(nearbyField, Is.Not.Null);
        nearbyField.SetValue(door, true);
        Assert.That(door.TryEnterRace(), Is.False,
            "La puerta no debe iniciar Race antes de completar el tutorial.");
        Assert.That(NetworkLobbySession.Instance.TryStartLocalRaceFromTutorial(
            "GameScene", out _), Is.False,
            "La sesión tampoco debe permitir saltarse el tutorial.");
        Assert.That(NetworkLobbySession.Instance.SelectedGameModeId,
            Is.EqualTo(GameModeId.None));

        FieldInfo stepsField = typeof(TutorialManager).GetField(
            "tutorialSteps", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo indexField = typeof(TutorialManager).GetField(
            "currentStepIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(stepsField, Is.Not.Null);
        Assert.That(indexField, Is.Not.Null);
        IList steps = (IList)stepsField.GetValue(tutorial);
        Assert.That(steps.Count, Is.GreaterThan(0));
        indexField.SetValue(tutorial, steps.Count);
        Assert.That(tutorial.IsCompleted, Is.True);
        Assert.That(door.TryEnterRace(), Is.True);

        deadline = Time.realtimeSinceStartup + 25f;
        while ((SceneManager.GetActiveScene().name != "GameScene" ||
                NetworkLobbySession.Instance == null ||
                NetworkLobbySession.Instance.Phase != SessionPhase.InMatch ||
                NetworkManager.Singleton.LocalClient.PlayerObject == null ||
                NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.scene.name !=
                    "GameScene") && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("GameScene"));
        Assert.That(NetworkLobbySession.Instance.SelectedGameModeId,
            Is.EqualTo(GameModeId.Race));
        Assert.That(NetworkLobbySession.Instance.SelectedPlayerCount,
            Is.EqualTo(1));
        Assert.That(NetworkLobbySession.Instance.Phase,
            Is.EqualTo(SessionPhase.InMatch));
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.scene.name,
            Is.EqualTo("GameScene"));
        Assert.That(Object.FindFirstObjectByType<GameManager>(), Is.Not.Null);
        Assert.That(Object.FindFirstObjectByType<RaceStatusUI>(), Is.Not.Null);
    }
}
