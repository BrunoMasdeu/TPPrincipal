using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

public class NetworkDataSerializationTests
{
    [Test]
    public void LobbyPlayerDataComparaTodosSusCampos()
    {
        LobbyPlayerData original = new(7, TeamId.Red, true);

        Assert.That(original, Is.EqualTo(new LobbyPlayerData(7, TeamId.Red, true)));
        Assert.That(original, Is.Not.EqualTo(new LobbyPlayerData(7, TeamId.Blue, true)));
        Assert.That(original, Is.Not.EqualTo(new LobbyPlayerData(7, TeamId.Red, false)));
    }

    [Test]
    public void LobbyPlayerDataCompletaRoundTripDeRed()
    {
        LobbyPlayerData original = new(11, TeamId.Blue, true);

        Assert.That(RoundTrip(original), Is.EqualTo(original));
    }

    [Test]
    public void PlayerDeathInfoDistingueLasCausasRequeridas()
    {
        Assert.That(
            new PlayerDeathInfo(
                1, 10, TeamId.Red, true, 20, TeamId.Blue,
                PlayerDeathCause.PlayerAttack
            ).Cause,
            Is.EqualTo(PlayerDeathCause.PlayerAttack)
        );
        Assert.That(
            new PlayerDeathInfo(
                2, 10, TeamId.Red, true, 10, TeamId.Red,
                PlayerDeathCause.Suicide
            ).Cause,
            Is.EqualTo(PlayerDeathCause.Suicide)
        );
        Assert.That(
            new PlayerDeathInfo(
                3, 10, TeamId.Red, false, 0, TeamId.None,
                PlayerDeathCause.Environmental
            ).Cause,
            Is.EqualTo(PlayerDeathCause.Environmental)
        );
        Assert.That(default(PlayerDeathInfo).Cause, Is.EqualTo(PlayerDeathCause.Unknown));
    }

    [Test]
    public void EventIdPermiteDetectarUnaMuerteDuplicada()
    {
        PlayerDeathInfo first = new(
            99,
            10,
            TeamId.Red,
            true,
            20,
            TeamId.Blue,
            PlayerDeathCause.PlayerAttack
        );
        PlayerDeathInfo duplicateId = new(
            99,
            30,
            TeamId.Blue,
            false,
            0,
            TeamId.None,
            PlayerDeathCause.Environmental
        );
        PlayerDeathInfo differentId = first;
        differentId.EventId = 100;

        Assert.That(first.IsSameEventAs(duplicateId), Is.True);
        Assert.That(first.IsSameEventAs(differentId), Is.False);
        Assert.That(first, Is.Not.EqualTo(duplicateId));
    }

    [Test]
    public void PlayerDeathInfoCompletaRoundTripDeRed()
    {
        PlayerDeathInfo original = new(
            15,
            3,
            TeamId.Blue,
            true,
            9,
            TeamId.Red,
            PlayerDeathCause.PlayerAttack
        );

        Assert.That(RoundTrip(original), Is.EqualTo(original));
    }

    [Test]
    public void PlayerMatchStatsIncluyeJugadorEquipoKillsYMuertes()
    {
        PlayerMatchStats original = new(8, TeamId.Red, 4, 2);

        Assert.That(RoundTrip(original), Is.EqualTo(original));
        Assert.That(original, Is.Not.EqualTo(new PlayerMatchStats(8, TeamId.Blue, 4, 2)));
    }

    [Test]
    public void MatchResultDataRepresentaResultadosComunes()
    {
        MatchResultData pending = default;
        MatchResultData redWin = new(
            TeamId.Red,
            MatchEndReason.ScoreLimit,
            false
        );
        MatchResultData draw = new(
            TeamId.None,
            MatchEndReason.TimeExpired,
            true
        );
        MatchResultData cancelled = new(
            TeamId.None,
            MatchEndReason.Cancelled,
            false
        );

        Assert.That(pending.IsPending, Is.True);
        Assert.That(redWin.WinningTeam, Is.EqualTo(TeamId.Red));
        Assert.That(draw.IsDraw, Is.True);
        Assert.That(cancelled.EndReason, Is.EqualTo(MatchEndReason.Cancelled));
        Assert.That(RoundTrip(redWin), Is.EqualTo(redWin));
    }

    private static T RoundTrip<T>(T value)
        where T : INetworkSerializable, new()
    {
        using FastBufferWriter writer = new(128, Allocator.Temp);
        writer.WriteNetworkSerializable(value);

        using FastBufferReader reader = new(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out T copy);
        return copy;
    }
}
