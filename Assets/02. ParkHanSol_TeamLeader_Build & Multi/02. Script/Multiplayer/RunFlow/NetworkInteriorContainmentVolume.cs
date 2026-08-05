using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkInteriorContainmentVolume : MonoBehaviour
    {
        [SerializeField] private NetworkInteriorContainmentController controller;
        [SerializeField] private Collider containmentTrigger;

        public NetworkInteriorContainmentController Controller => controller;

        private void Awake()
        {
            ValidateSetup();
        }

        private void OnEnable()
        {
            if (ValidateSetup())
            {
                controller.RegisterVolume(this);
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.NotifyVolumeDisabled(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (TryGetPlayer(other, out var player))
            {
                controller.NotifyPlayerEntered(this, player);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (TryGetPlayer(other, out var player))
            {
                controller.NotifyPlayerExited(this, player);
            }
        }

        private bool TryGetPlayer(Collider other, out NetworkPlayerController player)
        {
            player = null;
            if (!ValidateSetup() || other == null || other.GetComponent<CharacterController>() == null)
            {
                return false;
            }

            player = other.GetComponent<NetworkPlayerController>();
            return player != null;
        }

        private bool ValidateSetup()
        {
            if (controller != null
                && containmentTrigger != null
                && containmentTrigger.gameObject == gameObject
                && containmentTrigger.isTrigger)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_INTERIOR_CONTAINMENT_VOLUME_SETUP_FAILED volume={name} " +
                $"controller={controller != null} trigger={containmentTrigger != null} " +
                $"same_object={containmentTrigger != null && containmentTrigger.gameObject == gameObject} " +
                $"is_trigger={containmentTrigger != null && containmentTrigger.isTrigger}",
                this);
            enabled = false;
            return false;
        }
    }
}
