using LastJumpCrew.Common;
using System.Collections.Generic;
using UnityEngine;
using InterAct = LastJumpCrew.ParkHanSol.Interaction;
namespace LastJumpCrew.SeoBoGyeong.Economy
{
    public class CheckoutButton : MonoBehaviour ,IInteractable, InterAct.IInteractable
    {
        [SerializeField] private CheckoutDetector detector;
        [SerializeField] private string prompt = "Check Out";

        private List<ShopItemTag> basket => detector.basket;

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

        // 일괄 결제 결과 통지. 전부-아니면-전무이므로 success == true 일 때만
        // 구매된 id 들을 바구니에서 제거하고 진열품(GameObject)을 파괴한다.
        // (거부 사유는 세션이 로그로 출력. 파괴된 상품은 다음 스캔에서도 자동 제외됨.)
        private void OnPurchaseResolved(List<int> itemIds, bool success)
        {
            if (!success || itemIds == null) return;

            foreach (int id in itemIds)
            {
                // 같은 id 진열품이 여러 개일 수 있으니, 하나 찾으면 제거하고 다음 id 로 넘어간다.
                for (int i = basket.Count - 1; i >= 0; i--)
                {
                    var tag = basket[i];
                    if (tag == null || tag.ItemId != id) continue;

                    // TODO: 구매 성공 아이템의 IHoldableItem 지급/획득 처리 연결
                    basket.RemoveAt(i);
                    Destroy(tag.gameObject);
                    break;
                }
            }
        }

        #region Common IInteractable
        public string InteractionPrompt => prompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            bool value = detector.CheckBasket();
            Debug.Log($"[ItemCheckout/C] 인터렉션 체크 : {value}");
            return value;
        }

        public void Interact(IItemHolder itemHolder)
        {
            Debug.Log("[ItemCheckout/C] 인터렉션 실행");
            if (!CanInteract(itemHolder)) return;
            RequestPurchaseBasket();
        }
        #endregion

        #region 01.Interact IInteractable
        public bool CanInteract(InterAct.IItemHolder itemHolder)
        {
            bool value = detector.CheckBasket();
            //Debug.Log($"[ItemCheckout/P] 인터렉션 체크 : {value}");
            return value;
        }

        public void Interact(InterAct.IItemHolder itemHolder)
        {
            Debug.Log("[ItemCheckout/P] 인터렉션 실행");
            if (!CanInteract(itemHolder)) return;
            RequestPurchaseBasket();
        }
        #endregion

        // 바구니(구역 박스 안에서 인지된 상품) 전체의 id 를 모아 한 번에 결제를 요청한다.
        // 낱개로 여러 번 부르던 것을 1회 호출로 바꿔, 잔액 확인·차감·삭제가 모두 한 번에 일어난다.
        // 결과는 OnPurchaseResolved 로 돌아온다(로컬은 동기 실행).
        private void RequestPurchaseBasket()
        {
            var snapshot = detector.GetBasket();          // 순회 중 basket 이 변할 수 있어 스냅샷 사용
            var ids = new List<int>(snapshot.Length);
            foreach (var tag in snapshot)
            {
                if (tag == null) continue;                // 파괴된 진열품 방어
                ids.Add(tag.ItemId);
            }
            if (ids.Count == 0) return;

            commands.RequestPurchase(ids);
        }
    }
}
