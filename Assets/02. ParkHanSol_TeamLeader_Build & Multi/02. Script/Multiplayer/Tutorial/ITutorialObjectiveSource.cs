using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public interface ITutorialObjectiveSource
    {
        string ObjectiveId { get; }
        bool IsComplete { get; }
        event Action<ITutorialObjectiveSource> Completed;

        void SetObjectiveActive(bool active);
    }
}
