namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface IDebrisHolder
    {
        DebrisItem HeldDebris { get; }
        float HeldDebrisMass { get; }

        bool CanHoldDebris(DebrisItem debrisItem);
        bool TryHoldDebris(DebrisItem debrisItem);
    }
}
