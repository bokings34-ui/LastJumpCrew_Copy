using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Shop;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class NetworkScenePortalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string destinationSceneName;
        [SerializeField] private ShopSceneTransitionMode shopTransitionMode;
        [SerializeField] private string interactionPrompt = "Travel To Exterior Shop";
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 4f;

        public string InteractionPrompt => interactionPrompt;
        public string DestinationSceneName => destinationSceneName;

        public bool MatchesServerRequest(
            Transform playerTransform,
            string requestedSceneName,
            ShopSceneTransitionMode requestedTransitionMode)
        {
            return playerTransform != null
                && destinationSceneName == requestedSceneName
                && ResolveTransitionMode() == requestedTransitionMode
                && Vector3.Distance(playerTransform.position, transform.position) <= serverInteractionDistance;
        }

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
            player.RequestGameplaySceneTransition(destinationSceneName, ResolveTransitionMode());
        }

        private ShopSceneTransitionMode ResolveTransitionMode()
        {
            var mapRuntime = FindAnyObjectByType<PHSMapRuntimeContext>(FindObjectsInactive.Include);
            return mapRuntime != null && mapRuntime.KeepShopPortalAlwaysActive
                ? ShopSceneTransitionMode.None
                : shopTransitionMode;
        }
    }
}
