using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>Vista replicada de una bandera; las reglas completas viven sólo en el servidor.</summary>
public struct CtfFlagSnapshot : INetworkSerializable, IEquatable<CtfFlagSnapshot>
{
    public FlagState State;
    public ulong CarrierClientId;
    public Vector3 DropPosition;
    public double ReturnAt;

    public CtfFlagSnapshot(CtfFlagData flag)
    {
        State = flag.State;
        CarrierClientId = flag.CarrierClientId;
        DropPosition = flag.DropPosition;
        ReturnAt = flag.ReturnAt;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref State);
        serializer.SerializeValue(ref CarrierClientId);
        serializer.SerializeValue(ref DropPosition);
        serializer.SerializeValue(ref ReturnAt);
    }

    public bool Equals(CtfFlagSnapshot other) =>
        State == other.State && CarrierClientId == other.CarrierClientId &&
        DropPosition == other.DropPosition && ReturnAt.Equals(other.ReturnAt);

    public override bool Equals(object obj) =>
        obj is CtfFlagSnapshot other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(State, CarrierClientId, DropPosition, ReturnAt);
}
