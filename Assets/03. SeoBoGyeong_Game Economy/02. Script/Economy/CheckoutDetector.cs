using LastJumpCrew.SeoBoGyeong.Data;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace LastJumpCrew.SeoBoGyeong.Economy
{
    public enum TradeType
    {
        None,
        Sell,
        Buy,
    }

    [Serializable]
    public class CheckoutDetector : MonoBehaviour
    {
        public readonly List<ShopItemTag> basket = new();

        [SerializeField] private TradeType _type;
        [SerializeField] private TMP_Text textUI;

        private int _totalPrice =0;
        private DataManager _data;
        private string prefix = "Total : $ ";
        private void Awake()
        {
            if(GameCore.Instance != null) _data = GameCore.Instance.Data;
            RefreshTotalPrice();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_type == TradeType.None)
            {
                Debug.Log("[TradeStation] TradeType 미등록");
                return;
            }
            else if (_type == TradeType.Sell)
            {
                // 계산 구역 인식: ShopItemTag 가 붙은 진열품만 장바구니에 담는다
                var tag = other.GetComponentInParent<ShopItemTag>();
                if (tag == null || basket.Contains(tag)) return;
                Debug.Log($"[TradeStation] 아이템 {tag.gameObject}");
                basket.Add(tag);
                ChaingePrice(tag);
            }
            
        }

        private void OnTriggerExit(Collider other)
        {
            var tag = other.GetComponentInParent<ShopItemTag>();
            if (tag == null) return;

            basket.Remove(tag);
            ChaingePrice(tag,false);
        }

        public bool CheckBasket()
        {
            if (basket.Count > 0)
            {
                return true;
            }

            return false ;
        }

        public ShopItemTag[] GetBasket()
        {
            return basket.ToArray();
        }

        public void RefreshTotalPrice()
        {
            int price = 0;
            foreach (var item in basket)
            {
                ItemData items =  _data.Items.Get(item.ItemId);
               price += items.Price;
            }
            _totalPrice = price;
            textUI.text = prefix + _totalPrice.ToString();
        }

        private void ChaingePrice(ShopItemTag tag, bool isAdd = true)
        {
            int chaingePrice = _data.Items.Get(tag.ItemId).Price;
            if (isAdd)
            {
                chaingePrice = chaingePrice * 1;
            }
            else if (!isAdd)
            {
                chaingePrice = chaingePrice * -1;
            }
            _totalPrice += chaingePrice;
            textUI.text = prefix + _totalPrice.ToString();
        }
    }

}
