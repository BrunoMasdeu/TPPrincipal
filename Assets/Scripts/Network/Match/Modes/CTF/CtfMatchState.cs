using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Datos de una bandera; el ID 0 también puede ser un portador válido.</summary>
public readonly struct CtfFlagData
{
    public FlagState State { get; }
    public ulong CarrierClientId { get; }
    public Vector3 DropPosition { get; }
    public double ReturnAt { get; }

    private CtfFlagData(FlagState state, ulong carrierClientId,
        Vector3 dropPosition, double returnAt)
    {
        State = state;
        CarrierClientId = carrierClientId;
        DropPosition = dropPosition;
        ReturnAt = returnAt;
    }

    public static CtfFlagData AtBase() =>
        new CtfFlagData(FlagState.AtBase, 0UL, default, 0d);

    public static CtfFlagData CarriedBy(ulong clientId) =>
        new CtfFlagData(FlagState.Carried, clientId, default, 0d);

    public static CtfFlagData DroppedAt(Vector3 position, double returnAt) =>
        new CtfFlagData(FlagState.Dropped, 0UL, position, returnAt);
}

/// <summary>Estado puro de CTF. Cada transición válida devuelve una copia nueva.</summary>
public sealed class CtfMatchState
{
    private readonly HashSet<ulong> processedDeathEventIds;

    public CtfFlagData RedFlag { get; }
    public CtfFlagData BlueFlag { get; }
    public int RedCaptures { get; }
    public int BlueCaptures { get; }

    public CtfMatchState() : this(CtfFlagData.AtBase(), CtfFlagData.AtBase(),
        0, 0, new HashSet<ulong>())
    {
    }

    private CtfMatchState(CtfFlagData redFlag, CtfFlagData blueFlag,
        int redCaptures, int blueCaptures, HashSet<ulong> deathEventIds)
    {
        RedFlag = redFlag;
        BlueFlag = blueFlag;
        RedCaptures = redCaptures;
        BlueCaptures = blueCaptures;
        processedDeathEventIds = new HashSet<ulong>(deathEventIds);
    }

    public bool HasProcessedDeath(ulong eventId) =>
        processedDeathEventIds.Contains(eventId);

    public CtfFlagData GetFlag(TeamId flagTeam) => flagTeam switch
    {
        TeamId.Red => RedFlag,
        TeamId.Blue => BlueFlag,
        _ => throw new ArgumentOutOfRangeException(nameof(flagTeam))
    };

    internal CtfMatchState WithFlag(TeamId flagTeam, CtfFlagData flag,
        ulong deathEventId = 0UL)
    {
        var eventIds = new HashSet<ulong>(processedDeathEventIds);
        if (deathEventId != 0UL)
            eventIds.Add(deathEventId);

        return new CtfMatchState(
            flagTeam == TeamId.Red ? flag : RedFlag,
            flagTeam == TeamId.Blue ? flag : BlueFlag,
            RedCaptures, BlueCaptures, eventIds);
    }

    internal CtfMatchState WithCapture(TeamId scoringTeam) =>
        new CtfMatchState(
            scoringTeam == TeamId.Blue ? CtfFlagData.AtBase() : RedFlag,
            scoringTeam == TeamId.Red ? CtfFlagData.AtBase() : BlueFlag,
            RedCaptures + (scoringTeam == TeamId.Red ? 1 : 0),
            BlueCaptures + (scoringTeam == TeamId.Blue ? 1 : 0),
            processedDeathEventIds);
}
