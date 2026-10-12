using NUnit.Framework;

public class PlayerSpawnReadinessRulesTests
{
    [TestCase(true, 4, 4, 4, 0, true)]
    [TestCase(false, 4, 4, 4, 0, false)]
    [TestCase(true, 0, 0, 0, 0, false)]
    [TestCase(true, 4, 3, 3, 0, false)]
    [TestCase(true, 4, 4, 3, 0, false)]
    [TestCase(true, 4, 4, 4, 1, false)]
    public void InicioDeSpawnRequiereLoadingYCargaCompleta(
        bool isLoading,
        int requiredPlayers,
        int connectedPlayers,
        int loadedPlayers,
        int timedOutPlayers,
        bool expected)
    {
        Assert.That(
            PlayerSpawnReadinessRules.CanBeginSpawn(
                isLoading,
                requiredPlayers,
                connectedPlayers,
                loadedPlayers,
                timedOutPlayers
            ),
            Is.EqualTo(expected)
        );
    }

    [TestCase(1, 1, 1, 1, 0, true)]
    [TestCase(2, 2, 2, 2, 0, true)]
    [TestCase(2, 1, 1, 1, 0, false)]
    [TestCase(2, 2, 1, 1, 0, false)]
    [TestCase(2, 2, 2, 1, 0, false)]
    [TestCase(2, 2, 2, 2, 1, false)]
    [TestCase(2, 3, 2, 2, 0, false)]
    public void ReadinessComunConservaLosCasosDeLaCarrera(
        int requiredPlayers,
        int connectedPlayers,
        int loadedPlayers,
        int spawnedPlayers,
        int timedOutPlayers,
        bool expected)
    {
        bool commonResult = PlayerSpawnReadinessRules.ArePlayersReady(
            requiredPlayers,
            connectedPlayers,
            loadedPlayers,
            spawnedPlayers,
            timedOutPlayers
        );
        bool raceResult = RaceStartReadiness.ArePlayersReady(
            requiredPlayers,
            connectedPlayers,
            loadedPlayers,
            spawnedPlayers,
            timedOutPlayers
        );

        Assert.That(commonResult, Is.EqualTo(expected));
        Assert.That(commonResult, Is.EqualTo(raceResult));
    }

    [TestCase(true, true, true, false, true)]
    [TestCase(false, true, true, false, false)]
    [TestCase(true, false, true, false, false)]
    [TestCase(true, true, false, false, false)]
    [TestCase(true, true, true, true, false)]
    public void SpawnIndividualRequiereServidorConexionCargaYSinPlayerObject(
        bool isServer,
        bool isConnected,
        bool hasLoadedScene,
        bool alreadyHasPlayerObject,
        bool expected)
    {
        Assert.That(
            PlayerSpawnReadinessRules.CanSpawnPlayer(
                isServer,
                isConnected,
                hasLoadedScene,
                alreadyHasPlayerObject
            ),
            Is.EqualTo(expected)
        );
    }

    [TestCase(0, 4, 0)]
    [TestCase(1, 4, 1)]
    [TestCase(3, 4, 3)]
    [TestCase(4, 4, 0)]
    [TestCase(5, 4, 1)]
    [TestCase(-1, 4, -1)]
    [TestCase(0, 0, -1)]
    public void SeleccionDeSpawnEsDeterministaYReutilizaSoloAlAgotarOpciones(
        int assignedPlayers,
        int availableSpawnPoints,
        int expectedIndex)
    {
        Assert.That(
            TeamSpawnPoint.GetDeterministicIndex(
                assignedPlayers,
                availableSpawnPoints
            ),
            Is.EqualTo(expectedIndex)
        );
    }
}
