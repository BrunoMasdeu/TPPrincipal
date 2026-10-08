using System;
using System.Collections.Generic;
using NUnit.Framework;

public class TdmRulesTests
{
    private static TdmMatchState TwoPlayers() => new TdmMatchState(new[]
    {
        new PlayerMatchStats(0, TeamId.Red),
        new PlayerMatchStats(1, TeamId.Blue)
    });

    private static PlayerDeathInfo Death(
        ulong eventId, ulong victim, TeamId victimTeam,
        PlayerDeathCause cause, bool hasAttacker = false,
        ulong attacker = 0, TeamId attackerTeam = TeamId.None) =>
        new PlayerDeathInfo(eventId, victim, victimTeam, hasAttacker,
            attacker, attackerTeam, cause);

    private static TdmMatchState Players(int count)
    {
        var players = new List<PlayerMatchStats>();
        for (ulong id = 0; id < (ulong)count; id++)
            players.Add(new PlayerMatchStats(id, id % 2 == 0 ? TeamId.Red : TeamId.Blue));
        return new TdmMatchState(players);
    }

    [Test]
    public void EnemyKillScoresForAttackerAndTeamWithoutMutatingPreviousState()
    {
        TdmMatchState before = TwoPlayers();
        TdmDeathResult applied = TdmRules.ApplyDeath(before,
            Death(1, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack,
                true, 0, TeamId.Red), MatchPhase.Playing);

        Assert.That(applied.Decision, Is.EqualTo(TdmDeathDecision.Counted));
        Assert.That(applied.ScoringTeam, Is.EqualTo(TeamId.Red));
        Assert.That(applied.State.RedEliminations, Is.EqualTo(1));
        Assert.That(applied.State.BlueEliminations, Is.Zero);
        Assert.That(applied.State.TryGetPlayer(0, out PlayerMatchStats attacker), Is.True);
        Assert.That(attacker.Kills, Is.EqualTo(1));
        Assert.That(applied.State.TryGetPlayer(1, out PlayerMatchStats victim), Is.True);
        Assert.That(victim.Deaths, Is.EqualTo(1));
        Assert.That(before.RedEliminations, Is.Zero);
        Assert.That(before.TryGetPlayer(1, out PlayerMatchStats originalVictim), Is.True);
        Assert.That(originalVictim.Deaths, Is.Zero);
    }

    [Test]
    public void BlueCanScoreAgainstRed()
    {
        TdmDeathResult applied = TdmRules.ApplyDeath(TwoPlayers(),
            Death(2, 0, TeamId.Red, PlayerDeathCause.PlayerAttack,
                true, 1, TeamId.Blue), MatchPhase.Playing);

        Assert.That(applied.State.BlueEliminations, Is.EqualTo(1));
        Assert.That(applied.ScoringTeam, Is.EqualTo(TeamId.Blue));
    }

    [TestCase(PlayerDeathCause.Suicide)]
    [TestCase(PlayerDeathCause.Environmental)]
    public void SuicideAndEnvironmentAddOnlyVictimDeath(PlayerDeathCause cause)
    {
        // Aunque el campo atacante quede en 0, HasAttacker=false: 0 es el ID real del host.
        TdmDeathResult applied = TdmRules.ApplyDeath(TwoPlayers(),
            Death(3, 1, TeamId.Blue, cause), MatchPhase.Playing);

        Assert.That(applied.WasCounted, Is.True);
        Assert.That(applied.ScoringTeam, Is.EqualTo(TeamId.None));
        Assert.That(applied.State.RedEliminations, Is.Zero);
        Assert.That(applied.State.BlueEliminations, Is.Zero);
        Assert.That(applied.State.TryGetPlayer(0, out PlayerMatchStats host), Is.True);
        Assert.That(host.Kills, Is.Zero);
        Assert.That(applied.State.TryGetPlayer(1, out PlayerMatchStats victim), Is.True);
        Assert.That(victim.Deaths, Is.EqualTo(1));
    }

    [Test]
    public void ExplicitSelfAttackerIsStillSuicideWithoutTeamScore()
    {
        TdmDeathResult applied = TdmRules.ApplyDeath(TwoPlayers(),
            Death(4, 0, TeamId.Red, PlayerDeathCause.Suicide,
                true, 0, TeamId.Red), MatchPhase.Playing);

        Assert.That(applied.WasCounted, Is.True);
        Assert.That(applied.State.TryGetPlayer(0, out PlayerMatchStats victim), Is.True);
        Assert.That(victim.Deaths, Is.EqualTo(1));
        Assert.That(victim.Kills, Is.Zero);
        Assert.That(applied.State.RedEliminations, Is.Zero);
    }

