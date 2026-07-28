namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface INetworkRunRestartService
    {
        uint RestartEpoch { get; }
        NetworkRunRestartState RestartState { get; }
        string LastFailureReason { get; }
        bool IsRestartInProgress { get; }
        bool BlocksRun { get; }

        bool CanRequestRestart(out string reason);
        bool TryRequestRestart(out string reason);
    }
}
