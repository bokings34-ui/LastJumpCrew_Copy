using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkTutorialCheckpointObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private bool requireZeroGravity;
        [SerializeField] private bool requireJump;

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            trigger.isTrigger = true;

            if (playerController == null
                || requireJump && requireZeroGravity)
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_CHECKPOINT_DISABLED " +
                    $"objective={ObjectiveId} reason=" +
                    (playerController == null
                        ? "player_controller_missing"
                        : "jump_zero_gravity_conflict"),
                    this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanComplete)
            {
                return;
            }

            var player = other.GetComponentInParent<NetworkPlayerController>();
            if (player == null
                || player != playerController
                || requireZeroGravity
                    && player.GravityMode
                        == NetworkPlayerGravityMode.ShipGravity
                || requireJump
                    && (player.GravityMode
                            != NetworkPlayerGravityMode.ShipGravity
                        || player.IsGrounded
                        || player.VerticalVelocity <= 0f))
            {
                return;
            }

            CompleteObjective();
        }
    }
}
