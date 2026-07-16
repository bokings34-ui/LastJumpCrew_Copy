using System;
using SM;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public struct NetworkEventEffectSnapshot :
        INetworkSerializable,
        IEquatable<NetworkEventEffectSnapshot>
    {
        public ulong EventInstanceId;
        public uint EffectInstanceId;
        public byte KindValue;
        public Vector3 WorldPosition;
        public byte Variant;
        public byte LifecycleValue;
        public uint Revision;
        public double ChangedAtServerTime;

        public EventEffectKind Kind => (EventEffectKind)KindValue;
        public EventEffectLifecycle Lifecycle => (EventEffectLifecycle)LifecycleValue;
        public bool IsActive => Lifecycle == EventEffectLifecycle.Active;

        public NetworkEventEffectSnapshot(
            ulong eventInstanceId,
            uint effectInstanceId,
            EventEffectKind kind,
            Vector3 worldPosition,
            byte variant,
            EventEffectLifecycle lifecycle,
            uint revision,
            double changedAtServerTime)
        {
            EventInstanceId = eventInstanceId;
            EffectInstanceId = effectInstanceId;
            KindValue = (byte)kind;
            WorldPosition = worldPosition;
            Variant = variant;
            LifecycleValue = (byte)lifecycle;
            Revision = revision;
            ChangedAtServerTime = changedAtServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EventInstanceId);
            serializer.SerializeValue(ref EffectInstanceId);
            serializer.SerializeValue(ref KindValue);
            serializer.SerializeValue(ref WorldPosition);
            serializer.SerializeValue(ref Variant);
            serializer.SerializeValue(ref LifecycleValue);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref ChangedAtServerTime);
        }

        public bool Equals(NetworkEventEffectSnapshot other)
        {
            return EventInstanceId == other.EventInstanceId
                && EffectInstanceId == other.EffectInstanceId
                && KindValue == other.KindValue
                && WorldPosition.Equals(other.WorldPosition)
                && Variant == other.Variant
                && LifecycleValue == other.LifecycleValue
                && Revision == other.Revision
                && ChangedAtServerTime.Equals(other.ChangedAtServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkEventEffectSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                EventInstanceId,
                EffectInstanceId,
                KindValue,
                WorldPosition,
                Variant,
                LifecycleValue,
                Revision,
                ChangedAtServerTime);
        }
    }
}
