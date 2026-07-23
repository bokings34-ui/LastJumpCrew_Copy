namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkRunRestartState : byte
    {
        Idle = 0,
        LoadingScene = 1,
        Committing = 2,
        Completed = 3,
        Failed = 4,
    }
}
