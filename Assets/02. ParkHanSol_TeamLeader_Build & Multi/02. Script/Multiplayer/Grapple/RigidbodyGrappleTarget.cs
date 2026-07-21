using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyGrappleTarget : MonoBehaviour, IGrappleTarget
    {
        [SerializeField] private Transform grapplePoint;

        private Rigidbody targetRigidbody;

        public Transform GrapplePoint => grapplePoint;
        public bool CanMoveByGrapple => targetRigidbody != null && !targetRigidbody.isKinematic;
        public float GrappleMass => targetRigidbody == null ? 0f : Mathf.Max(0.1f, targetRigidbody.mass);
        public GrapplePullMode PullMode => GrapplePullMode.MassBalanced;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            if (grapplePoint == null)
            {
                Debug.LogError($"PHS_RIGIDBODY_GRAPPLE_TARGET_SETUP_FAILED reason=grapple_point_missing target={name}");
            }
        }

        public void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime)
        {
            if (!CanMoveByGrapple)
            {
                Debug.LogError($"PHS_RIGIDBODY_GRAPPLE_TARGET_FAILED reason=not_movable target={name}");
                return;
            }

            var offset = targetPosition - targetRigidbody.position;
            if (offset.magnitude <= stopDistance)
            {
                return;
            }

            var pullDirection = offset.normalized;
            var currentVelocity = targetRigidbody.linearVelocity;
            var radialSpeed = Vector3.Dot(currentVelocity, pullDirection);
            var nextRadialSpeed = Mathf.MoveTowards(
                radialSpeed,
                maximumPullSpeed,
                pullAcceleration * deltaTime);
            var tangentialVelocity = currentVelocity - pullDirection * radialSpeed;
            targetRigidbody.linearVelocity = tangentialVelocity + pullDirection * nextRadialSpeed;
        }
    }
}
