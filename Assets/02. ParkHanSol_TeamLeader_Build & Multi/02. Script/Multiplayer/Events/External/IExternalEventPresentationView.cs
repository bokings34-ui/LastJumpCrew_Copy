namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.External
{
    public interface IExternalEventPresentationView
    {
        PHSExternalEventPresentationPhase CurrentPhase { get; }

        void ShowTelegraph(
            float phaseElapsedSeconds,
            bool allowOneShotAudio);

        void ShowActive(float phaseElapsedSeconds);

        float ShowResolved(
            float phaseElapsedSeconds,
            bool allowOneShotAudio);

        float ShowFailed(
            float phaseElapsedSeconds,
            bool allowOneShotAudio);

        void Cleanup();
    }
}
