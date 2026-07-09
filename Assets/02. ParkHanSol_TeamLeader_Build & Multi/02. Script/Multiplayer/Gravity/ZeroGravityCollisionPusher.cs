using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ZeroGravityCollisionPusher : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float playerMass = 1f;
        [SerializeField, Min(0f)] private float pushMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float minimumImpulse = 0.35f;
        [SerializeField, Min(0f)] private float maximumImpulse = 8f;
        [SerializeField] private bool onlyPushInZeroGravity = true;

        private NetworkPlayerController playerController;
        private CharacterController characterController;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            characterController = GetComponent<CharacterController>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.rigidbody == null || hit.rigidbody.isKinematic)
            {
                return;
            }

            if (onlyPushInZeroGravity
                && playerController != null
                && playerController.GravityMode == NetworkPlayerGravityMode.ShipGravity)
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
                Mathf.Max(playerSpeed * playerMass * pushMultiplier, minimumImpulse),
                minimumImpulse,
                maximumImpulse);

            hit.rigidbody.AddForceAtPosition(pushDirection * impulse, hit.point, ForceMode.Impulse);
        }
    }
}
