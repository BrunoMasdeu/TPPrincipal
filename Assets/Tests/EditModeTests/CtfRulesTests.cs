using NUnit.Framework;
using UnityEngine;

public class CtfRulesTests
{
    private static CtfMatchState PickUp(CtfMatchState state, TeamId flagTeam,
        TeamId playerTeam, ulong playerId, double now = 0d)
    {
        Assert.That(CtfRules.TryPickUpFlag(state, flagTeam, playerTeam,
            playerId, MatchPhase.Playing, now, out CtfMatchState next), Is.True);
        return next;
    }

    private static CtfMatchState Capture(CtfMatchState state, TeamId team,
        ulong playerId)
    {
        Assert.That(CtfRules.TryCapture(state, team, team, playerId,
            MatchPhase.Playing, out CtfMatchState next), Is.True);
        return next;
    }

    [Test]
    public void FlagsBeginAtBaseWithoutCaptures()
    {
        var state = new CtfMatchState();
        Assert.That(state.RedFlag.State, Is.EqualTo(FlagState.AtBase));
        Assert.That(state.BlueFlag.State, Is.EqualTo(FlagState.AtBase));
        Assert.That(state.RedCaptures, Is.Zero);
        Assert.That(state.BlueCaptures, Is.Zero);
    }

    [Test]
    public void EnemyPicksUpFlagOnceAndRulesDoNotMutatePreviousState()
    {
        var original = new CtfMatchState();
        CtfMatchState carried = PickUp(original, TeamId.Blue, TeamId.Red, 0UL);
        Assert.That(carried.BlueFlag.State, Is.EqualTo(FlagState.Carried));
        Assert.That(carried.BlueFlag.CarrierClientId, Is.Zero);
        Assert.That(original.BlueFlag.State, Is.EqualTo(FlagState.AtBase));
        Assert.That(CtfRules.TryPickUpFlag(carried, TeamId.Blue, TeamId.Red,
            2UL, MatchPhase.Playing, 1d, out CtfMatchState duplicate), Is.False);
        Assert.That(duplicate, Is.SameAs(carried));
        Assert.That(CtfRules.TryPickUpFlag(original, TeamId.Blue, TeamId.Blue,
            1UL, MatchPhase.Playing, 1d, out _), Is.False);
        Assert.That(CtfRules.TryPickUpFlag(original, TeamId.Blue, TeamId.Red,
            0UL, MatchPhase.Countdown, 1d, out _), Is.False);
    }

    [Test]
    public void CarrierCapturesAtOwnBaseEvenWhenOwnFlagIsStolen()
    {
        CtfMatchState state = PickUp(new CtfMatchState(),
            TeamId.Red, TeamId.Blue, 1UL);
        state = PickUp(state, TeamId.Blue, TeamId.Red, 0UL);

        Assert.That(CtfRules.TryCapture(state, TeamId.Red, TeamId.Red, 0UL,
            MatchPhase.Playing, out CtfMatchState scored), Is.True);
        Assert.That(scored.RedCaptures, Is.EqualTo(1));
        Assert.That(scored.BlueFlag.State, Is.EqualTo(FlagState.AtBase));
        Assert.That(scored.RedFlag.State, Is.EqualTo(FlagState.Carried));
        Assert.That(scored.RedFlag.CarrierClientId, Is.EqualTo(1UL));
        Assert.That(state.RedCaptures, Is.Zero);
        Assert.That(CtfRules.TryCapture(scored, TeamId.Red, TeamId.Red, 0UL,
            MatchPhase.Playing, out _), Is.False);
    }

    [Test]
    public void WrongTeamOrWrongCarrierCannotCapture()
    {
        CtfMatchState state = PickUp(new CtfMatchState(),
            TeamId.Blue, TeamId.Red, 0UL);
        Assert.That(CtfRules.TryCapture(state, TeamId.Blue, TeamId.Red, 0UL,
            MatchPhase.Playing, out _), Is.False);
        Assert.That(CtfRules.TryCapture(state, TeamId.Red, TeamId.Red, 2UL,
            MatchPhase.Playing, out _), Is.False);
        Assert.That(CtfRules.TryCapture(state, TeamId.Red, TeamId.Red, 0UL,
            MatchPhase.Finished, out _), Is.False);
    }

    [Test]
    public void CarrierDeathDropsFlagForFiveSecondsAndDuplicateIsIgnored()
    {
        CtfMatchState carried = PickUp(new CtfMatchState(),
            TeamId.Blue, TeamId.Red, 0UL);
        var death = new PlayerDeathInfo(10UL, 0UL, TeamId.Red,
            true, 1UL, TeamId.Blue, PlayerDeathCause.PlayerAttack);
        Vector3 position = new Vector3(4f, 1f, -2f);

        Assert.That(CtfRules.TryDropCarriedFlag(carried, death, position,
            100d, MatchPhase.Playing, out CtfMatchState dropped), Is.True);
        Assert.That(dropped.BlueFlag.State, Is.EqualTo(FlagState.Dropped));
        Assert.That(dropped.BlueFlag.DropPosition, Is.EqualTo(position));
        Assert.That(dropped.BlueFlag.ReturnAt, Is.EqualTo(105d));
        Assert.That(dropped.HasProcessedDeath(10UL), Is.True);
        Assert.That(carried.BlueFlag.State, Is.EqualTo(FlagState.Carried));

        CtfMatchState pickedAgain = PickUp(dropped,
            TeamId.Blue, TeamId.Red, 0UL, 101d);
        Assert.That(CtfRules.TryDropCarriedFlag(pickedAgain, death, position,
            102d, MatchPhase.Playing, out CtfMatchState duplicate), Is.False);
        Assert.That(duplicate, Is.SameAs(pickedAgain));
    }

