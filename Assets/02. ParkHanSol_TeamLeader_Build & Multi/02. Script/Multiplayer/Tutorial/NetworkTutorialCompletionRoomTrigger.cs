using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkTutorialCompletionRoomTrigger : MonoBehaviour
    {
        [SerializeField] private NetworkTutorialDirector tutorialDirector;
        [SerializeField] private NetworkPlayerController playerController;

        private void Awake()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_COMPLETE_TRIGGER_FAILED " +
                    $"reason={reason} trigger={name}",
                    this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var enteringPlayer = other.GetComponentInParent<
                NetworkPlayerController>();
            if (enteringPlayer != playerController)
            {
                return;
            }

            tutorialDirector.ReportCompleteRoomEntered();
        }

        public bool TryValidate(out string reason)
        {
            if (tutorialDirector == null)
            {
                reason = "director_missing";
                return false;
            }

            if (playerController == null)
            {
                reason = "player_controller_missing";
                return false;
            }

            var trigger = GetComponent<Collider>();
            if (trigger == null || !trigger.isTrigger)
            {
                reason = "trigger_collider_invalid";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
