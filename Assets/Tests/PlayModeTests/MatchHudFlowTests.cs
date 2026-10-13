#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MatchHudFlowTests
{
    private Mouse mouse;
    private Keyboard keyboard;
    private NetworkManager network;
    private NetworkLobbySession session;
    private GameObject player;
    private GameObject remote;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        mouse = InputSystem.AddDevice<Mouse>();
        keyboard = InputSystem.AddDevice<Keyboard>();
        yield return SceneManager.LoadSceneAsync("MenuScene");
        network = NetworkManager.Singleton;
        network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
        Assert.That(network.GetComponent<NetworkSessionBootstrap>().PrepareRoom(out string error), Is.True, error);
        Assert.That(network.StartHost(), Is.True);
        yield return null;
        session = NetworkLobbySession.Instance;
        session.RequestGameModeChange(GameModeId.TDM);
        session.RequestPlayerCountChange(2);
        session.RequestTeamChange(TeamId.Red);
        // Probar el enlace del HUD sin crear un segundo proceso de juego.
        Variable<SessionPhase>(session, "sessionPhase").Value = SessionPhase.InMatch;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
        player = Object.Instantiate(asset);
        player.GetComponent<NetworkObject>().SpawnWithOwnership(network.LocalClientId);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (network != null)
        {
            network.Shutdown();
            while (network != null && network.ShutdownInProgress)
                yield return null;
            if (network != null) Object.Destroy(network.gameObject);
        }
        if (player != null) Object.Destroy(player);
        if (remote != null) Object.Destroy(remote);
        yield return null;
        yield return SceneManager.LoadSceneAsync("MenuScene");
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    [UnityTest]
    public IEnumerator OwnerHudReceivesScoreMoneyStatisticsAndResultEvents()
    {
        MatchHudPresenter hud = player.GetComponent<NetworkPlayerHudPresenter>().MatchHudInstance;
        Assert.That(hud, Is.Not.Null);
        TdmMatchManager tdm = session.GetComponent<TdmMatchManager>();
        Variable<int>(tdm, "redEliminations").Value = 7;
        Assert.That(Text(hud, "PanelSuperior/EquipoRojoScore/Puntos"), Is.EqualTo("7"));
        Variable<int>(player.GetComponent<NetworkPlayerCombat>(), "money").Value = 1234;
        Assert.That(Text(hud, "DineroTxt"), Is.EqualTo("$1234"));
        var statistics = (NetworkList<PlayerMatchStats>)typeof(TdmMatchManager)
            .GetField("playerStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tdm);
        statistics.Add(new PlayerMatchStats(network.LocalClientId, TeamId.Red, 4, 2));
        Assert.That(Text(hud, "TabPanel/StatsRojoContainer (1)/PlayerRow0/KillsTxt"), Is.EqualTo("4"));
        NetworkMatchManager match = session.GetComponent<NetworkMatchManager>();
        Variable<MatchResultData>(match, "result").Value = new MatchResultData(TeamId.Blue, MatchEndReason.ScoreLimit, false);
        Variable<MatchPhase>(match, "phase").Value = MatchPhase.Finished;
        yield return null;
        Assert.That(Text(hud, "PanelResultadoPartida/PanelResultado/Txt_ResultTitle"), Is.EqualTo("VICTORIA DEL EQUIPO AZUL"));
        Assert.That(hud.transform.Find("PanelResultadoPartida").gameObject.activeSelf, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(player.GetComponent<PlayerNetworkSetup>().LocalGameplayInputEnabled, Is.False);
        // El botón de salida sobrevive al despawn que provoca Shutdown.
        hud.transform.Find("PanelResultadoPartida/PanelResultado/ButtonReturnMenu").GetComponent<Button>().onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 10;
        while (Object.FindFirstObjectByType<MatchHudPresenter>() != null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<MatchHudPresenter>(), Is.Null);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MenuScene"));
        Assert.That(NetworkManager.Singleton.IsListening, Is.False);
    }

    [UnityTest]
    public IEnumerator RemotePlayerHasNoMatchHudAndOwnerDespawnCleansUp()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
        remote = Object.Instantiate(asset);
        remote.GetComponent<NetworkObject>().SpawnWithOwnership(999);
        yield return null;
        Assert.That(remote.GetComponent<NetworkPlayerHudPresenter>().MatchHudInstance, Is.Null);
        Assert.That(Object.FindObjectsByType<MatchHudPresenter>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        player.GetComponent<NetworkObject>().Despawn();
        yield return null;
        Assert.That(Object.FindObjectsByType<MatchHudPresenter>(FindObjectsSortMode.None), Is.Empty);
    }

    private static string Text(MatchHudPresenter hud, string path) => hud.transform.Find(path).GetComponent<TMP_Text>().text;
    private static NetworkVariable<T> Variable<T>(object owner, string field) =>
        (NetworkVariable<T>)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
}
#endif
