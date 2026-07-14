using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.SeoBoGyeong.Economy;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 상점 계산대. 계산 구역(트리거) 안의 진열 아이템(ShopItemTag)을 모아
    /// F 상호작용 시 GameCore.Commands.RequestPurchase 로 구매를 요청한다.
    /// 가격·잔액 검증과 Credit 차감은 세션(권위)이 수행 — 여기는 요청 + 결과 반영만(서버 권위 원칙).
    /// </summary>
    public class ItemCheckout : MonoBehaviour, IInteractable
    {
        private readonly List<ShopItemTag> basket = new();

        // 규율: Services resolve 는 시작 시 1회 캐싱(매 프레임 조회 금지)
        private IGameCommands commands;
        private IGameStateProvider state;

        // interface ---
        public string InteractionPrompt => "Check Out";

        private void Start()
        {
            commands = GameCore.Instance.Commands;
            state = GameCore.Instance.State;
            state.PurchaseResolved += OnPurchaseResolved;
        }

        private void OnDestroy()
        {
            if (state != null) state.PurchaseResolved -= OnPurchaseResolved;
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null) return false;
            if (basket.Count <= 0) return false;

            return true;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder)) return;

            // 스냅샷 순회 — 로컬에선 결과 이벤트가 동기로 돌아와 순회 중 basket 이 변한다
            var snapshot = basket.ToArray();
            foreach (var tag in snapshot)
            {
                if (tag == null) continue; // 파괴된 진열품 방어
                commands.RequestPurchase(tag.ItemId);
            }
        }

        /// <summary>구매 결과 수신. 성공한 itemId 와 일치하는 진열품 1개를 지급 처리(진열 제거).</summary>
        private void OnPurchaseResolved(int itemId, bool success)
        {
            if (!success) return; // 거부 사유는 세션이 로그로 출력

            for (int i = 0; i < basket.Count; i++)
            {
                var tag = basket[i];
                if (tag == null || tag.ItemId != itemId) continue;

                basket.RemoveAt(i);
                Destroy(tag.gameObject);
                // TODO(조한용 연동): 구매 성공 아이템의 IHoldableItem 지급/획득 처리 연결
                return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 계산 구역 인식: ShopItemTag 가 붙은 진열품만 장바구니에 담는다
            var tag = other.GetComponentInParent<ShopItemTag>();
            if (tag == null || basket.Contains(tag)) return;

            basket.Add(tag);
        }

        private void OnTriggerExit(Collider other)
        {
            var tag = other.GetComponentInParent<ShopItemTag>();
            if (tag == null) return;

            basket.Remove(tag);
        }
    }
}
