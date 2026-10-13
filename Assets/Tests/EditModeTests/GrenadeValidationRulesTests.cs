using NUnit.Framework;
using UnityEngine;

public class GrenadeValidationRulesTests
{
    [Test]
    public void LanzamientoRequierePlayingVidaCantidadYCadencia()
    {
        Assert.That(GrenadeValidationRules.CanThrow(
            MatchPhase.Playing, PlayerLifeState.Alive, 3, 0d, 1d), Is.True);
        Assert.That(GrenadeValidationRules.CanThrow(
            MatchPhase.Countdown, PlayerLifeState.Alive, 3, 0d, 1d), Is.False);
        Assert.That(GrenadeValidationRules.CanThrow(
            MatchPhase.Playing, PlayerLifeState.Dead, 3, 0d, 1d), Is.False);
        Assert.That(GrenadeValidationRules.CanThrow(
            MatchPhase.Playing, PlayerLifeState.Alive, 0, 0d, 1d), Is.False);
        Assert.That(GrenadeValidationRules.CanThrow(
            MatchPhase.Playing, PlayerLifeState.Alive, 3, 2d, 1d), Is.False);
    }

    [Test]
    public void MiraDebeSerFinitaUnitariaYCercanaAlJugador()
    {
        Vector3 player = new(10f, 0f, 10f);

        Assert.That(GrenadeValidationRules.IsAimValid(
            player + Vector3.up,
            Vector3.forward,
            player), Is.True);
        Assert.That(GrenadeValidationRules.IsAimValid(
            player + Vector3.forward * 10f,
            Vector3.forward,
            player), Is.False);
        Assert.That(GrenadeValidationRules.IsAimValid(
            player,
            Vector3.zero,
            player), Is.False);
        Assert.That(GrenadeValidationRules.IsAimValid(
            new Vector3(float.NaN, 0f, 0f),
            Vector3.forward,
            player), Is.False);
    }

    [Test]
    public void VelocidadCombinaImpulsoFrontalYVertical()
    {
        Vector3 velocity = GrenadeValidationRules.CalculateInitialVelocity(
            Vector3.forward,
            12f,
            4f);

        Assert.That(velocity, Is.EqualTo(new Vector3(0f, 4f, 12f)));
    }

    [TestCase(3, 2)]
    [TestCase(1, 0)]
    [TestCase(0, 0)]
    public void ConsumoNuncaProduceCantidadNegativa(int before, int expected)
    {
        Assert.That(
            GrenadeValidationRules.CountAfterThrow(before),
            Is.EqualTo(expected));
    }
}
