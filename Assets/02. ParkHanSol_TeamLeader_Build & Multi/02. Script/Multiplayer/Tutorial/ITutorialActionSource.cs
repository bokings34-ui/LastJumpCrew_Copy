using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public interface ITutorialActionSource
    {
        event Action<TutorialActionKind> ActionSucceeded;

        void ReportInteractionSuccess();
    }
}
