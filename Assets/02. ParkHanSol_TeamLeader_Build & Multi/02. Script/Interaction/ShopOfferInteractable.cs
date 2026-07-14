using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Selects one catalog item; payment remains explicitly on the separate purchase button.</summary>
    public sealed class ShopOfferInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ShopPurchaseTerminalInteractable purchaseTerminal;
        [SerializeField] private UtilityItemPrefabData itemPrefabData;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private string interactionPrompt = "Select Item";

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            RefreshLabel();
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return purchaseTerminal != null && itemPrefabData != null && itemPrefabData.Price > 0;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError($"PHS_SHOP_OFFER_SELECT_FAILED reason=setup_missing offer={name}");
                return;
            }

            purchaseTerminal.Select(itemPrefabData);
        }

        private void RefreshLabel()
        {
            if (priceLabel != null && itemPrefabData != null)
            {
                priceLabel.text = $"{itemPrefabData.ItemId}\n{itemPrefabData.Price} CR\n[F] SELECT";
            }
        }
    }
}
