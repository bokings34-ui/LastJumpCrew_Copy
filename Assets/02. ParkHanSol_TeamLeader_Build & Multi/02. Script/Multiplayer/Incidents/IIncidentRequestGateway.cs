namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IIncidentRequestGateway
    {
        bool IsReady { get; }

        bool TrySubmitServer(
            IIncidentRequestSource source,
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason);
    }
}
