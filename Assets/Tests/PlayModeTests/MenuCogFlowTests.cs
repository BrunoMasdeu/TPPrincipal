using System.Collections;
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
                NetworkLobbySession.Instance.Phase != SessionPhase.InMatch) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CentroEntrenamiento"));
        Assert.That(NetworkManager.Singleton.IsHost, Is.True);
        Assert.That(NetworkLobbySession.Instance.SelectedGameModeId, Is.EqualTo(GameModeId.Race));
        Assert.That(NetworkLobbySession.Instance.SelectedPlayerCount, Is.EqualTo(1));
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject, Is.Not.Null);
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerHudPresenter>().MatchHudInstance,
            Is.Null, "CanvasTDM sólo pertenece a partidas de combate.");
    }
}
