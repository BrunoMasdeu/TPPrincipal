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
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HudIntegrationTests
{
    [TestFixture(TeamId.Red)]
    [TestFixture(TeamId.Blue)]
    public class MatchHudFlowTests
    {
        private readonly InputTestFixture input = new();
        private readonly TeamId localTeam;
        private Mouse mouse;
        private Keyboard keyboard;
        private NetworkManager network;
        private NetworkLobbySession session;
        private GameObject player;
        private GameObject remote;

        public MatchHudFlowTests(TeamId localTeam) => this.localTeam = localTeam;

        private GameObject TeamPlayerPrefab(TeamId team)
        {
            var spawner = new SerializedObject(session.GetComponent<NetworkPlayerSpawner>());
            return (GameObject)spawner.FindProperty(team == TeamId.Red
                ? "redTeamPlayerPrefab" : "blueTeamPlayerPrefab").objectReferenceValue;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input.Setup();
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
            session.RequestTeamChange(localTeam);
            // Probar el enlace del HUD sin crear un segundo proceso de juego.
            Variable<SessionPhase>(session, "sessionPhase").Value = SessionPhase.InMatch;
            GameObject asset = TeamPlayerPrefab(localTeam);
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
            input.TearDown();
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
            statistics.Add(new PlayerMatchStats(network.LocalClientId, localTeam, 4, 2));
            string rows = localTeam == TeamId.Red ? "StatsRojoContainer (1)" : "StatsAzulContainer";
            Assert.That(Text(hud, $"TabPanel/{rows}/PlayerRow0/KillsTxt"), Is.EqualTo("4"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            yield return null;
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard.tabKey.isPressed, Is.True);
            Assert.That(hud.transform.Find("TabPanel").gameObject.activeSelf, Is.True);
            Assert.That(Text(hud, $"TabPanel/{rows}/PlayerRow0/KillsTxt"), Is.EqualTo("4"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            yield return null;
            Assert.That(hud.transform.Find("TabPanel").gameObject.activeSelf, Is.False);
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
            GameObject asset = TeamPlayerPrefab(localTeam == TeamId.Red ? TeamId.Blue : TeamId.Red);
            remote = Object.Instantiate(asset);
            remote.GetComponent<NetworkObject>().SpawnWithOwnership(999);
            yield return null;
            Assert.That(remote.GetComponent<NetworkPlayerHudPresenter>().MatchHudInstance, Is.Null);
            Assert.That(Object.FindObjectsByType<MatchHudPresenter>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            player.GetComponent<NetworkObject>().Despawn();
            yield return null;
            Assert.That(Object.FindObjectsByType<MatchHudPresenter>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DamageConfirmationRejectsAlliesAndDeadTargetsAndReachesOwnerHud()
        {
            session.Players.Add(new LobbyPlayerData(999, localTeam, false));
            remote = Object.Instantiate(TeamPlayerPrefab(localTeam));
            remote.GetComponent<NetworkObject>().SpawnWithOwnership(999);
            yield return null;
            NetworkMatchManager match = session.GetComponent<NetworkMatchManager>();
            Variable<MatchPhase>(match, "phase").Value = MatchPhase.Playing;
            var combat = player.GetComponent<NetworkPlayerCombat>();
            var victim = remote.GetComponent<NetworkPlayerHealth>();
            var feedback = player.GetComponentInChildren<HitConfirmationFeedback>(true);
            int confirmations = 0;
            bool killed = false;
            combat.HitConfirmed += (damage, lethal) => { confirmations++; killed = lethal; };

            Assert.That(combat.TryApplyDamageToPlayerOnServer(victim, 25, out _, out _), Is.False);
            Assert.That(confirmations, Is.Zero);
            for (int i = 0; i < session.Players.Count; i++)
                if (session.Players[i].ClientId == 999)
                    session.Players[i] = new LobbyPlayerData(999, localTeam == TeamId.Red ? TeamId.Blue : TeamId.Red, false);
            Assert.That(combat.TryApplyDamageToPlayerOnServer(victim, 25, out int applied, out _), Is.True);
            Assert.That(applied, Is.EqualTo(25));
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(killed, Is.False);
            Assert.That(feedback.IsVisible, Is.True);
            Assert.That(remote.GetComponentInChildren<HitConfirmationFeedback>(true).IsVisible, Is.False);
            Assert.That(combat.TryApplyDamageToPlayerOnServer(victim, 100, out applied, out _), Is.True);
            Assert.That(applied, Is.EqualTo(75));
            Assert.That(confirmations, Is.EqualTo(2));
            Assert.That(killed, Is.True);
            Assert.That(feedback.IsShowingKill, Is.True);
            Assert.That(combat.TryApplyDamageToPlayerOnServer(victim, 25, out _, out _), Is.False);
            Assert.That(confirmations, Is.EqualTo(2));
            player.GetComponent<NetworkPlayerHudPresenter>().SetHudVisible(false);
            Assert.That(feedback.IsVisible, Is.False);
        }

        private static string Text(MatchHudPresenter hud, string path) => hud.transform.Find(path).GetComponent<TMP_Text>().text;
        private static NetworkVariable<T> Variable<T>(object owner, string field) =>
            (NetworkVariable<T>)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
#endif
