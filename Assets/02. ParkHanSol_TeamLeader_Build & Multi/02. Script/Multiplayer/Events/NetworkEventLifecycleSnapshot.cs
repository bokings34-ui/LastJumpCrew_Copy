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
        public ulong CommandId;
        public FixedString64Bytes RoomId;
        public FixedString64Bytes LocationId;
        public byte StateValue;
        public float Progress;
        public float RequiredProgress;
        public bool Success;
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
            : this(
                instanceId,
                eventId,
                0UL,
                roomId,
                roomId,
                state,
                0f,
                0f,
                false,
                revision,
                changedAtServerTime)
        {
        }

        public NetworkEventLifecycleSnapshot(
            ulong instanceId,
            EventId eventId,
            ulong commandId,
            string roomId,
            string locationId,
            EventState state,
            float progress,
            float requiredProgress,
            bool success,
            uint revision,
            double changedAtServerTime)
        {
            InstanceId = instanceId;
            EventIdValue = (int)eventId;
            CommandId = commandId;
            RoomId = new FixedString64Bytes(roomId ?? string.Empty);
            LocationId = new FixedString64Bytes(locationId ?? string.Empty);
            StateValue = (byte)state;
            Progress = progress;
            RequiredProgress = requiredProgress;
            Success = success;
            Revision = revision;
            ChangedAtServerTime = changedAtServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref EventIdValue);
            serializer.SerializeValue(ref CommandId);
            serializer.SerializeValue(ref RoomId);
            serializer.SerializeValue(ref LocationId);
            serializer.SerializeValue(ref StateValue);
            serializer.SerializeValue(ref Progress);
            serializer.SerializeValue(ref RequiredProgress);
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref ChangedAtServerTime);
        }

        public bool Equals(NetworkEventLifecycleSnapshot other)
        {
            return InstanceId == other.InstanceId
                && EventIdValue == other.EventIdValue
                && CommandId == other.CommandId
                && RoomId.Equals(other.RoomId)
                && LocationId.Equals(other.LocationId)
                && StateValue == other.StateValue
                && Progress.Equals(other.Progress)
                && RequiredProgress.Equals(other.RequiredProgress)
                && Success == other.Success
                && Revision == other.Revision
                && ChangedAtServerTime.Equals(other.ChangedAtServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkEventLifecycleSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = InstanceId.GetHashCode();
                hash = (hash * 397) ^ EventIdValue;
                hash = (hash * 397) ^ CommandId.GetHashCode();
                hash = (hash * 397) ^ RoomId.GetHashCode();
                hash = (hash * 397) ^ LocationId.GetHashCode();
                hash = (hash * 397) ^ StateValue;
                hash = (hash * 397) ^ Progress.GetHashCode();
                hash = (hash * 397) ^ RequiredProgress.GetHashCode();
                hash = (hash * 397) ^ Success.GetHashCode();
                hash = (hash * 397) ^ (int)Revision;
                hash = (hash * 397) ^ ChangedAtServerTime.GetHashCode();
                return hash;
            }
        }
    }
}
