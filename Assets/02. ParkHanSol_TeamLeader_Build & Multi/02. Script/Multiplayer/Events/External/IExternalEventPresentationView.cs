namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.External
{
    public interface IExternalEventPresentationView
    {
        PHSExternalEventPresentationPhase CurrentPhase { get; }

        void ShowTelegraph();

        void ShowActive();

        void ShowResolved();

        void ShowFailed();

        void Cleanup();
    }
}
