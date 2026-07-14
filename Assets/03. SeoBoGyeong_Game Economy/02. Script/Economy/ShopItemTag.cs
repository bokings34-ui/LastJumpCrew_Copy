using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    /// <summary>
    /// 상점 진열 아이템에 판매용 itemId 를 실어주는 태그.
    /// RangeItemSpawner 가 스폰 직후 AddComponent 로 부착한다 — 프리팹 수정 없이 코드로만 연결.
    /// ItemCheckout 은 계산 구역에서 이 태그로 상품을 인식한다(gameObject.name 임시분 대체).
    /// </summary>
    public class ShopItemTag : MonoBehaviour
    {
        public int ItemId { get; private set; }

        public void Init(int itemId) => ItemId = itemId;
    }
}
