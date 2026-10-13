using NUnit.Framework;

public class CombatValidationRulesTests
{
    [Test]
    public void SoloLosModosDeEquiposUsanCombateEnRed()
    {
        Assert.That(CombatValidationRules.IsCombatMode(GameModeId.TDM), Is.True);
        Assert.That(CombatValidationRules.IsCombatMode(GameModeId.CTF), Is.True);
        Assert.That(CombatValidationRules.IsCombatMode(GameModeId.Race), Is.False);
    }

    [Test]
    public void InventarioPuedePrevisualizarseTemporalmenteEnEntrenamiento()
    {
        Assert.That(CombatValidationRules.SupportsInventoryPreview(
            GameModeId.TDM), Is.True);
        Assert.That(CombatValidationRules.SupportsInventoryPreview(
            GameModeId.CTF), Is.True);
        Assert.That(CombatValidationRules.SupportsInventoryPreview(
            GameModeId.Race), Is.True);
        Assert.That(CombatValidationRules.SupportsInventoryPreview(
            GameModeId.None), Is.False);
        Assert.That(CombatValidationRules.SupportsInventoryPreview(
            GameModeId.None, true), Is.True);
    }

    [Test]
    public void DisparoRequierePlayingVidaBalasYCadencia()
    {
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Playing, PlayerLifeState.Alive, 12, 0d, 4d, 4d), Is.True);
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Countdown, PlayerLifeState.Alive, 12, 0d, 0d, 1d), Is.False);
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Playing, PlayerLifeState.Dead, 12, 0d, 0d, 1d), Is.False);
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Playing, PlayerLifeState.Alive, 0, 0d, 0d, 1d), Is.False);
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Playing, PlayerLifeState.Alive, 12, 2d, 0d, 1d), Is.False);
        Assert.That(CombatValidationRules.CanFire(
            MatchPhase.Playing, PlayerLifeState.Alive, 12, 0d, 2d, 1d), Is.False);
    }

    [Test]
    public void RecargaRequiereCargadorIncompletoYEstadoActivo()
    {
        Assert.That(CombatValidationRules.CanReload(
            MatchPhase.Playing, PlayerLifeState.Alive, 5, 12, 0d), Is.True);
        Assert.That(CombatValidationRules.CanReload(
            MatchPhase.Playing, PlayerLifeState.Alive, 12, 12, 0d), Is.False);
        Assert.That(CombatValidationRules.CanReload(
            MatchPhase.Playing, PlayerLifeState.Dead, 5, 12, 0d), Is.False);
        Assert.That(CombatValidationRules.CanReload(
            MatchPhase.Playing, PlayerLifeState.Alive, 5, 12, 0d, 0), Is.False);
    }

    [Test]
    public void AtaqueMeleeNoNecesitaMunicion()
    {
        Assert.That(CombatValidationRules.CanAttack(
            MatchPhase.Playing,
            PlayerLifeState.Alive,
            false,
            0,
            0d,
            1d,
            1d), Is.True);
    }

    [TestCase(4, 12, 30, 8)]
    [TestCase(10, 12, 1, 1)]
    [TestCase(12, 12, 30, 0)]
    [TestCase(0, 30, 10, 10)]
    [TestCase(0, 30, 0, 0)]
    public void TransferenciaDeRecargaNuncaExcedeCargadorNiReserva(
        int ammunition,
        int magazineSize,
        int reserve,
        int expected)
    {
        Assert.That(CombatValidationRules.CalculateReloadTransfer(
            ammunition,
            magazineSize,
            reserve), Is.EqualTo(expected));
    }

    [Test]
    public void FuegoAliadoQuedaDesactivado()
    {
        Assert.That(CombatValidationRules.IsEnemy(TeamId.Red, TeamId.Blue), Is.True);
        Assert.That(CombatValidationRules.IsEnemy(TeamId.Red, TeamId.Red), Is.False);
        Assert.That(CombatValidationRules.IsEnemy(TeamId.None, TeamId.Blue), Is.False);
    }

    [Test]
    public void VidaSeLimitaACeroYLaMuerteOcurreUnaSolaVez()
    {
        Assert.That(CombatValidationRules.HealthAfterDamage(20, 25), Is.Zero);
        Assert.That(CombatValidationRules.IsLethalTransition(20, 0), Is.True);
        Assert.That(CombatValidationRules.IsLethalTransition(0, 0), Is.False);
    }
}
