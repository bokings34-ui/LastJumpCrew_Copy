using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;
using LocalInteraction = LastJumpCrew.ParkHanSol.Interaction;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionItemButton :
        MonoBehaviour,
        LocalInteraction.IInteractable,
        IInteractable
    {
        [SerializeField] private string itemId;
        [SerializeField] private string interactionPrompt = "Get test item";
        [SerializeField] private LocalInteraction.ShopCheckoutButtonPressVisual pressVisual;

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(LocalInteraction.IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        public void Interact(LocalInteraction.IItemHolder itemHolder)
        {
            GrantItem();
        }

        bool IInteractable.CanInteract(IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        void IInteractable.Interact(IItemHolder itemHolder)
        {
            GrantItem();
        }

        private bool CanInteractCore()
        {
            return TryGetHostItemLifecycle(out _, out var itemRecord)
                && string.IsNullOrEmpty(itemRecord.HeldItemId)
                && !string.IsNullOrWhiteSpace(itemId);
        }

        private void GrantItem()
        {
            var accepted = TryGetHostItemLifecycle(out var lifecycle, out _)
                && lifecycle.TryAssignHeldItemServer(itemId);
            pressVisual?.Play(accepted);
            Debug.Log(
                $"PHS_FEATURE_ITEM_GRANTED accepted={accepted} item={itemId}",
                this);
        }

        private static bool TryGetHostItemLifecycle(
            out NetworkPlayerItemLifecycle lifecycle,
            out NetworkPlayerItemRecord itemRecord)
        {
            lifecycle = null;
            itemRecord = null;
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer
                || !networkManager.ConnectedClients.TryGetValue(
                    networkManager.LocalClientId,
                    out var client)
                || client.PlayerObject == null)
            {
                return false;
            }

            lifecycle = client.PlayerObject.GetComponent<NetworkPlayerItemLifecycle>();
            itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            return lifecycle != null && itemRecord != null;
        }
    }
}
