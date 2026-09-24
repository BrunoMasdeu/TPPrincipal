using NUnit.Framework;

public class RaceStartRulesTests
{
    [TestCase(1, 1, 1, 1, 0, true)]
    [TestCase(2, 2, 2, 2, 0, true)]
    [TestCase(2, 1, 1, 1, 0, false)]
    [TestCase(2, 2, 1, 1, 0, false)]
    [TestCase(2, 2, 2, 1, 0, false)]
    [TestCase(2, 2, 2, 2, 1, false)]
    [TestCase(2, 3, 2, 2, 0, false)]
    public void JugadoresListosRequiereCantidadExactaCargadaYSpawneada(
        int requiredPlayers,
        int connectedPlayers,
        int loadedPlayers,
        int spawnedPlayers,
        int timedOutPlayers,
        bool expected)
    {
        bool result = RaceStartReadiness.ArePlayersReady(
            requiredPlayers,
            connectedPlayers,
            loadedPlayers,
            spawnedPlayers,
            timedOutPlayers
        );

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(EstadoCarrera.Esperando, true)]
    [TestCase(EstadoCarrera.Activa, false)]
    [TestCase(EstadoCarrera.Finalizada, false)]
    public void InicioSoloSeAceptaDesdeEspera(
        EstadoCarrera state,
        bool expected)
    {
        Assert.That(RaceStateRules.CanStart(state), Is.EqualTo(expected));
    }

    [TestCase(EstadoCarrera.Esperando, false)]
    [TestCase(EstadoCarrera.Activa, true)]
    [TestCase(EstadoCarrera.Finalizada, false)]
    public void LlegadaYTimeoutSoloSeAceptanDuranteCarreraActiva(
        EstadoCarrera state,
        bool expected)
    {
        Assert.That(RaceStateRules.CanFinish(state), Is.EqualTo(expected));
    }
}
