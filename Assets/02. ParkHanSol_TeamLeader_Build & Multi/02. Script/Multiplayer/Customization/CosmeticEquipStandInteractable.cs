using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public sealed class CosmeticEquipStandInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private CosmeticItemData itemData;
        [SerializeField] private string interactionPrompt = "Equip Cosmetic";

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_STAND_SETUP_FAILED reason=item_data_missing stand={name}", this);
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
                Debug.LogError($"PHS_COSMETIC_EQUIP_STAND_FAILED reason=item_data_missing stand={name}", this);
                return;
            }

            if (!TryResolveCustomization(itemHolder, out var customization))
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_STAND_FAILED reason=customization_missing stand={name} item={itemData.ItemId}", this);
                return;
            }

            customization.RequestEquip(itemData.ItemId);
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
