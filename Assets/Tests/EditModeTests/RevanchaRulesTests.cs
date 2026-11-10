using NUnit.Framework;

// [COG-620] Votación de revancha y [COG-607] cambios de ping que no son
// configuración del lobby.
public class RevanchaRulesTests
{
    [TestCase(2, 2)]
    [TestCase(3, 2)]
    [TestCase(4, 3)]
    [TestCase(6, 4)]
    public void HaceFaltaMasDeLaMitad(int jugadores, int esperados)
    {
        Assert.That(RevanchaRules.VotosNecesarios(jugadores), Is.EqualTo(esperados));
    }

    [Test]
    public void ConLaMitadNoAlcanza()
    {
        Assert.That(RevanchaRules.HayMayoria(2, 4), Is.False);
        Assert.That(RevanchaRules.HayMayoria(3, 4), Is.True);
    }

    [Test]
    public void SinJugadoresNoHayMayoria()
    {
        Assert.That(RevanchaRules.HayMayoria(0, 0), Is.False);
    }

    [Test]
    public void SeVotaConLaPartidaTerminadaYTodosConectados()
    {
        Assert.That(
            RevanchaRules.PuedeVotarse(
                GameModeId.TDM, SessionPhase.InMatch, MatchPhase.Finished, 2, 2),
            Is.True);
    }

    [Test]
    public void NoSeVotaSiFaltaAlguien()
    {
        Assert.That(
            RevanchaRules.PuedeVotarse(
                GameModeId.TDM, SessionPhase.InMatch, MatchPhase.Finished, 1, 2),
            Is.False);
    }

    [Test]
    public void NoSeVotaSiLaPartidaSigue()
    {
        Assert.That(
            RevanchaRules.PuedeVotarse(
                GameModeId.TDM, SessionPhase.InMatch, MatchPhase.Playing, 2, 2),
            Is.False);
    }

    [Test]
    public void NoSeVotaEnLaCarrera()
    {
        Assert.That(
            RevanchaRules.PuedeVotarse(
                GameModeId.Race, SessionPhase.InMatch, MatchPhase.Finished, 2, 2),
            Is.False);
    }

    // Si estos cambios se avisaran como cambios del lobby, la partida
    // terminada se reiniciaría sola un segundo después de mostrar el resultado.
    [Test]
    public void ElPingSoloNoEsUnCambioDelLobby()
    {
        LobbyPlayerData anterior = new(1, TeamId.Red, true, "Tomas");
        LobbyPlayerData actual = anterior;
        actual.Ping = 45;

        Assert.That(LobbyPlayerData.SoloCambioPingOVoto(anterior, actual), Is.True);
    }

    [Test]
    public void ElVotoSoloNoEsUnCambioDelLobby()
    {
        LobbyPlayerData anterior = new(1, TeamId.Red, true, "Tomas");
        LobbyPlayerData actual = anterior;
        actual.QuiereRevancha = true;

        Assert.That(LobbyPlayerData.SoloCambioPingOVoto(anterior, actual), Is.True);
    }

    [Test]
    public void CambiarDeEquipoSiEsUnCambioDelLobby()
    {
        LobbyPlayerData anterior = new(1, TeamId.Red, true, "Tomas");
        LobbyPlayerData actual = anterior;
        actual.TeamId = TeamId.Blue;
        actual.Ping = 45;

        Assert.That(LobbyPlayerData.SoloCambioPingOVoto(anterior, actual), Is.False);
    }
}
