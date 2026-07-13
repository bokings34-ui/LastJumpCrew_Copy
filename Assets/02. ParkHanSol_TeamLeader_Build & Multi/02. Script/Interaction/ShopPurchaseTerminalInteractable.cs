using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Host-side confirmation terminal. The selected item is queued for the ship delivery box.</summary>
    public sealed class ShopPurchaseTerminalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SessionPartyCreditsWallet wallet;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string interactionPrompt = "Purchase";

        private UtilityItemPrefabData selectedItem;

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            if (wallet == null)
            {
                wallet = SessionPartyCreditsWallet.Instance;
            }

            RefreshStatus("SELECT AN ITEM");
        }

        public void Select(UtilityItemPrefabData itemPrefabData)
        {
            selectedItem = itemPrefabData;
            if (selectedItem == null)
            {
                RefreshStatus("SELECT AN ITEM");
                return;
            }

            RefreshStatus($"{selectedItem.ItemId}\n{selectedItem.Price} CR\n[F] BUY");
            Debug.Log($"PHS_SHOP_ITEM_SELECTED terminal={name} item={selectedItem.ItemId} price={selectedItem.Price}");
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return selectedItem != null && selectedItem.Price > 0 && (wallet != null || SessionPartyCreditsWallet.Instance != null) && SessionPurchaseDeliveryService.Instance != null;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (wallet == null)
            {
                wallet = SessionPartyCreditsWallet.Instance;
            }

            if (!CanInteract(itemHolder))
            {
                RefreshStatus("SELECT ITEM / HOST ONLY");
                return;
            }

            if (!wallet.TrySpendCredits(selectedItem.Price))
            {
                RefreshStatus($"NOT ENOUGH CR\nNEED {selectedItem.Price}");
                return;
            }

            SessionPurchaseDeliveryService.Instance.QueueDelivery(selectedItem);
            RefreshStatus($"PAID: {selectedItem.ItemId}\nSHIP DELIVERY READY");
            selectedItem = null;
        }

        private void RefreshStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
