using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSFirstPersonCameraWallProtection : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField, Min(0.01f)] private float probeRadius = 0.12f;
        [SerializeField, Min(0f)] private float wallClearance = 0.06f;

        private readonly RaycastHit[] wallHits = new RaycastHit[12];
        private readonly Collider[] overlapHits = new Collider[12];
        private Vector3 intendedLocalPosition;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                Debug.LogError($"PHS_CAMERA_WALL_PROTECTION_FAILED reason=camera_transform_missing target={name}", this);
                enabled = false;
                return;
            }

            intendedLocalPosition = cameraTransform.localPosition;
        }

        private void LateUpdate()
        {
            cameraTransform.localPosition = intendedLocalPosition;

            var origin = transform.position;
            var target = transform.TransformPoint(intendedLocalPosition);
            var offset = target - origin;
            var targetDistance = offset.magnitude;
            if (targetDistance <= Mathf.Epsilon)
            {
                return;
            }

            var direction = offset / targetDistance;
            var allowedDistance = targetDistance;
            var hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                direction,
                wallHits,
                targetDistance + wallClearance,
                collisionLayers,
                QueryTriggerInteraction.Ignore);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = wallHits[index];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform.root))
                {
                    continue;
                }

                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - wallClearance));
            }

            if (allowedDistance >= targetDistance
                && HasExternalWallOverlap(target))
            {
                allowedDistance = 0f;
            }

            cameraTransform.position = origin + direction * allowedDistance;
        }

        private bool HasExternalWallOverlap(Vector3 target)
        {
            var overlapCount = Physics.OverlapSphereNonAlloc(
                target,
                probeRadius,
                overlapHits,
                collisionLayers,
                QueryTriggerInteraction.Ignore);
            for (var index = 0; index < overlapCount; index++)
            {
                var overlap = overlapHits[index];
                if (overlap != null && !overlap.transform.IsChildOf(transform.root))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
