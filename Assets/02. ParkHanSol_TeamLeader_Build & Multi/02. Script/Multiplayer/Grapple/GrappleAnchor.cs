using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class GrappleAnchor : MonoBehaviour, IGrappleAnchor
    {
        [SerializeField] private Transform grapplePoint;

        public Transform GrapplePoint => grapplePoint;
        public bool CanMoveByGrapple => false;
        public float GrappleMass => float.PositiveInfinity;
        public GrapplePullMode PullMode => GrapplePullMode.PullOwner;

        private void Awake()
        {
            if (grapplePoint == null)
            {
                Debug.LogError($"PHS_GRAPPLE_ANCHOR_SETUP_FAILED reason=grapple_point_missing anchor={name}");
            }
        }

        public void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime)
        {
        }
    }
}
