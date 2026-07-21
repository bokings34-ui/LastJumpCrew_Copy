namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    public interface IShipAccidentScheduleConfigurator
    {
        bool TryConfigureServer(
            PHSMapShipAccidentWeight[] entries,
            float intervalMinSeconds,
            float intervalMaxSeconds,
            int maximumActiveAccidents,
            float moduleDamageMultiplier,
            float shipDamageMultiplier,
            out string reason);

        bool TryStartServer(out string reason);

        bool TryStopServer(out string reason);
    }
}
