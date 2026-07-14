using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_ShopProductData",
        menuName = "LastJumpCrew/ParkHanSol/Shop Product Data")]
    public sealed class ShopProductData : ScriptableObject
    {
        [SerializeField] private string offerId;
        [SerializeField] private UtilityItemPrefabData itemPrefabData;
        [SerializeField, Min(0)] private int purchasePrice;

        public string OfferId => offerId;
        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
        public int PurchasePrice => purchasePrice;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(offerId) && itemPrefabData != null && purchasePrice > 0;
    }
}
