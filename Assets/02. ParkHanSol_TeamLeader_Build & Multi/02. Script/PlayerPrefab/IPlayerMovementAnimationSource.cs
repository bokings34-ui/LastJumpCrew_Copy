namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    public interface IPlayerMovementAnimationSource
    {
        bool IsGrounded { get; }
        bool HasMoveInput { get; }
        bool IsRunning { get; }
        float VerticalVelocity { get; }
    }
}
