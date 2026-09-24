using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public class Sprint4RaceAutomatedTests
{
    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_004")]
    public void TST_S4_004_LaCarreraSeHabilitaConLosDosJugadoresListos()
    {
        Assert.That(
            RaceStartReadiness.ArePlayersReady(2, 2, 2, 2, 0),
            Is.True
        );
        Assert.That(RaceStateRules.CanStart(EstadoCarrera.Esperando), Is.True);
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_005")]
    public void TST_S4_005_ElRelojEsperaLaCargaCompletaDeAmbosJugadores()
    {
        Assert.That(
            RaceStartReadiness.ArePlayersReady(2, 2, 1, 1, 0),
            Is.False,
            "No debe iniciar mientras un cliente sigue cargando."
        );
        Assert.That(
            RaceStartReadiness.ArePlayersReady(2, 2, 2, 1, 0),
            Is.False,
            "No debe iniciar antes de que ambos personajes sean generados."
        );
        Assert.That(
            RaceStartReadiness.ArePlayersReady(2, 2, 2, 2, 1),
            Is.False,
            "No debe iniciar cuando una carga agotó el tiempo."
        );
        Assert.That(RaceStartReadiness.ArePlayersReady(2, 2, 2, 2, 0), Is.True);
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_006")]
    public void TST_S4_006_UnaSenalRepetidaNoVuelveAIniciarLaCarrera()
    {
        Assert.That(RaceStateRules.CanStart(EstadoCarrera.Esperando), Is.True);
        Assert.That(RaceStateRules.CanStart(EstadoCarrera.Activa), Is.False);
        Assert.That(RaceStateRules.CanStart(EstadoCarrera.Finalizada), Is.False);
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_007")]
    public void TST_S4_007_ElHostQueLlegaPrimeroGanaYElClientePierde()
    {
        Assert.That(
            RaceResultRules.GetLocalResult(
                EstadoCarrera.Finalizada,
                MotivoFinalizacionCarrera.Llegada,
                0,
                0
            ),
            Is.EqualTo(ResultadoCarreraLocal.Victoria)
        );
        Assert.That(
            RaceResultRules.GetLocalResult(
                EstadoCarrera.Finalizada,
                MotivoFinalizacionCarrera.Llegada,
                0,
                1
            ),
            Is.EqualTo(ResultadoCarreraLocal.DerrotaPorLlegadaRival)
        );
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_008")]
    public void TST_S4_008_ElClienteQueLlegaPrimeroGanaSinSesgoDeRol()
    {
        Assert.That(
            RaceResultRules.GetLocalResult(
                EstadoCarrera.Finalizada,
                MotivoFinalizacionCarrera.Llegada,
                1,
                1
            ),
            Is.EqualTo(ResultadoCarreraLocal.Victoria)
        );
        Assert.That(
            RaceResultRules.GetLocalResult(
                EstadoCarrera.Finalizada,
                MotivoFinalizacionCarrera.Llegada,
                1,
                0
            ),
            Is.EqualTo(ResultadoCarreraLocal.DerrotaPorLlegadaRival)
        );
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_009")]
    public void TST_S4_009_UnaLlegadaInvalidaNoPublicaVictoria()
    {
        Assert.That(RaceStateRules.CanFinish(EstadoCarrera.Esperando), Is.False);
        Assert.That(RaceStateRules.CanFinish(EstadoCarrera.Finalizada), Is.False);
        Assert.That(
            RaceResultRules.GetLocalResult(
                EstadoCarrera.Finalizada,
                MotivoFinalizacionCarrera.Llegada,
                GameManager.SinGanador,
                0
            ),
            Is.EqualTo(ResultadoCarreraLocal.Pendiente)
        );
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_010")]
    public void TST_S4_010_ContactosRepetidosNoDuplicanElCierre()
    {
        EstadoCarrera state = EstadoCarrera.Activa;

        Assert.That(RaceStateRules.CanFinish(state), Is.True);
        state = EstadoCarrera.Finalizada;

        Assert.That(RaceStateRules.CanFinish(state), Is.False);
        Assert.That(RaceStateRules.CanFinish(state), Is.False);
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_011")]
    public void TST_S4_011_SoloElRivalDelGanadorRecibeDerrotaPorLlegada()
    {
        ulong winnerId = 7;

        ResultadoCarreraLocal winnerResult = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            winnerId,
            winnerId
        );
        ResultadoCarreraLocal rivalResult = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            winnerId,
            8
        );

        Assert.That(winnerResult, Is.EqualTo(ResultadoCarreraLocal.Victoria));
        Assert.That(
            rivalResult,
            Is.EqualTo(ResultadoCarreraLocal.DerrotaPorLlegadaRival)
        );
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_012")]
    public void TST_S4_012_ElTimeoutDerrotaATodosYSoloSeEvaluaEnCarreraActiva()
    {
        Assert.That(RaceStateRules.CanFinish(EstadoCarrera.Esperando), Is.False);

        foreach (ulong localClientId in new[] { 0ul, 1ul })
        {
            Assert.That(
                RaceResultRules.GetLocalResult(
                    EstadoCarrera.Finalizada,
                    MotivoFinalizacionCarrera.TiempoAgotado,
                    GameManager.SinGanador,
                    localClientId
                ),
                Is.EqualTo(ResultadoCarreraLocal.DerrotaPorTiempo)
            );
        }
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_014")]
    public void TST_S4_014_ElPrimerEventoAutoritativoDefineElResultado()
    {
        EstadoCarrera state = EstadoCarrera.Activa;
        MotivoFinalizacionCarrera reason = MotivoFinalizacionCarrera.Ninguno;
        ulong winnerId = GameManager.SinGanador;

        Assert.That(
            TryApplyResult(
                ref state,
                ref reason,
                ref winnerId,
                MotivoFinalizacionCarrera.Llegada,
                1
            ),
            Is.True
        );
        Assert.That(
            TryApplyResult(
                ref state,
                ref reason,
                ref winnerId,
                MotivoFinalizacionCarrera.TiempoAgotado,
                GameManager.SinGanador
            ),
            Is.False
        );
        Assert.That(reason, Is.EqualTo(MotivoFinalizacionCarrera.Llegada));
        Assert.That(winnerId, Is.EqualTo(1ul));

        state = EstadoCarrera.Activa;
        reason = MotivoFinalizacionCarrera.Ninguno;
        winnerId = GameManager.SinGanador;

        Assert.That(
            TryApplyResult(
                ref state,
                ref reason,
                ref winnerId,
                MotivoFinalizacionCarrera.TiempoAgotado,
                GameManager.SinGanador
            ),
            Is.True
        );
        Assert.That(
            TryApplyResult(
                ref state,
                ref reason,
                ref winnerId,
                MotivoFinalizacionCarrera.Llegada,
                1
            ),
            Is.False
        );
        Assert.That(reason, Is.EqualTo(MotivoFinalizacionCarrera.TiempoAgotado));
        Assert.That(winnerId, Is.EqualTo(GameManager.SinGanador));
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_015")]
    public void TST_S4_015_ElEstadoDeCierreEsLegiblePorTodosYEscritoPorServidor()
    {
        GameObject gameObject = new GameObject("Sprint4GameManagerTest");

        try
        {
            gameObject.AddComponent<NetworkObject>();
            GameManager gameManager = gameObject.AddComponent<GameManager>();

            AssertServerReplicated(gameManager.tiempoActual);
            AssertServerReplicated(gameManager.estadoCarrera);
            AssertServerReplicated(gameManager.motivoFinalizacion);
            AssertServerReplicated(gameManager.idGanador);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_016")]
    public void TST_S4_016_ElResultadoFinalPermaneceInmutableAnteEventosTardios()
    {
        const ulong winnerId = 3;

        Assert.That(RaceStateRules.CanFinish(EstadoCarrera.Finalizada), Is.False);

        ResultadoCarreraLocal firstRead = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            winnerId,
            winnerId
        );
        ResultadoCarreraLocal repeatedRead = RaceResultRules.GetLocalResult(
            EstadoCarrera.Finalizada,
            MotivoFinalizacionCarrera.Llegada,
            winnerId,
            winnerId
        );

        Assert.That(firstRead, Is.EqualTo(ResultadoCarreraLocal.Victoria));
        Assert.That(repeatedRead, Is.EqualTo(firstRead));
    }

    private static bool TryApplyResult(
        ref EstadoCarrera state,
        ref MotivoFinalizacionCarrera reason,
        ref ulong winnerId,
        MotivoFinalizacionCarrera proposedReason,
        ulong proposedWinnerId)
    {
        if (!RaceStateRules.CanFinish(state))
            return false;

        reason = proposedReason;
        winnerId = proposedWinnerId;
        state = EstadoCarrera.Finalizada;
        return true;
    }

    private static void AssertServerReplicated<T>(NetworkVariable<T> variable)
    {
        Assert.That(
            variable.ReadPerm,
            Is.EqualTo(NetworkVariableReadPermission.Everyone)
        );
        Assert.That(
            variable.WritePerm,
            Is.EqualTo(NetworkVariableWritePermission.Server)
        );
    }
}
