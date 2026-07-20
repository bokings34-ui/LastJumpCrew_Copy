using SM;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public interface IShipEventImpactSink
    {
        bool TryApplyTerminalImpact(
            ulong eventInstanceId,
            EventId eventId,
            bool success,
            out string reason);
    }
}
