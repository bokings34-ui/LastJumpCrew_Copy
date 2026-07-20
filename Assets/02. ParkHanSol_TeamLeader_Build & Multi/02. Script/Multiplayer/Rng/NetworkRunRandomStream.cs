namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Stable semantic domains for run-scoped deterministic randomness.
    /// Numeric values are content contracts and must not be renumbered.
    /// </summary>
    public enum NetworkRunRandomStream : uint
    {
        None = 0,
        MapChoice = 100,
        ExternalThreat = 200,
        InternalAccident = 300,
        InternalAccidentAnchor = 301,
        DebrisLayout = 400,
        DebrisRecycle = 401,
        ShopStock = 500,
        IncidentSpread = 600
    }
}
