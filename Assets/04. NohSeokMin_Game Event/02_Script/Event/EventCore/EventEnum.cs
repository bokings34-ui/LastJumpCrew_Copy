namespace SM
{
    public enum EventType
    {
        Internal = 7100,
        External = 7200,
        Environment = 7300
    }

    public enum EventState
    {
        Ready,
        Trigger,
        InProgress,
        Resolve,
        Fail
    }

    public enum EventId
    {
        Fire = 7101,
        EnemySpawn = 7102,
        PowerOff = 7103,
        OxygenLeak = 7104,
        EngineBreak = 7105,
        MicDestroy = 7106,
        
        EnemyScout = 7201,
        MeteorAttack = 7202,
        EmpAttack = 7203,

        PatrolZone = 7301,
        MeteorZone = 7302,
        NebulaZone = 7303,
        PlanetZone = 7304
    }
}