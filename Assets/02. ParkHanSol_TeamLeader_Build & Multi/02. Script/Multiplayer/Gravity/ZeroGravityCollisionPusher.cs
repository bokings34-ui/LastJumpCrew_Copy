using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ZeroGravityCollisionPusher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float pushMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float minimumImpulse = 0.35f;
        [SerializeField, Min(0f)] private float maximumImpulse = 8f;
        [SerializeField, Min(0f)] private float collisionSeparationDistance = 0.02f;
        [SerializeField, Min(0f)] private float maximumSeparationDistance = 0.25f;
        [SerializeField] private bool onlyPushInZeroGravity = true;

        private NetworkPlayerController playerController;
        private CharacterController characterController;
        private bool isSeparatingFromCollision;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            characterController = GetComponent<CharacterController>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null)
            {
                return;
            }

            if (playerController == null)
            {
                Debug.LogError($"PHS_ZERO_GRAVITY_COLLISION_FAILED reason=player_controller_missing pusher={name}");
                return;
            }

            if (onlyPushInZeroGravity
                && playerController != null
                && playerController.GravityMode == NetworkPlayerGravityMode.ShipGravity)
            {
                return;
            }

            SeparateFromCollision(hit.collider, hit.normal);
            playerController.ReflectZeroGravityVelocity(hit.normal);

            if (hit.rigidbody == null || hit.rigidbody.isKinematic)
            {
                return;
            }

            var pushDirection = hit.moveDirection;
            if (pushDirection.sqrMagnitude <= 0.001f)
            {
                pushDirection = hit.rigidbody.worldCenterOfMass - transform.position;
            }

            pushDirection.y = Mathf.Abs(pushDirection.y) > 0.15f ? pushDirection.y : 0f;
            if (pushDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            pushDirection.Normalize();

            var playerSpeed = characterController == null ? 0f : characterController.velocity.magnitude;
            var impulse = Mathf.Clamp(
                Mathf.Max(playerSpeed * playerController.SpaceMass * pushMultiplier, minimumImpulse),
                minimumImpulse,
                maximumImpulse);

            hit.rigidbody.AddForceAtPosition(pushDirection * impulse, hit.point, ForceMode.Impulse);
        }

        private void SeparateFromCollision(Collider hitCollider, Vector3 hitNormal)
        {
            if (isSeparatingFromCollision
                || characterController == null
                || hitCollider == null
                || maximumSeparationDistance <= 0f)
            {
                return;
            }

            var separationDirection = hitNormal.sqrMagnitude <= 0.001f
                ? Vector3.zero
                : hitNormal.normalized;
            var separationDistance = collisionSeparationDistance;

            if (Physics.ComputePenetration(
                    characterController,
                    transform.position,
                    transform.rotation,
                    hitCollider,
                    hitCollider.transform.position,
                    hitCollider.transform.rotation,
                    out var penetrationDirection,
                    out var penetrationDistance))
            {
                separationDirection = penetrationDirection;
                separationDistance += penetrationDistance;
            }

            if (separationDirection.sqrMagnitude <= 0.001f || separationDistance <= 0f)
            {
                return;
            }

            separationDistance = Mathf.Min(separationDistance, maximumSeparationDistance);
            isSeparatingFromCollision = true;
            try
            {
                characterController.Move(separationDirection * separationDistance);
            }
            finally
            {
                isSeparatingFromCollision = false;
            }
        }
    }
}
