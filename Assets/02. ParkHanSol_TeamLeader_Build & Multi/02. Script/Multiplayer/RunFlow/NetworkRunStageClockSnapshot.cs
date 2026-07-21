using System;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkRunStageClockState : byte
    {
        Stopped = 0,
        Running = 1,
        Paused = 2,
        Expired = 3
    }

    public struct NetworkRunStageClockSnapshot :
        INetworkSerializable,
        IEquatable<NetworkRunStageClockSnapshot>
    {
        public int MapId;
        public uint StageSequence;
        public uint Revision;
        public NetworkRunStageClockState State;
        public double DeadlineServerTime;
        public float FrozenRemainingSeconds;

        public NetworkRunStageClockSnapshot(
            int mapId,
            uint stageSequence,
            uint revision,
            NetworkRunStageClockState state,
            double deadlineServerTime,
            float frozenRemainingSeconds)
        {
            MapId = mapId;
            StageSequence = stageSequence;
            Revision = revision;
            State = state;
            DeadlineServerTime = deadlineServerTime;
            FrozenRemainingSeconds = frozenRemainingSeconds;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref MapId);
            serializer.SerializeValue(ref StageSequence);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref DeadlineServerTime);
            serializer.SerializeValue(ref FrozenRemainingSeconds);
        }

        public bool Equals(NetworkRunStageClockSnapshot other)
        {
            return MapId == other.MapId
                && StageSequence == other.StageSequence
                && Revision == other.Revision
                && State == other.State
                && DeadlineServerTime.Equals(other.DeadlineServerTime)
                && FrozenRemainingSeconds.Equals(other.FrozenRemainingSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunStageClockSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                MapId,
                StageSequence,
                Revision,
                (byte)State,
                DeadlineServerTime,
                FrozenRemainingSeconds);
        }
    }
}
