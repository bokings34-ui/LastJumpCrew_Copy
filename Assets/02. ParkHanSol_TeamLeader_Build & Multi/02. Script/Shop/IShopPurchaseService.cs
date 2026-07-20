using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public readonly struct ShopPurchaseRequest
    {
        public ShopPurchaseRequest(string purchaseId, ShopProductData product)
        {
            PurchaseId = purchaseId;
            Product = product;
        }

        public string PurchaseId { get; }
        public ShopProductData Product { get; }
    }

    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseResult(bool success, string reason, int totalPrice, int purchasedCount)
        {
            Success = success;
            Reason = reason;
            TotalPrice = totalPrice;
            PurchasedCount = purchasedCount;
        }

        public bool Success { get; }
        public string Reason { get; }
        public int TotalPrice { get; }
        public int PurchasedCount { get; }
    }

    public interface IShopPurchaseService
    {
        event Action<ShopProductData> ProductPurchased;

        int AvailableCredits { get; }
        bool TryPurchase(IReadOnlyList<ShopPurchaseRequest> requests, out ShopPurchaseResult result);
        bool RequestPurchase(
            IReadOnlyList<ShopPurchaseRequest> requests,
            Action<ShopPurchaseResult> onCompleted);
    }
}
