namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface INetworkSession
    {
        bool IsRunning { get; }
        bool StartHost();
        bool StartClient();
        bool StartServer();
        void Shutdown();
    }
}