    [Test]
    public void OwnTeamReturnsDroppedFlagAndEnemyCanPickItUp()
    {
        CtfMatchState carried = PickUp(new CtfMatchState(),
            TeamId.Red, TeamId.Blue, 1UL);
        var death = new PlayerDeathInfo(11UL, 1UL, TeamId.Blue,
            false, 0UL, TeamId.None, PlayerDeathCause.Environmental);
        Assert.That(CtfRules.TryDropCarriedFlag(carried, death,
            Vector3.zero, 20d, MatchPhase.Playing,
            out CtfMatchState dropped), Is.True);

        Assert.That(CtfRules.TryReturnOwnDroppedFlag(dropped,
            TeamId.Red, TeamId.Blue, MatchPhase.Playing, out _), Is.False);
        Assert.That(CtfRules.TryReturnOwnDroppedFlag(dropped,
            TeamId.Red, TeamId.Red, MatchPhase.Playing,
            out CtfMatchState returned), Is.True);
        Assert.That(returned.RedFlag.State, Is.EqualTo(FlagState.AtBase));

        CtfMatchState enemyPickup = PickUp(dropped,
            TeamId.Red, TeamId.Blue, 1UL, 21d);
        Assert.That(enemyPickup.RedFlag.State, Is.EqualTo(FlagState.Carried));
    }

    [Test]
    public void DroppedFlagReturnsAtDeadlineAndCannotBePickedUpAfterIt()
    {
        CtfMatchState carried = PickUp(new CtfMatchState(),
            TeamId.Blue, TeamId.Red, 0UL);
        var death = new PlayerDeathInfo(12UL, 0UL, TeamId.Red,
            false, 0UL, TeamId.None, PlayerDeathCause.Suicide);
        Assert.That(CtfRules.TryDropCarriedFlag(carried, death,
            Vector3.one, 50d, MatchPhase.Playing,
            out CtfMatchState dropped), Is.True);
        Assert.That(CtfRules.TryReturnExpiredFlags(dropped, 54.9d,
            MatchPhase.Playing, out _), Is.False);
        Assert.That(CtfRules.TryPickUpFlag(dropped, TeamId.Blue,
            TeamId.Red, 0UL, MatchPhase.Playing, 55d, out _), Is.False);
        Assert.That(CtfRules.TryReturnExpiredFlags(dropped, 55d,
            MatchPhase.Playing, out CtfMatchState returned), Is.True);
        Assert.That(returned.BlueFlag.State, Is.EqualTo(FlagState.AtBase));
    }

    [Test]
    public void DisconnectReturnsOnlyFlagsCarriedByThatPlayer()
    {
        CtfMatchState carried = PickUp(new CtfMatchState(),
            TeamId.Blue, TeamId.Red, 0UL);
        Assert.That(CtfRules.TryReturnDisconnectedCarrier(carried, 1UL,
            MatchPhase.Playing, out _), Is.False);
        Assert.That(CtfRules.TryReturnDisconnectedCarrier(carried, 0UL,
            MatchPhase.Countdown, out _), Is.False);
        Assert.That(CtfRules.TryReturnDisconnectedCarrier(carried, 0UL,
            MatchPhase.Playing, out CtfMatchState returned), Is.True);
        Assert.That(returned.BlueFlag.State, Is.EqualTo(FlagState.AtBase));
        Assert.That(carried.BlueFlag.State, Is.EqualTo(FlagState.Carried));
    }

    [TestCase(2, 3, true)]
    [TestCase(4, 5, true)]
    [TestCase(6, 7, true)]
    [TestCase(8, 9, true)]
    [TestCase(3, 0, false)]
    public void CaptureLimitsFollowPlayerCount(int players, int expected,
        bool supported)
    {
        Assert.That(CtfRules.TryGetCaptureLimit(players, out int limit),
            Is.EqualTo(supported));
        Assert.That(limit, Is.EqualTo(expected));
    }

    [Test]
    public void ThreeCapturesFinishTwoPlayerTestButNotBefore()
    {
        CtfMatchState state = new CtfMatchState();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(CtfRules.TryResolveCaptureLimit(state, 2,
                MatchPhase.Playing, out _), Is.False);
            state = PickUp(state, TeamId.Blue, TeamId.Red, 0UL);
            state = Capture(state, TeamId.Red, 0UL);
        }

        Assert.That(CtfRules.TryResolveCaptureLimit(state, 2,
            MatchPhase.Playing, out MatchResultData result), Is.True);
        Assert.That(result.WinningTeam, Is.EqualTo(TeamId.Red));
        Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.ScoreLimit));
        Assert.That(CtfRules.TryResolveCaptureLimit(state, 2,
            MatchPhase.Finished, out _), Is.False);
    }

    [Test]
    public void TimeoutUsesCapturesAndAllowsDraw()
    {
        CtfMatchState tied = new CtfMatchState();
        Assert.That(CtfRules.TryResolveTimeExpired(tied,
            MatchPhase.Playing, out MatchResultData draw), Is.True);
        Assert.That(draw.IsDraw, Is.True);

        CtfMatchState blueLeads = PickUp(tied,
            TeamId.Red, TeamId.Blue, 1UL);
        blueLeads = Capture(blueLeads, TeamId.Blue, 1UL);
        Assert.That(CtfRules.TryResolveTimeExpired(blueLeads,
            MatchPhase.Playing, out MatchResultData result), Is.True);
        Assert.That(result.WinningTeam, Is.EqualTo(TeamId.Blue));
        Assert.That(result.IsDraw, Is.False);
        Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
    }
}
