namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IIncidentScheduleConfigurator
    {
        bool TryConfigureServer(
            RunIncidentScheduleDefinition definition,
            out string reason);

        bool TrySetSchedulingEnabledServer(
            bool schedulingEnabled,
            out string reason);

        bool TryCancelScheduleServer(
            string cause,
            out string reason);
    }
}
