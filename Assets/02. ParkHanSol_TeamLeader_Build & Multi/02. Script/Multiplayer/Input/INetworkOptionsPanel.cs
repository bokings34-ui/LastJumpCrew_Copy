namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public interface INetworkOptionsPanel
    {
        bool IsOpen { get; }
        bool IsRebinding { get; }
        bool ConsumedCancelThisFrame { get; }
        void Open();
        void Close();
        void CloseWithoutNotification();
    }
}
