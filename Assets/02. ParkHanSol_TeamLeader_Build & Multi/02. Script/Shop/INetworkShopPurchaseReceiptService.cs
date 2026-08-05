using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface INetworkShopPurchaseReceiptService
    {
        bool TryCommitCheckoutPurchaseServer(
            ulong purchaserClientId,
            IReadOnlyList<string> purchaseIds,
            IReadOnlyList<ShopProductData> products,
            out ShopPurchaseResult result);
    }
}
