using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkTutorialCheckpointObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private bool requireZeroGravity;

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
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
                        == NetworkPlayerGravityMode.ShipGravity)
            {
                return;
            }

            CompleteObjective();
        }
    }
}
