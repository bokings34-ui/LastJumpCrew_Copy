using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum GrapplePullMode
    {
        PullOwner,
        MassBalanced,
        PullTarget
    }

    public interface IGrappleTarget
    {
        Transform GrapplePoint { get; }
        bool CanMoveByGrapple { get; }
        float GrappleMass { get; }
        GrapplePullMode PullMode { get; }

        void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime);
    }
}