    [Test]
    public void RepeatedEventIdDoesNotCountTwiceEvenIfPayloadChanges()
    {
        TdmMatchState initial = TwoPlayers();
        TdmMatchState counted = TdmRules.ApplyDeath(initial,
            Death(5, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack,
                true, 0, TeamId.Red), MatchPhase.Playing).State;
        TdmDeathResult duplicate = TdmRules.ApplyDeath(counted,
            Death(5, 0, TeamId.Red, PlayerDeathCause.Environmental), MatchPhase.Playing);

        Assert.That(duplicate.Decision, Is.EqualTo(TdmDeathDecision.Duplicate));
        Assert.That(duplicate.State, Is.SameAs(counted));
        Assert.That(counted.HasProcessedEvent(5), Is.True);
        Assert.That(counted.RedEliminations, Is.EqualTo(1));
    }

    [TestCase(MatchPhase.WaitingForPlayers)]
    [TestCase(MatchPhase.Countdown)]
    [TestCase(MatchPhase.Finished)]
    public void DeathOutsidePlayingDoesNotChangeState(MatchPhase phase)
    {
        TdmMatchState initial = TwoPlayers();
        TdmDeathResult rejected = TdmRules.ApplyDeath(initial,
            Death(6, 1, TeamId.Blue, PlayerDeathCause.Environmental), phase);
        Assert.That(rejected.Decision, Is.EqualTo(TdmDeathDecision.NotPlaying));
        Assert.That(rejected.State, Is.SameAs(initial));
    }

    [Test]
    public void FriendlyFireDeathIsRejectedWithoutAnyStatistics()
    {
        var state = new TdmMatchState(new[]
        {
            new PlayerMatchStats(0, TeamId.Red), new PlayerMatchStats(2, TeamId.Red),
            new PlayerMatchStats(1, TeamId.Blue), new PlayerMatchStats(3, TeamId.Blue)
        });
        TdmDeathResult rejected = TdmRules.ApplyDeath(state,
            Death(7, 2, TeamId.Red, PlayerDeathCause.PlayerAttack,
                true, 0, TeamId.Red), MatchPhase.Playing);

        Assert.That(rejected.Decision, Is.EqualTo(TdmDeathDecision.Invalid));
        Assert.That(rejected.State, Is.SameAs(state));
        Assert.That(state.HasProcessedEvent(7), Is.False);
    }

    [Test]
    public void InvalidAndContradictoryDeathsAreRejected()
    {
        TdmMatchState state = TwoPlayers();
        PlayerDeathInfo[] invalid =
        {
            Death(0, 1, TeamId.Blue, PlayerDeathCause.Environmental),
            Death(8, 2, TeamId.Blue, PlayerDeathCause.Environmental),
            Death(9, 1, TeamId.Red, PlayerDeathCause.Environmental),
            Death(10, 1, TeamId.Blue, PlayerDeathCause.Unknown),
            Death(11, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack),
            Death(12, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack, true, 0, TeamId.Blue),
            Death(13, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack, true, 1, TeamId.Blue),
            Death(14, 1, TeamId.Blue, PlayerDeathCause.Environmental, true, 0, TeamId.Red),
            Death(15, 1, TeamId.Blue, PlayerDeathCause.Suicide, true, 0, TeamId.Red)
        };
        foreach (PlayerDeathInfo death in invalid)
        {
            TdmDeathResult rejected = TdmRules.ApplyDeath(state, death, MatchPhase.Playing);
            Assert.That(rejected.Decision, Is.EqualTo(TdmDeathDecision.Invalid));
            Assert.That(rejected.State, Is.SameAs(state));
        }
    }

    [Test]
    public void InitialPlayersMustHaveUniqueIdsAndPlayableTeams()
    {
        Assert.Throws<ArgumentException>(() => new TdmMatchState(new[]
        {
            new PlayerMatchStats(0, TeamId.Red), new PlayerMatchStats(0, TeamId.Blue)
        }));
        Assert.Throws<ArgumentException>(() => new TdmMatchState(new[]
        {
            new PlayerMatchStats(0, TeamId.None)
        }));
        Assert.Throws<ArgumentException>(() => new TdmMatchState(new[]
        {
            new PlayerMatchStats(0, TeamId.Red, -1)
        }));
    }

