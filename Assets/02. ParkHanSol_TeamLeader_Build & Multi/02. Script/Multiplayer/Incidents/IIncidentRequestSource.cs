namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Local scene signal only. Implementations identify a configured route and
    /// location; they never reserve or spawn an incident directly.
    /// </summary>
    public interface IIncidentRequestSource
    {
        string IncidentSourceId { get; }
        string IncidentTargetId { get; }
    }
}
