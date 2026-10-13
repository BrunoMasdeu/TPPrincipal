using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CanvasTdmIntegrationTests
{
    private GameObject hud;
    private MatchHudView view;

    [SetUp]
    public void LoadHud()
    {
        hud = PrefabUtility.LoadPrefabContents("Assets/Prefabs/CanvasTDM.prefab");
        view = hud.GetComponent<MatchHudView>();
    }

    [TearDown]
    public void ReleaseHud() => PrefabUtility.UnloadPrefabContents(hud);

    [TestCase("redTeamPlayerPrefab")]
    [TestCase("blueTeamPlayerPrefab")]
    public void EachConfiguredTeamReferencesTheSameMatchHud(string teamPrefabField)
    {
        Assert.That(view, Is.Not.Null);
        Assert.That(view.HasRequiredReferences, Is.True);
        GameObject session = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkSessionRoot.prefab");
        var spawner = new SerializedObject(session.GetComponent<NetworkPlayerSpawner>());
        GameObject player = spawner.FindProperty(teamPrefabField).objectReferenceValue as GameObject;
        Assert.That(player, Is.Not.Null, teamPrefabField);
        Assert.That(player.GetComponent<NetworkPlayerHudPresenter>(), Is.Not.Null, player.name);
        var presenter = new SerializedObject(player.GetComponent<NetworkPlayerHudPresenter>());
        Assert.That(presenter.FindProperty("matchHudPrefab").objectReferenceValue,
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CanvasTDM.prefab").GetComponent<MatchHudPresenter>()),
            $"{player.name} debe mostrar el mismo HUD que el otro equipo.");
        Assert.That(player.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1),
            "CanvasTDM se crea sólo para el propietario; PlayerUI continúa dentro del jugador.");
        Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.transform.Find("TabPanel").gameObject.activeSelf, Is.False);
        Assert.That(hud.transform.Find("PanelResultadoPartida").gameObject.activeSelf, Is.False);
    }

    [TestCase(0, "00:00")]
    [TestCase(-5, "00:00")]
    [TestCase(59.1f, "01:00")]
    [TestCase(125, "02:05")]
    public void ClockUsesCeilingAndMinutes(float seconds, string expected) =>
        Assert.That(MatchHudView.FormatTime(seconds), Is.EqualTo(expected));

    [Test]
    public void ScoresAndPlayersReplaceSampleValuesWithActualData()
    {
        view.SetScores(7, 3, 30, GameModeId.TDM);
        Assert.That(Text("PanelSuperior/EquipoRojoScore/Puntos"), Is.EqualTo("7"));
        Assert.That(Text("TabPanel/EquipoAzulScore"), Is.EqualTo("3"));
        Assert.That(Text("PanelSuperior/FinalScore/Text (TMP)"), Is.EqualTo("Meta: 30 eliminaciones"));
        view.SetMoney(850);
        Assert.That(Text("DineroTxt"), Is.EqualTo("$850"));
        view.SetPlayers(new[] { new PlayerMatchStats(0, TeamId.Red, 2, 1),
            new PlayerMatchStats(1, TeamId.Blue, 0, 2) }, 0, true);
        Assert.That(Text("TabPanel/StatsRojoContainer (1)/PlayerRow0/NameTxt"), Is.EqualTo("Jugador 1 (vos)"));
        Assert.That(Text("TabPanel/StatsRojoContainer (1)/PlayerRow0/KillsTxt"), Is.EqualTo("2"));
        Assert.That(hud.transform.Find("TabPanel/StatsRojoContainer (1)/PlayerRow1").gameObject.activeSelf, Is.False);
        view.SetPlayers(new[] { new PlayerMatchStats(1, TeamId.Blue) }, 1, false);
        Assert.That(Text("TabPanel/StatsAzulContainer/PlayerRow0/KillsTxt"), Is.EqualTo("--"),
            "CTF no publica estadísticas individuales: no inventar eliminaciones.");
    }

    [Test]
    public void CtfUsesCapturesAndOnlyHostCanReturnEveryoneToLobby()
    {
        view.SetMode(GameModeId.CTF);
        view.SetScores(2, 1, 3, GameModeId.CTF);
        Assert.That(Text("PanelSuperior/FinalScore/Text (TMP)"), Is.EqualTo("Meta: 3 capturas"));
        view.SetResult(true, new MatchResultData(TeamId.None, MatchEndReason.TimeExpired, true), false);
        Assert.That(hud.transform.Find("Txt_TableHint").gameObject.activeSelf, Is.False);
        Assert.That(Text("PanelResultadoPartida/PanelResultado/Txt_ResultTitle"), Is.EqualTo("EMPATE"));
        Button lobby = hud.transform.Find("PanelResultadoPartida/PanelResultado/ButtonRevancha").GetComponent<Button>();
        Assert.That(lobby.interactable, Is.False);
        view.SetResult(true, new MatchResultData(TeamId.Red, MatchEndReason.ScoreLimit, false), true);
        Assert.That(lobby.interactable, Is.True);
        Assert.That(lobby.onClick.GetPersistentTarget(0), Is.EqualTo(hud.GetComponent<MatchHudPresenter>()));
        Assert.That(lobby.onClick.GetPersistentMethodName(0), Is.EqualTo("ReturnToLobby"));
    }

    private string Text(string path) => hud.transform.Find(path).GetComponent<TMP_Text>().text;
}
