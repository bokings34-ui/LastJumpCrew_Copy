namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IDebrisPopulationRuntime
    {
        int ConfiguredDebrisAmount { get; }
        int ActiveDebrisAmount { get; }
        bool ConfigureTargetDebrisCount(int debrisAmount);
    }
}
