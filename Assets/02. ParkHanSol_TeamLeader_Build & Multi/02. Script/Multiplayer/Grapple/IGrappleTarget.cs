using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGrappleTarget
    {
        Transform GrapplePoint { get; }
        bool CanMoveByGrapple { get; }
        float GrappleMass { get; }

        void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime);
    }
}
