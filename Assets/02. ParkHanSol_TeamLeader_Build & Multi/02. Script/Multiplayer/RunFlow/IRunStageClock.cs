using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IRunStageClock
    {
        NetworkRunStageClockSnapshot Snapshot { get; }
        int MapId { get; }
        uint StageSequence { get; }
        uint Revision { get; }
        NetworkRunStageClockState State { get; }
        float RemainingSeconds { get; }

        event Action<
            NetworkRunStageClockSnapshot,
            NetworkRunStageClockSnapshot> SnapshotChanged;
        event Action<NetworkRunStageClockSnapshot> ExpiredServer;

        bool TryStartServer(int mapId, float durationSeconds, out string reason);
        bool TryPauseServer(out string reason);
        bool TryResumeServer(out string reason);
        bool TryStopServer(out string reason);
        bool TryMarkExpiredServer(out string reason);
    }
}
