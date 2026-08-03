using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum ShopStockPolicy
    {
        OnePerVisit,
        Unlimited
    }

    [CreateAssetMenu(
        fileName = "PHS_ShopProductData",
        menuName = "LastJumpCrew/ParkHanSol/Shop Product Data")]
    public sealed class ShopProductData : ScriptableObject
    {
        [Header("상품 연결")]
        [Tooltip("상점 안에서 사용하는 고유 Offer ID")]
        [SerializeField] private string offerId;
        [Tooltip("실제 Held/Drop 프리팹과 아이콘을 가진 아이템 데이터")]
        [SerializeField] private UtilityItemDataSO itemPrefabData;
        [Header("상점 가격")]
        [Tooltip("실제 결제에서 사용하는 유일한 구매 가격")]
        [SerializeField, Min(1)] private int purchasePrice = 1;
        [Header("경제 어댑터")]
        [Tooltip("팀 경제 ItemData와 연결할 때 쓰는 ID. 가격 조회에는 사용하지 않음")]
        [SerializeField, Min(0)] private int economyItemId;
        [Header("물리 진열")]
        [SerializeField] private bool isDisplayed = true;
        [SerializeField] private int displayOrder;
        [SerializeField] private ShopStockPolicy stockPolicy = ShopStockPolicy.OnePerVisit;
        [SerializeField, TextArea] private string shopDescription;

        public string OfferId => offerId;
        public UtilityItemDataSO ItemPrefabData => itemPrefabData;
        public int PurchasePrice => purchasePrice;
        public int EconomyItemId => economyItemId;
        public bool IsDisplayed => isDisplayed;
        public int DisplayOrder => displayOrder;
        public ShopStockPolicy StockPolicy => stockPolicy;
        public string ShopDescription => shopDescription;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(offerId) && itemPrefabData != null && purchasePrice > 0;
    }
}
