using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopCatalog
    {
        IReadOnlyList<ShopProductData> Products { get; }

        bool TryGetByOfferId(string offerId, out ShopProductData product);
        bool TryGetByItemData(UtilityItemDataSO itemData, out ShopProductData product);
        bool TryGetByEconomyItemId(int economyItemId, out ShopProductData product);
    }
}
