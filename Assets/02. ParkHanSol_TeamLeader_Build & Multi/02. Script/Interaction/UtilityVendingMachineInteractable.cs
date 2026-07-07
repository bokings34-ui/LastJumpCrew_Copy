using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class UtilityVendingMachineInteractable : MonoBehaviour, IInteractable, LastJumpCrew.Common.IInteractable
    {
        [SerializeField] private UtilityVendingMachineData vendingMachineData;
        [SerializeField] private string interactionPrompt = "F";

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

        bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetCommonItem(out var item))
            {
                return false;
            }

            return itemHolder.CanHold(item);
        }

        void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetCommonItem(out var item))
            {
                return;
            }

            if (!itemHolder.CanHold(item))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={item.ItemId}");
                return;
            }

            itemHolder.Hold(item);
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

        private bool TryGetCommonItem(out LastJumpCrew.Common.IHoldableItem item)
        {
            item = null;

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            if (itemPrefabData.HeldPrefab == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_PREFAB_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            item = itemPrefabData.HeldPrefab.GetComponent<LastJumpCrew.Common.IHoldableItem>();
            if (item == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_CONTRACT_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }
    }
}
