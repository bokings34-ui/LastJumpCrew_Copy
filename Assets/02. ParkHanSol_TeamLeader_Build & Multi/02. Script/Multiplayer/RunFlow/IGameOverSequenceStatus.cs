using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGameOverSequenceStatus
    {
        NetworkGameOverSequenceSnapshot Snapshot { get; }
        NetworkGameOverSequenceState State { get; }
        uint Revision { get; }
        bool IsPlaying { get; }
        bool IsCompleted { get; }

        event Action<
            NetworkGameOverSequenceSnapshot,
            NetworkGameOverSequenceSnapshot> SequenceChanged;
    }
}
