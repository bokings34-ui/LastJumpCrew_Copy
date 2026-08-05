namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface INetworkPlayerLifeState
    {
        bool IsAlive { get; }
        bool IsWaitingForWarpRevive { get; }
        bool IsWaitingForAutomaticRespawn { get; }
        float RespawnRemainingSeconds { get; }
        void BeginDeadZoneWarning(float warningSeconds);
        void CancelDeadZoneWarning();
        void KillForContainmentBreach();
        void KillForWarp();
        bool TryReviveAfterWarp();
    }
}
