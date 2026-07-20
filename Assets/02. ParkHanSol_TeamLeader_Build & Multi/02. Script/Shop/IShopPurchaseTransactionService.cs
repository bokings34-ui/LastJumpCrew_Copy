using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public readonly struct ShopPurchaseDeliveryRequest
    {
        public ShopPurchaseDeliveryRequest(
            string purchaseId,
            UtilityItemPrefabData itemPrefabData)
        {
            PurchaseId = purchaseId;
            ItemPrefabData = itemPrefabData;
        }

        public string PurchaseId { get; }
        public UtilityItemPrefabData ItemPrefabData { get; }
    }

    /// <summary>
    /// Commits one server-authoritative payment and its delivery entries as one transaction.
    /// </summary>
    public interface IShopPurchaseTransactionService
    {
        bool TryCommitPurchase(
            string transactionId,
            int totalPrice,
            IReadOnlyList<ShopPurchaseDeliveryRequest> deliveries,
            ulong purchaserClientId,
            out string reason);
    }
}
