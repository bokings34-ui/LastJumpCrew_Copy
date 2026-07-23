using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    /// <summary>
    /// 상점 진열 아이템에 판매용 itemId 를 실어주는 태그.
    /// RangeItemSpawner 가 스폰 직후 AddComponent 로 부착한다 — 프리팹 수정 없이 코드로만 연결.
    /// 계산 구역(CheckoutDetector) 이 이 태그로 상품을 인식한다.
    ///
    /// 추가: 활성화(스폰) 시 자기 콜라이더를 정적 레지스트리에 등록해,
    ///       OverlapBox 스캔이 GetComponent 재조회 없이 Collider→ShopItemTag 로 O(1) 변환하게 한다.
    ///       (물리 레이어는 변경하지 않는다 — 픽업/상호작용 시스템 보존.)
    /// </summary>
    public class ShopItemTag : MonoBehaviour
    {
        // Collider → 태그 정적 레지스트리. 스캔이 히트 Collider 를 태그로 즉시 변환한다.
        private static readonly Dictionary<Collider, ShopItemTag> registry = new();

        public int ItemId { get; private set; }
        public int ItemPrice { get; private set; }

        // 등록해 둔 콜라이더 목록. 해제 시 정확히 되돌리기 위해 보관한다.
        private readonly List<Collider> registeredColliders = new();

        public void Init(int itemId, int price)
        {
            ItemId = itemId;
            ItemPrice = price;
        }

        // 에디터 Domain Reload 를 꺼도, 매 플레이 시작 시 레지스트리를 초기화한다(이전 실행의 유령 항목 방지).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => registry.Clear();

        /// <summary>히트 Collider 를 태그로 변환. 등록돼 있지 않으면 false.</summary>
        public static bool TryGet(Collider col, out ShopItemTag tag) => registry.TryGetValue(col, out tag);

        private void OnEnable()
        {
            // 자기 자신과 자식의 모든 콜라이더를 레지스트리에 등록한다.
            // 물리 레이어는 건드리지 않는다 — 플레이어 픽업/상호작용이 레이어에 의존하므로 보존.
            registeredColliders.Clear();
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                registry[col] = this;
                registeredColliders.Add(col);
            }
        }

        private void OnDisable()
        {
            // 내가 등록한 콜라이더만 정확히 해제(다른 태그가 덮어썼을 가능성 방어).
            foreach (var col in registeredColliders)
            {
                if (col != null && registry.TryGetValue(col, out var owner) && owner == this)
                    registry.Remove(col);
            }
            registeredColliders.Clear();
        }
    }
}
