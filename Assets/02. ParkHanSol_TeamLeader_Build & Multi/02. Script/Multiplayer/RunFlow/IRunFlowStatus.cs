namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkRunPhase : byte
    {
        Waiting,
        Charging,
        WarpReady,
        Rearming,
        Shop,
        FinalShop,
        Clear,
        GameOver
    }

    public interface IRunFlowStatus
    {
        NetworkRunPhase Phase { get; }
        float WarpChargeNormalized { get; }
        int ClearedZoneCount { get; }
        int CompletedShopCycleCount { get; }
        int SafePlayerCount { get; }
        bool IsFinalShopPending { get; }
    }
}
