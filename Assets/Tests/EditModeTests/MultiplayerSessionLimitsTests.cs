using NUnit.Framework;

public class MultiplayerSessionLimitsTests
{
    [Test]
    public void SalaReservaOchoLugaresIncluidoElHost()
    {
        Assert.That(MultiplayerSessionLimits.RoomCapacity, Is.EqualTo(8));
        Assert.That(
            MultiplayerSessionLimits.RemoteClientCapacity,
            Is.EqualTo(7)
        );
    }

    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(7, true)]
    [TestCase(8, false)]
    [TestCase(-1, false)]
    public void CapacidadDeSalaNoDependeDelTamanioDePartida(
        int connectedPlayers,
        bool expected)
    {
        Assert.That(
            MultiplayerSessionLimits.CanAcceptConnection(connectedPlayers),
            Is.EqualTo(expected)
        );
    }

    [TestCase(2, 2, true)]
    [TestCase(4, 2, true)]
    [TestCase(8, 4, true)]
    [TestCase(2, 3, false)]
    [TestCase(9, 2, false)]
    [TestCase(0, 0, false)]
    public void CantidadDePartidaRespetaConectadosYCapacidadDeSala(
        int selectedPlayers,
        int connectedPlayers,
        bool expected)
    {
        Assert.That(
            MultiplayerSessionLimits.CanSelectMatchPlayerCount(
                selectedPlayers,
                connectedPlayers),
            Is.EqualTo(expected)
        );
    }
}
