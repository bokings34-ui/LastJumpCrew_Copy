using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class UtilityVendingMachineInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private UtilityVendingMachineData vendingMachineData;
        [SerializeField] private string interactionPrompt = "E";

        public string InteractionPrompt => interactionPrompt;
        public UtilityVendingMachineData VendingMachineData => vendingMachineData;

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            return itemHolder.CanReplaceHeldItem(itemPrefabData);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return;
            }

            if (!itemHolder.CanReplaceHeldItem(itemPrefabData))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={itemPrefabData.ItemId}");
                return;
            }

            itemHolder.ReplaceHeldItem(itemPrefabData, transform);
        }

        private bool TryGetItemPrefabData(out UtilityItemPrefabData itemPrefabData)
        {
            itemPrefabData = null;

            if (vendingMachineData == null)
            {
                Debug.LogWarning($"PHS_VENDING_DATA_MISSING target={name}");
                return false;
            }

            itemPrefabData = vendingMachineData.ItemPrefabData;
            if (itemPrefabData == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_DATA_MISSING target={name} vendingData={vendingMachineData.name}");
                return false;
            }

            return true;
        }
    }
}
