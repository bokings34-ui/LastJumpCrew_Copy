using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class ExteriorTestTeleportInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform destination;
        [SerializeField] private string interactionPrompt = "Move To Exterior Test Zone";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return destination != null && itemHolder is Component;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=setup_missing portal={name}");
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            if (player == null)
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=player_missing portal={name}");
                return;
            }

            player.RequestTestTeleport(destination.position, destination.rotation);
        }
    }
}
