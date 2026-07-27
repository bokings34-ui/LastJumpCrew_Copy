namespace LastJumpCrew.ParkHanSol.Items
{
    public interface IUtilityItemVfx
    {
        bool IsLoopPlaying { get; }

        void PlayUse();

        void BeginLoop();

        void EndLoop();

        void PlayImpact();

        void StopAll();
    }
}
