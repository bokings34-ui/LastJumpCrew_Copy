namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IShipDockRepairCommands
    {
        bool TryRestoreShipDurabilityAtDock(int amount, out string reason);
    }
}
