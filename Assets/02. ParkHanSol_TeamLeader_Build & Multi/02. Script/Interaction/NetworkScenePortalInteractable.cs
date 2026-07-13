using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class NetworkScenePortalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string destinationSceneName;
        [SerializeField] private string interactionPrompt = "Travel To Exterior Shop";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (string.IsNullOrWhiteSpace(destinationSceneName) || itemHolder is not Component holderComponent)
            {
                return false;
            }

            return holderComponent.GetComponent<NetworkPlayerController>() != null;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=setup_missing portal={name}");
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            player.RequestGameplaySceneTransition(destinationSceneName);
        }
    }
}
