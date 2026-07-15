namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface INetworkPlayerLifeState
    {
        bool IsAlive { get; }
        bool IsWaitingForWarpRevive { get; }
        void BeginDeadZoneWarning(float warningSeconds);
        void CancelDeadZoneWarning();
        void KillForWarp();
        bool TryReviveAfterWarp();
    }
}
