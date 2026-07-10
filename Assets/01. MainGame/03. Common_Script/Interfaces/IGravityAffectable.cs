namespace LastJumpCrew.Common
{
    public interface IGravityAffectable
    {
        void EnterGravitySource(IGravitySource gravitySource);
        void ExitGravitySource(IGravitySource gravitySource);
        void RefreshGravitySource(IGravitySource gravitySource);
    }
}
