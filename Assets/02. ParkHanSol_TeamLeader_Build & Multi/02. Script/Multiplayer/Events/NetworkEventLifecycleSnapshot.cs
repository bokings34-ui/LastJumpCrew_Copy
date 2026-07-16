using System;
using SM;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public struct NetworkEventLifecycleSnapshot :
        INetworkSerializable,
        IEquatable<NetworkEventLifecycleSnapshot>
    {
        public ulong InstanceId;
        public int EventIdValue;
        public FixedString64Bytes RoomId;
        public byte StateValue;
        public uint Revision;
        public double ChangedAtServerTime;

        public EventId EventId => (EventId)EventIdValue;
        public EventState State => (EventState)StateValue;
        public bool IsTerminal => State == EventState.Resolve || State == EventState.Fail;

        public NetworkEventLifecycleSnapshot(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state,
            uint revision,
            double changedAtServerTime)
        {
            InstanceId = instanceId;
            EventIdValue = (int)eventId;
            RoomId = new FixedString64Bytes(roomId ?? string.Empty);
            StateValue = (byte)state;
            Revision = revision;
            ChangedAtServerTime = changedAtServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref EventIdValue);
            serializer.SerializeValue(ref RoomId);
            serializer.SerializeValue(ref StateValue);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref ChangedAtServerTime);
        }

        public bool Equals(NetworkEventLifecycleSnapshot other)
        {
            return InstanceId == other.InstanceId
                && EventIdValue == other.EventIdValue
                && RoomId.Equals(other.RoomId)
                && StateValue == other.StateValue
                && Revision == other.Revision
                && ChangedAtServerTime.Equals(other.ChangedAtServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkEventLifecycleSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                InstanceId,
                EventIdValue,
                RoomId,
                StateValue,
                Revision,
                ChangedAtServerTime);
        }
    }
}
