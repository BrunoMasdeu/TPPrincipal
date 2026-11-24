using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CanvasTdmIntegrationTests
{
    private const string HudPrefabPath = "Assets/UI/CanvasTDM.prefab";
    private const string ResultPanelPath = "PanelResultadoPartida";
    private GameObject hud;
    private MatchHudView view;

    [SetUp]
    public void LoadHud()
    {
        hud = PrefabUtility.LoadPrefabContents(HudPrefabPath);
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
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath).GetComponent<MatchHudPresenter>()),
            $"{player.name} debe mostrar el mismo HUD que el otro equipo.");
        Assert.That(player.GetComponentInChildren<MatchHudPresenter>(true), Is.Null,
            "CanvasTDM se crea sólo para el propietario.");
        Assert.That(player.GetComponentInChildren<PlayerHudView>(true), Is.Not.Null,
            "PlayerUI continúa dentro del jugador.");
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
        Assert.That(Text("PanelPrincipal/PanelSuperior/EquipoRojoScore/Puntos"), Is.EqualTo("7"));
        Assert.That(Text("TabPanel/EquipoAzulScore"), Is.EqualTo("3"));
        Assert.That(Text("PanelPrincipal/PanelSuperior/FinalScore/Text (TMP)"), Is.EqualTo("Meta: 30 eliminaciones"));
        view.SetMoney(850);
        Assert.That(Text("PanelPrincipal/DineroTxt"), Is.EqualTo("$850"));
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
        Assert.That(Text("PanelPrincipal/PanelSuperior/FinalScore/Text (TMP)"), Is.EqualTo("Meta: 3 capturas"));
        view.SetResult(true, new MatchResultData(TeamId.None, MatchEndReason.TimeExpired, true), false, TeamId.Blue);
        Assert.That(hud.transform.Find("PanelPrincipal/Txt_TableHint").gameObject.activeSelf, Is.False);
        Assert.That(Text($"{ResultPanelPath}/Txt_ResultTitle"), Is.EqualTo("EMPATE"));
        Button lobby = hud.transform.Find($"{ResultPanelPath}/ButtonLobby").GetComponent<Button>();
        Assert.That(lobby.interactable, Is.False);
        view.SetResult(true, new MatchResultData(TeamId.Red, MatchEndReason.ScoreLimit, false), true, TeamId.Red);
        Assert.That(lobby.interactable, Is.True);
        Assert.That(lobby.onClick.GetPersistentTarget(0), Is.EqualTo(hud.GetComponent<MatchHudPresenter>()));
        Assert.That(lobby.onClick.GetPersistentMethodName(0), Is.EqualTo("ReturnToLobby"));
    }

    [TestCase(TeamId.Red, TeamId.Red, MatchEndReason.ScoreLimit, "VICTORIA", "Gana el equipo rojo")]
    [TestCase(TeamId.Blue, TeamId.Red, MatchEndReason.ScoreLimit, "DERROTA", "Gana el equipo rojo")]
    [TestCase(TeamId.Blue, TeamId.Blue, MatchEndReason.TimeExpired, "VICTORIA", "Gana el equipo azul")]
    [TestCase(TeamId.Red, TeamId.Blue, MatchEndReason.TimeExpired, "DERROTA", "Gana el equipo azul")]
    [TestCase(TeamId.Red, TeamId.Red, MatchEndReason.TeamAbandoned, "VICTORIA", "Gana el equipo rojo")]
    public void ResultHeaderShowsLocalOutcomeAndWinningTeam(
        TeamId localTeam, TeamId winner, MatchEndReason reason, string title, string subtitle)
    {
        view.SetResult(true, new MatchResultData(winner, reason, false), false, localTeam);

        Assert.That(Text($"{ResultPanelPath}/Txt_ResultTitle"), Is.EqualTo(title));
        TMP_Text winnerLabel = hud.transform.Find($"{ResultPanelPath}/WinTeam").GetComponent<TMP_Text>();
        Image winnerFrame = hud.transform.Find($"{ResultPanelPath}/MarcoImage").GetComponent<Image>();
        Assert.That(winnerLabel.gameObject.activeInHierarchy, Is.True);
        Assert.That(winnerFrame.gameObject.activeInHierarchy, Is.True);
        Assert.That(winnerLabel.text, Is.EqualTo(subtitle));
        Assert.That(winnerLabel.GetPreferredValues(subtitle).x, Is.LessThanOrEqualTo(winnerLabel.rectTransform.rect.width));
        Color winnerColor = winner == TeamId.Red ? new Color(1f, 0.24f, 0.24f) : new Color(0f, 0.72f, 1f);
        Assert.That(winnerLabel.color, Is.EqualTo(winnerColor));
        Assert.That(winnerFrame.color, Is.EqualTo(winnerColor));
    }

    [TestCase(MatchEndReason.TimeExpired, true, "EMPATE")]
    [TestCase(MatchEndReason.None, false, "Esperando resultado...")]
    public void ResultWithoutWinnerHidesTheWinnerBanner(MatchEndReason reason, bool isDraw, string title)
    {
        view.SetResult(true, new MatchResultData(TeamId.Blue, MatchEndReason.ScoreLimit, false), true, TeamId.Blue);
        view.SetResult(true, new MatchResultData(TeamId.None, reason, isDraw), true, TeamId.Blue);

        Assert.That(Text($"{ResultPanelPath}/Txt_ResultTitle"), Is.EqualTo(title));
        Assert.That(hud.transform.Find($"{ResultPanelPath}/WinTeam").gameObject.activeSelf, Is.False);
        Assert.That(hud.transform.Find($"{ResultPanelPath}/MarcoImage").gameObject.activeSelf, Is.False);
    }

    private string Text(string path) => hud.transform.Find(path).GetComponent<TMP_Text>().text;
}
