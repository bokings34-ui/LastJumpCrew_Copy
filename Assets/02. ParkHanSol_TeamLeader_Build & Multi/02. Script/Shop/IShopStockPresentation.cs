using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopStockPresentation
    {
        ShopProductData CurrentProduct { get; }
        Transform PresentationAnchor { get; }
        bool IsInStock { get; }
    }
}
