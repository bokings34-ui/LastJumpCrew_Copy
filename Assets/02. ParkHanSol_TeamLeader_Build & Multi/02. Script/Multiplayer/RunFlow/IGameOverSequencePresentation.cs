namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGameOverSequencePresentation
    {
        bool IsPresenting { get; }
        uint PresentedRevision { get; }

        void Present(NetworkGameOverSequenceSnapshot snapshot);
        void Complete(NetworkGameOverSequenceSnapshot snapshot);
        void ResetPresentation();
    }
}
