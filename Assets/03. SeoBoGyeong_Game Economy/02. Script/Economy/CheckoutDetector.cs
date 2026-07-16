using LastJumpCrew.SeoBoGyeong.Data;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.SeoBoGyeong.item;
using LastJumpCrew.Common;
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
        private HashSet<ShopItemTag> shopItems = new();

        [SerializeField] private TradeType _type;
        [SerializeField] private TMP_Text textUI;
        [SerializeField] private UtilityConnect utilityConnect;   // 프리팹->int 경제 브릿지

        private int _totalPrice =0;
        private DataManager _data;
        private string prefix = "Total : $";
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

            // Buy 진열품을 인식해 장바구니에 담는다.
            // ShopItemTag 가 없으면 프리팹(UtilityItemObject)을 int 경제로 브릿지해 태그를 부착한다.
            ShopItemTag tag = ResolveTag(other);
            if (tag == null || shopItems.Contains(tag)) return;
            if (other.GetComponent<IItemHolder>().CurrentItem != null) return;

            Debug.Log($"[TradeStation] 아이템 {tag.gameObject}");
            shopItems.Add(tag);
            ChaingePrice(tag);
        }
        private void OnTriggerStay(Collider other)
        {
            if (_type == TradeType.None)
            {
                Debug.Log("[TradeStation] TradeType 미등록");
                return;
            }
            ShopItemTag tag = ResolveTag(other);
            if (tag == null || basket.Contains(tag)) return;
            if (other.GetComponent<IItemHolder>().CurrentItem != null) return;

            Debug.Log($"[TradeStation] 아이템 {tag.gameObject}");
            basket.Add(tag);
            ChaingePrice(tag);
        }
        

        private void OnTriggerExit(Collider other)
        {
            ShopItemTag tag = ResolveTag(other);
            if (tag == null) return;
            if (other.GetComponent<IItemHolder>().CurrentItem != null) return;

            basket.Remove(tag);
            ChaingePrice(tag, false);
        }

        // 진열품에서 ShopItemTag 를 얻는다. 없으면 UtilityItemObject(string ItemId)를
        // UtilityConnect 로 UtilityItemData(int)에 매핑한 뒤 ShopItemTag 를 부착한다.
        private ShopItemTag ResolveTag(Collider other)
        {
            ShopItemTag tag = other.GetComponentInParent<ShopItemTag>();
            if (tag != null) return tag;

            UtilityItemObject obj = other.GetComponentInParent<UtilityItemObject>();
            if (obj == null) return null;

            if (utilityConnect == null)
            {
                Debug.LogError("[TradeStation] UtilityConnect 미연결 - 프리팹 인식 불가");
                return null;
            }

            if (!utilityConnect.TryGetData(obj.ItemId, out UtilityItemData data)) return null;

            tag = obj.gameObject.AddComponent<ShopItemTag>();
            tag.Init(data.Id);
            return tag;
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

        public void RefreshTotalPrice(int totalprice = 0)
        {
            if (basket.Count <= 0) return;
            totalprice = 0;

            foreach (var item in basket)
            {
                ItemData items =  _data.Items.Get(item.ItemId);
                totalprice += items.Price;
            }
            _totalPrice = totalprice;
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
