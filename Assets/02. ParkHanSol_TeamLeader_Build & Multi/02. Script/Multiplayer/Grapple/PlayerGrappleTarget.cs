using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PlayerGrappleTarget : MonoBehaviour, IGrappleTarget
    {
        [SerializeField] private Transform grapplePoint;

        private NetworkPlayerController playerController;

        public Transform GrapplePoint => grapplePoint;
        public bool CanMoveByGrapple => true;
        public float GrappleMass => playerController == null ? 0f : playerController.SpaceMass;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            if (grapplePoint == null)
            {
                Debug.LogError($"PHS_PLAYER_GRAPPLE_TARGET_SETUP_FAILED reason=grapple_point_missing player={name}");
            }
        }

        public void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime)
        {
            if (playerController == null)
            {
                Debug.LogError($"PHS_PLAYER_GRAPPLE_TARGET_FAILED reason=controller_missing player={name}");
                return;
            }

            playerController.ApplyGrapplePull(
                targetPosition,
                pullAcceleration,
                maximumPullSpeed,
                stopDistance,
                deltaTime);
        }
    }
}
