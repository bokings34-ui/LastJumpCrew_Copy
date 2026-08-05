using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_ItemDropMotionProfile",
        menuName = "LastJumpCrew/ParkHanSol/Item Drop Motion Profile")]
    public sealed class ItemDropMotionProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float forwardSpeed = 0.65f;
        [SerializeField, Min(0f)] private float downwardSpeed = 0.25f;
        [SerializeField] private Vector3 localAngularVelocity = new(1.1f, 0.35f, 0.75f);
        [SerializeField, Min(0.01f)] private float floorProbeStartHeight = 2f;
        [SerializeField, Min(0.01f)] private float floorProbeDistance = 5f;
        [SerializeField, Min(0f)] private float floorClearance = 0.02f;

        public bool TryApply(Rigidbody targetRigidbody, Quaternion sourceRotation)
        {
            if (targetRigidbody == null)
            {
                return false;
            }

            targetRigidbody.isKinematic = false;
            targetRigidbody.detectCollisions = true;
            targetRigidbody.linearVelocity =
                sourceRotation * Vector3.forward * forwardSpeed
                + Vector3.down * downwardSpeed;
            ApplyAngularVelocity(targetRigidbody, sourceRotation);
            targetRigidbody.WakeUp();
            return true;
        }

        public bool TryApplyAngularVelocity(
            Rigidbody targetRigidbody,
            Quaternion sourceRotation)
        {
            if (targetRigidbody == null)
            {
                return false;
            }

            ApplyAngularVelocity(targetRigidbody, sourceRotation);
            targetRigidbody.WakeUp();
            return true;
        }

        public bool TryResolveFloorPlacement(
            Rigidbody targetRigidbody,
            Vector3 requestedPosition,
            Quaternion sourceRotation,
            Transform ignoredRoot,
            out Vector3 resolvedPosition,
            out Quaternion resolvedRotation)
        {
            resolvedPosition = requestedPosition;
            resolvedRotation = Quaternion.Euler(0f, sourceRotation.eulerAngles.y, 0f);

            if (targetRigidbody == null || ignoredRoot == null)
            {
                return false;
            }

            targetRigidbody.rotation = resolvedRotation;
            var colliders = targetRigidbody.GetComponentsInChildren<Collider>(true);
            var halfHeight = 0f;
            foreach (var targetCollider in colliders)
            {
                if (targetCollider != null)
                {
                    halfHeight = Mathf.Max(halfHeight, targetCollider.bounds.extents.y);
                }
            }

            if (halfHeight <= 0f)
            {
                return false;
            }

            var rayOrigin = requestedPosition + Vector3.up * floorProbeStartHeight;
            var hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                floorProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            var closestDistance = float.MaxValue;
            RaycastHit floorHit = default;
            var foundFloor = false;

            foreach (var hit in hits)
            {
                var hitTransform = hit.collider == null ? null : hit.collider.transform;
                if (hitTransform == null
                    || hitTransform.root == ignoredRoot
                    || hitTransform.root == targetRigidbody.transform.root
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                floorHit = hit;
                foundFloor = true;
            }

            if (!foundFloor)
            {
                return false;
            }

            resolvedPosition = floorHit.point + Vector3.up * (halfHeight + floorClearance);
            return true;
        }

        private void ApplyAngularVelocity(
            Rigidbody targetRigidbody,
            Quaternion sourceRotation)
        {
            targetRigidbody.angularVelocity =
                sourceRotation * localAngularVelocity;
        }
    }
}
