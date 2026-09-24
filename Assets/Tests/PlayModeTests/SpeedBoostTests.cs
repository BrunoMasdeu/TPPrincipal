using NUnit.Framework;

public class SpeedBoostTests
{
    [Test]
    public void TemporizadorDevuelveTiempoRestante()
    {
        float remaining = SpeedBoostTime.CalculateRemainingSeconds(
            18d,
            12.5d
        );

        Assert.That(remaining, Is.EqualTo(5.5f).Within(0.0001f));
    }

    [Test]
    public void TemporizadorNuncaDevuelveValorNegativo()
    {
        float remaining = SpeedBoostTime.CalculateRemainingSeconds(
            8d,
            12d
        );

        Assert.That(remaining, Is.EqualTo(0f));
    }
}
