using LastJumpCrew.Common;
using LastJumpCrew.SeBoGyeong.Economy;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    // 계산대 위 감지 구역에 들어온 아이템 가격을 합산하는 컴포넌트
    public class ItemCheckout : MonoBehaviour, IInteractable
    {
        
        private List<int> _purchaseItem = new List<int>();
        private HashSet<GameObject> _shoppingList = new HashSet<GameObject>();
        private int _totalPrice;
        
        //임시
        private CreditWallet creditWallet = new CreditWallet(500);
        private List<string> _buyItem = new List<string>();

        // interface ---
        public string InteractionPrompt => "Check Out";

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null) return false;
            if(_purchaseItem.Count<=0) return false;

            return true;
        }

        
        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder)) return;
            // 구매 과정

            //TODO :금액 차감
            if (!creditWallet.SpendCredits(_totalPrice))
            {
                Debug.Log("[상점] 구매불가. 원인 : 잔액 부족");
                return;
            }
            //TODO :트리거 안의 오브젝트 삭제 & 연출
            PurchaseItem();

            //TODO :이펙트

        }

        // ---
        [ContextMenu("Purchase Item")]
        private void TestBuy()
        { 
            //임시
            _totalPrice = 100;
            if (!creditWallet.SpendCredits(_totalPrice))
            {
                Debug.Log("[상점] 구매불가. 원인 : 잔액 부족");
                return;
            }
            PurchaseItem();
        }

        //TODO : 상점 내의 판매 아이템을 인식
        //TODO : 트리거에서 판별할 정보의 종류


        private void OnTriggerEnter(Collider other)
        {
            //제외 목록 : 손에 들린거. 원래 플레이어가 가지고 있던 아이템

            Debug.Log($"진입 : {other.gameObject.name}");
            _shoppingList.Add(other.gameObject);
            //_purchaseItem.Add(other.Id);
        }

        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"퇴장 : {other.gameObject.name}");
            _shoppingList.Remove(other.gameObject);
        }

        
        public void PurchaseItem()
        {
            foreach (var item in _shoppingList)
            {
                // TODO: 아이템 id 넘기기
                //임시로 gameObject.name  / SO연결하면 _purchaseItem.Add 로 수정
                string tmp = item.gameObject.name;
                _buyItem.Add(tmp);
                Destroy(item);
            }
            _shoppingList.Clear();

        }

        [ContextMenu("List text")]
        private void DebugText()
        {
            Debug.Log($"[Credit] 잔액 : {creditWallet.Credits}");
            Debug.Log($"장바구니 : {_shoppingList.Count}개 / {string.Join(',', _shoppingList)}");

            if (_purchaseItem.Count > 0)
            {
                Debug.Log($"구매한 상품 id : {string.Join(',', _purchaseItem)}");
            }

            if (_buyItem.Count > 0)
            {
                Debug.Log($"구매한 상품 : {string.Join(',', _buyItem)}");
            }
        }
    }
}
