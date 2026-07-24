using System;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkGameOverSequenceState : byte
    {
        Idle = 0,
        Playing = 1,
        Completed = 2
    }

    public struct NetworkGameOverSequenceSnapshot :
        INetworkSerializable,
        IEquatable<NetworkGameOverSequenceSnapshot>
    {
        public NetworkGameOverSequenceState State;
        public GameOverReason Reason;
        public uint Revision;
        public double StartedServerTime;
        public double CompletesServerTime;

        public NetworkGameOverSequenceSnapshot(
            NetworkGameOverSequenceState state,
            GameOverReason reason,
            uint revision,
            double startedServerTime,
            double completesServerTime)
        {
            State = state;
            Reason = reason;
            Revision = revision;
            StartedServerTime = startedServerTime;
            CompletesServerTime = completesServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref Reason);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref StartedServerTime);
            serializer.SerializeValue(ref CompletesServerTime);
        }

        public bool Equals(NetworkGameOverSequenceSnapshot other)
        {
            return State == other.State
                && Reason == other.Reason
                && Revision == other.Revision
                && StartedServerTime.Equals(other.StartedServerTime)
                && CompletesServerTime.Equals(other.CompletesServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkGameOverSequenceSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                (byte)State,
                (int)Reason,
                Revision,
                StartedServerTime,
                CompletesServerTime);
        }
    }
}
