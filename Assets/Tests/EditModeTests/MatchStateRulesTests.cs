using NUnit.Framework;

public class MatchStateRulesTests
{
    [TestCase(MatchPhase.WaitingForPlayers, MatchPhase.Countdown, true)]
    [TestCase(MatchPhase.Countdown, MatchPhase.Playing, true)]
    [TestCase(MatchPhase.Playing, MatchPhase.Finished, true)]
    [TestCase(MatchPhase.Countdown, MatchPhase.WaitingForPlayers, true)]
    [TestCase(MatchPhase.WaitingForPlayers, MatchPhase.Playing, false)]
    [TestCase(MatchPhase.Countdown, MatchPhase.Finished, false)]
    [TestCase(MatchPhase.Playing, MatchPhase.WaitingForPlayers, false)]
    [TestCase(MatchPhase.Finished, MatchPhase.Countdown, false)]
    public void SoloPermiteTransicionesDelCicloComun(
        MatchPhase current,
        MatchPhase next,
        bool expected)
    {
        Assert.That(
            MatchStateRules.CanTransition(current, next),
            Is.EqualTo(expected)
        );
    }

    [TestCase(MatchPhase.WaitingForPlayers)]
    [TestCase(MatchPhase.Countdown)]
    [TestCase(MatchPhase.Playing)]
    [TestCase(MatchPhase.Finished)]
    public void UnaLlamadaRepetidaNoTransiciona(MatchPhase phase)
    {
        Assert.That(MatchStateRules.CanTransition(phase, phase), Is.False);
    }

    [Test]
    public void ReinicioDesdeFinishedRequiereAutorizacion()
    {
        Assert.That(
            MatchStateRules.CanResetForRematch(MatchPhase.Finished, false),
            Is.False
        );
        Assert.That(
            MatchStateRules.CanResetForRematch(MatchPhase.Finished, true),
            Is.True
        );
        Assert.That(
            MatchStateRules.CanResetForRematch(MatchPhase.Playing, true),
            Is.False
        );
    }

    [Test]
    public void MetodosEspecificosExpresanCadaOperacion()
    {
        Assert.That(
            MatchStateRules.CanStartCountdown(MatchPhase.WaitingForPlayers),
            Is.True
        );
        Assert.That(
            MatchStateRules.CanCancelCountdown(MatchPhase.Countdown),
            Is.True
        );
        Assert.That(
            MatchStateRules.CanBeginMatch(MatchPhase.Countdown),
            Is.True
        );
        Assert.That(
            MatchStateRules.CanFinishMatch(MatchPhase.Playing),
            Is.True
        );
    }
}
