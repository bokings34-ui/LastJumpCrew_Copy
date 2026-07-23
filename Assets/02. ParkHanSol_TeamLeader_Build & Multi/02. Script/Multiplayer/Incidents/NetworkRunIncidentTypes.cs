namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkRunIncidentStageState : byte
    {
        Inactive = 0,
        Active = 1,
        Cancelled = 2
    }

    public enum NetworkRunIncidentChannel : byte
    {
        External = 1,
        Internal = 2
    }

    public enum NetworkRunIncidentPayloadKind : byte
    {
        EventManagerEvent = 1,
        ShipAccident = 2
    }

    public enum NetworkRunIncidentFamily : byte
    {
        None = 0,
        Fire = 1,
        Power = 2,
        Oxygen = 3,
        Device = 4,
        Hull = 5,
        Steam = 6,
        Gravity = 7,
        Enemy = 8,
        Meteor = 9,
        EMP = 10
    }

    public enum NetworkRunIncidentSourceKind : byte
    {
        Scheduled = 1,
        Consequence = 2,
        Device = 3,
        Terminal = 4,
        Validation = 5
    }

    public enum NetworkRunIncidentCommandState : byte
    {
        Pending = 0,
        Claimed = 1,
        Active = 2,
        Resolved = 3,
        Failed = 4,
        Cancelled = 5
    }
}
