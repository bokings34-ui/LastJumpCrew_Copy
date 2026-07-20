using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public sealed class CosmeticPurchaseOfferInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private CosmeticItemData itemData;
        [SerializeField] private string interactionPrompt = "Purchase Cosmetic";

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_OFFER_SETUP_FAILED reason=item_data_missing offer={name}", this);
            }
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return itemData != null && TryResolveCustomization(itemHolder, out _);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_OFFER_FAILED reason=item_data_missing offer={name}", this);
                return;
            }

            if (!TryResolveCustomization(itemHolder, out var customization))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_OFFER_FAILED reason=customization_missing offer={name} item={itemData.ItemId}", this);
                return;
            }

            customization.RequestPurchase(itemData.ItemId);
        }

        private static bool TryResolveCustomization(
            IItemHolder itemHolder,
            out NetworkPlayerCustomization customization)
        {
            customization = null;
            return itemHolder is Component itemHolderComponent &&
                   itemHolderComponent.TryGetComponent(out customization);
        }
    }
}
