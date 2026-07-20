using LastJumpCrew.Common;
using System.Collections.Generic;
using UnityEngine;
using InterAct = LastJumpCrew.ParkHanSol.Interaction;
namespace LastJumpCrew.SeoBoGyeong.Economy
{
    public class CheckoutButton : MonoBehaviour ,IInteractable, InterAct.IInteractable
    {
        [SerializeField] private CheckoutDetector detector;

        private List<ShopItemTag> basket => detector.basket;
        private List<int> requestItem = new();

        private IGameCommands commands;
        private IGameStateProvider state;


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

        private void OnPurchaseResolved(int itemId, bool success)
        {
            if (!success) return; // 거부 사유는 세션이 로그로 출력

            for (int i = 0; i < basket.Count; i++)
            {
                var tag = basket[i];
                if (tag == null || tag.ItemId != itemId) continue;

                // TODO: 구매 성공 아이템의 IHoldableItem 지급/획득 처리 연결
                requestItem.Add(itemId);

                basket.RemoveAt(i);
                Destroy(tag.gameObject);

                return;
            }
        }

        #region Common IInteractable
        public string InteractionPrompt => "Check Out";

        public bool CanInteract(IItemHolder itemHolder)
        {
            bool value= detector.CheckBasket();
            Debug.Log($"[ItemCheckout] 인터렉션 체크 : {value}");
            return value;
        }

        public void Interact(IItemHolder itemHolder)
        {
            Debug.Log("[ItemCheckout] 인터렉션 실행");
            if (!CanInteract(itemHolder)) return;

            // 스냅샷 순회 ? 로컬에선 결과 이벤트가 동기로 돌아와 순회 중 basket 이 변한다
            var snapshot = detector.GetBasket();
            foreach (var tag in snapshot)
            {
                if (tag == null) continue; // 파괴된 진열품 방어
                commands.RequestPurchase(tag.ItemId);
            }
        }
        #endregion

        #region 01.Interact IInteractable
        public bool CanInteract(InterAct.IItemHolder itemHolder)
        {
           return detector.CheckBasket();
        }

        public void Interact(InterAct.IItemHolder itemHolder)
        {
            Debug.Log("[ItemCheckout] 인터렉션 실행");
            if (!CanInteract(itemHolder)) return;

            // 스냅샷 순회 ? 로컬에선 결과 이벤트가 동기로 돌아와 순회 중 basket 이 변한다
            var snapshot = detector.GetBasket();
            foreach (var tag in snapshot)
            {
                if (tag == null) continue; // 파괴된 진열품 방어
                commands.RequestPurchase(tag.ItemId);
            }
        }
        #endregion
    }
}

