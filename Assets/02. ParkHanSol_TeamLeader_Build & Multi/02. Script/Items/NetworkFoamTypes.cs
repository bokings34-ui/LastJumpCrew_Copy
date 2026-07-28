using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum NetworkFoamBlobPhase : byte
    {
        Flying = 1,
        Attached = 2,
        Hardened = 3,
        Dissolving = 4
    }

    public enum NetworkFoamTargetKind : byte
    {
        Surface = 1,
        Fire = 2,
        HullBreach = 3
    }

    public enum NetworkFoamTargetState : byte
    {
        Accumulating = 1,
        Hardened = 2,
        Completed = 3,
        Dissolving = 4
    }

    public struct NetworkFoamBlobSnapshot :
        INetworkSerializable,
        IEquatable<NetworkFoamBlobSnapshot>
    {
        public byte PhaseValue;
        public byte TargetKindValue;
        public uint ClusterId;
        public Vector3 LaunchOrigin;
        public Vector3 LaunchDirection;
        public Vector3 AttachedPosition;
        public Vector3 AttachedNormal;
        public float Speed;
        public double LaunchServerTime;
        public double PhaseServerTime;
        public double ExpireServerTime;

        public NetworkFoamBlobPhase Phase =>
            (NetworkFoamBlobPhase)PhaseValue;
        public NetworkFoamTargetKind TargetKind =>
            (NetworkFoamTargetKind)TargetKindValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref PhaseValue);
            serializer.SerializeValue(ref TargetKindValue);
            serializer.SerializeValue(ref ClusterId);
            serializer.SerializeValue(ref LaunchOrigin);
            serializer.SerializeValue(ref LaunchDirection);
            serializer.SerializeValue(ref AttachedPosition);
            serializer.SerializeValue(ref AttachedNormal);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref LaunchServerTime);
            serializer.SerializeValue(ref PhaseServerTime);
            serializer.SerializeValue(ref ExpireServerTime);
        }

        public bool Equals(NetworkFoamBlobSnapshot other)
        {
            return PhaseValue == other.PhaseValue
                && TargetKindValue == other.TargetKindValue
                && ClusterId == other.ClusterId
                && LaunchOrigin.Equals(other.LaunchOrigin)
                && LaunchDirection.Equals(other.LaunchDirection)
                && AttachedPosition.Equals(other.AttachedPosition)
                && AttachedNormal.Equals(other.AttachedNormal)
                && Speed.Equals(other.Speed)
                && LaunchServerTime.Equals(other.LaunchServerTime)
                && PhaseServerTime.Equals(other.PhaseServerTime)
                && ExpireServerTime.Equals(other.ExpireServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkFoamBlobSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(
                    PhaseValue,
                    TargetKindValue,
                    ClusterId,
                    LaunchOrigin,
                    LaunchDirection,
                    AttachedPosition),
                HashCode.Combine(
                    AttachedNormal,
                    Speed,
                    LaunchServerTime,
                    PhaseServerTime,
                    ExpireServerTime));
        }
    }

    public struct NetworkFoamTargetSnapshot :
        INetworkSerializable,
        IEquatable<NetworkFoamTargetSnapshot>
    {
        public FixedString64Bytes TargetKey;
        public byte KindValue;
        public byte StateValue;
        public byte Current;
        public byte Required;
        public Vector3 WorldPosition;
        public Vector3 SurfaceNormal;
        public uint Revision;

        public NetworkFoamTargetKind Kind =>
            (NetworkFoamTargetKind)KindValue;
        public NetworkFoamTargetState State =>
            (NetworkFoamTargetState)StateValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref TargetKey);
            serializer.SerializeValue(ref KindValue);
            serializer.SerializeValue(ref StateValue);
            serializer.SerializeValue(ref Current);
            serializer.SerializeValue(ref Required);
            serializer.SerializeValue(ref WorldPosition);
            serializer.SerializeValue(ref SurfaceNormal);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkFoamTargetSnapshot other)
        {
            return TargetKey.Equals(other.TargetKey)
                && KindValue == other.KindValue
                && StateValue == other.StateValue
                && Current == other.Current
                && Required == other.Required
                && WorldPosition.Equals(other.WorldPosition)
                && SurfaceNormal.Equals(other.SurfaceNormal)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkFoamTargetSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                TargetKey,
                KindValue,
                StateValue,
                Current,
                Required,
                WorldPosition,
                SurfaceNormal,
                Revision);
        }
    }
}
