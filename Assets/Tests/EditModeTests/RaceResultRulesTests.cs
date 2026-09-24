using NUnit.Framework;

public class RaceResultRulesTests
{
    [Test]
    public void ResultadoPermanecePendienteMientrasLaCarreraNoFinalizo()
    {
        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            EstadoCarrera.Activa,
            MotivoFinalizacionCarrera.Llegada,
            0,
            0
        );

        Assert.That(result, Is.EqualTo(ResultadoCarreraLocal.Pendiente));
    }

    [Test]
    public void JugadorQueLlegaPrimeroObtieneVictoria()
    {
        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            1,
            1
        );

        Assert.That(result, Is.EqualTo(ResultadoCarreraLocal.Victoria));
    }

    [Test]
    public void RivalDelGanadorPierdePorLlegada()
    {
        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            1,
            0
        );

        Assert.That(
            result,
            Is.EqualTo(ResultadoCarreraLocal.DerrotaPorLlegadaRival)
        );
    }

    [TestCase(0ul)]
    [TestCase(1ul)]
    public void TiempoAgotadoGeneraDerrotaParaTodos(ulong localClientId)
    {
        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.TiempoAgotado,
            GameManager.SinGanador,
            localClientId
        );

        Assert.That(
            result,
            Is.EqualTo(ResultadoCarreraLocal.DerrotaPorTiempo)
        );
    }

    [Test]
    public void LlegadaSinGanadorValidoPermanecePendiente()
    {
        ResultadoCarreraLocal result = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            GameManager.SinGanador,
            0
        );

        Assert.That(result, Is.EqualTo(ResultadoCarreraLocal.Pendiente));
    }
}