    [TestCase(4, 30, true)]
    [TestCase(6, 40, true)]
    [TestCase(8, 60, true)]
    [TestCase(2, 0, false)]
    [TestCase(5, 0, false)]
    public void OfficialLimitsAreExplicit(int players, int expected, bool supported)
    {
        bool found = TdmRules.TryGetOfficialEliminationLimit(players, out int limit);
        Assert.That(found, Is.EqualTo(supported));
        Assert.That(limit, Is.EqualTo(expected));
    }

    [Test]
    public void ScoreLimitEndsOnlyAtOfficialThreshold()
    {
        TdmMatchState state = Players(4);
        for (ulong id = 1; id <= 30; id++)
        {
            Assert.That(TdmRules.TryResolveScoreLimit(state, 4, MatchPhase.Playing, out _), Is.False);
            state = TdmRules.ApplyDeath(state,
                Death(id, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack,
                    true, 0, TeamId.Red), MatchPhase.Playing).State;
        }

        Assert.That(TdmRules.TryResolveScoreLimit(state, 4, MatchPhase.Playing, out MatchResultData result), Is.True);
        Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.ScoreLimit));
        Assert.That(result.WinningTeam, Is.EqualTo(TeamId.Red));
        Assert.That(result.IsDraw, Is.False);
        Assert.That(TdmRules.TryResolveScoreLimit(state, 2, MatchPhase.Playing, out _), Is.False);
        Assert.That(TdmRules.TryResolveScoreLimit(state, 4, MatchPhase.Finished, out _), Is.False);
    }

    [Test]
    public void TimeoutChoosesLeadingTeamOrDrawWithoutStartingAnotherClock()
    {
        TdmMatchState tied = TwoPlayers();
        Assert.That(TdmRules.TryResolveTimeExpired(tied, MatchPhase.Playing, out MatchResultData draw), Is.True);
        Assert.That(draw.IsDraw, Is.True);
        Assert.That(draw.WinningTeam, Is.EqualTo(TeamId.None));
        Assert.That(draw.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));

        TdmMatchState blueLeads = TdmRules.ApplyDeath(tied,
            Death(20, 0, TeamId.Red, PlayerDeathCause.PlayerAttack,
                true, 1, TeamId.Blue), MatchPhase.Playing).State;
        Assert.That(TdmRules.TryResolveTimeExpired(blueLeads, MatchPhase.Playing, out MatchResultData result), Is.True);
        Assert.That(result.WinningTeam, Is.EqualTo(TeamId.Blue));
        Assert.That(result.IsDraw, Is.False);
        Assert.That(TdmRules.TryResolveTimeExpired(blueLeads, MatchPhase.Finished, out _), Is.False);
    }

    [Test]
    public void FinalSummaryIsSnapshotAndRejectsPendingResult()
    {
        TdmMatchState initial = TwoPlayers();
        Assert.That(TdmRules.TryCreateFinalSummary(initial, default, out _), Is.False);

        TdmMatchState scored = TdmRules.ApplyDeath(initial,
            Death(21, 1, TeamId.Blue, PlayerDeathCause.PlayerAttack,
                true, 0, TeamId.Red), MatchPhase.Playing).State;
        Assert.That(TdmRules.TryResolveTimeExpired(scored, MatchPhase.Playing, out MatchResultData result), Is.True);
        Assert.That(TdmRules.TryCreateFinalSummary(scored, result, out TdmFinalSummary summary), Is.True);

        TdmMatchState later = TdmRules.ApplyDeath(scored,
            Death(22, 0, TeamId.Red, PlayerDeathCause.Environmental), MatchPhase.Playing).State;
        Assert.That(later.TryGetPlayer(0, out PlayerMatchStats changed), Is.True);
        Assert.That(changed.Deaths, Is.EqualTo(1));
        Assert.That(summary.RedEliminations, Is.EqualTo(1));
        Assert.That(summary.Players[0].Deaths, Is.Zero);
        Assert.That(summary.Result, Is.EqualTo(result));
    }
}
