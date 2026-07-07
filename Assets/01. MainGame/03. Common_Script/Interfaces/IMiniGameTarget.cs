namespace LastJumpCrew.Common
{
    public interface IMiniGameTarget
    {
        string MiniGameTargetId { get; }
        void OnMiniGameSucceeded();
        void OnMiniGameFailed();
    }
}
