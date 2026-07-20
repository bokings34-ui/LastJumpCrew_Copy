using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Shop;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Host-side confirmation terminal. The selected item is queued for the ship delivery box.</summary>
    public sealed class ShopPurchaseTerminalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private MonoBehaviour purchaseServiceSource;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string interactionPrompt = "Purchase";

        private IShopPurchaseService purchaseService;
        private ShopProductData selectedProduct;
        private bool purchasePending;
        private uint nextPurchaseSequence;

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            purchaseService = purchaseServiceSource as IShopPurchaseService;
            RefreshStatus("SELECT AN ITEM");
        }

        public void Select(ShopProductData productData)
        {
            selectedProduct = productData;
            if (selectedProduct == null)
            {
                RefreshStatus("SELECT AN ITEM");
                return;
            }

            var availableCredits = purchaseService?.AvailableCredits ?? 0;
            RefreshStatus(selectedProduct.PurchasePrice > availableCredits
                ? $"{selectedProduct.ItemPrefabData.DisplayName}\n{selectedProduct.PurchasePrice} CR\nNOT ENOUGH - HAVE {availableCredits}"
                : $"{selectedProduct.ItemPrefabData.DisplayName}\n{selectedProduct.PurchasePrice} CR\n[F] BUY");
            Debug.Log($"PHS_SHOP_ITEM_SELECTED terminal={name} offer={selectedProduct.OfferId} price={selectedProduct.PurchasePrice}");
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !purchasePending &&
                selectedProduct != null &&
                selectedProduct.IsConfigured &&
                purchaseService != null &&
                selectedProduct.PurchasePrice <= purchaseService.AvailableCredits;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (purchaseService != null &&
                selectedProduct != null &&
                selectedProduct.PurchasePrice > purchaseService.AvailableCredits)
            {
                RefreshStatus(
                    $"NOT ENOUGH CR\nNEED {selectedProduct.PurchasePrice} / HAVE {purchaseService.AvailableCredits}");
                return;
            }

            if (!CanInteract(itemHolder))
            {
                RefreshStatus("PURCHASE OFFLINE");
                return;
            }

            var requestedProduct = selectedProduct;
            var purchaseId = $"terminal:{GetEntityId()}:{requestedProduct.OfferId}:{++nextPurchaseSequence}";
            var request = new ShopPurchaseRequest(purchaseId, requestedProduct);
            purchasePending = true;
            RefreshStatus("PURCHASE PENDING");
            if (!purchaseService.RequestPurchase(
                    new List<ShopPurchaseRequest> { request },
                    result => HandlePurchaseCompleted(requestedProduct, result)))
            {
                purchasePending = false;
                return;
            }
        }

        private void HandlePurchaseCompleted(ShopProductData requestedProduct, ShopPurchaseResult result)
        {
            purchasePending = false;
            if (!result.Success)
            {
                var status = result.Reason switch
                {
                    "insufficient_credits" => $"NOT ENOUGH CR\nNEED {requestedProduct.PurchasePrice}",
                    "out_of_stock" => "ITEM SOLD OUT",
                    _ => "PURCHASE FAILED"
                };
                RefreshStatus(status);
                return;
            }

            RefreshStatus($"PAID: {requestedProduct.ItemPrefabData.DisplayName}\nSHIP DELIVERY READY");
            if (selectedProduct == requestedProduct)
            {
                selectedProduct = null;
            }
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
