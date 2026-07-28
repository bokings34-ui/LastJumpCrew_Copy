namespace LastJumpCrew.ParkHanSol.Multiplayer.RunFlow
{
    public interface ILocalGameSessionLauncher
    {
        bool IsLaunching { get; }
        void LaunchSinglePlayer();
    }
}
