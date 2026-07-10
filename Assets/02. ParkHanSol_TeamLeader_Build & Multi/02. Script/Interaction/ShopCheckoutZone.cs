using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 계산대 위 감지 구역에 들어온 아이템 가격을 합산하는 테스트용 계산대 컴포넌트다.
    // 실제 결제, 재화 차감, 함선 이동은 아직 연결하지 않고 로그와 가격 표시까지만 담당한다.
    public sealed class ShopCheckoutZone : MonoBehaviour, IInteractable
    {
        // checkoutTrigger는 계산대 본체가 아니라 아이템을 올려두는 감지용 Trigger다.
        // 플레이어 Raycast를 막지 않도록 NoPlayerInteract 레이어에 두고, Inspector에서 직접 연결한다.
        [SerializeField] private string interactionPrompt = "Checkout";
        [SerializeField] private BoxCollider checkoutTrigger;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private string pricePrefix = "TOTAL";

        private readonly HashSet<UtilityItemObject> checkoutItems = new();
        private int lastDisplayedPrice = -1;

        public string InteractionPrompt => interactionPrompt;
        public int CurrentTotalPrice => CalculateTotalPrice();

        private void Awake()
        {
            ValidateSetup();
            RefreshPriceText(true);
        }

        private void Update()
        {
            // Trigger 이벤트만 의존하면 이미 안에 있던 아이템이나 Rigidbody 누락 상황을 놓칠 수 있다.
            // 테스트 안정성을 위해 매 프레임 현재 박스 안 Collider를 다시 스캔한다.
            RefreshCheckoutItemsFromZone();
            RefreshPriceText(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_TRACK_FAILED reason=collider_missing zone={name}");
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null)
            {
                return;
            }

            checkoutItems.Add(itemObject);
            Debug.Log($"PHS_SHOP_CHECKOUT_ITEM_ENTER zone={name} item={itemObject.ItemId}");
            RefreshPriceText(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_UNTRACK_FAILED reason=collider_missing zone={name}");
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null)
            {
                return;
            }

            checkoutItems.Remove(itemObject);
            Debug.Log($"PHS_SHOP_CHECKOUT_ITEM_EXIT zone={name} item={itemObject.ItemId}");
            RefreshPriceText(true);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (!ValidateSetup())
            {
                return false;
            }

            if (CalculateTotalPrice() <= 0)
            {
                Debug.LogWarning($"PHS_SHOP_CHECKOUT_FAILED reason=no_priced_items zone={name}");
                return false;
            }

            return true;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            // 현재 단계에서는 구매 확정 후 아이템 삭제/이동을 하지 않는다.
            // 함선/재화 시스템이 붙으면 이 지점에서 후처리를 연결한다.
            var totalPrice = CalculateTotalPrice();
            Debug.Log($"PHS_SHOP_CHECKOUT_CONFIRMED zone={name} totalPrice={totalPrice} itemCount={CountPricedItems()}");
        }

        private void RefreshPriceText(bool force)
        {
            if (priceText == null)
            {
                return;
            }

            var totalPrice = CalculateTotalPrice();
            if (!force && totalPrice == lastDisplayedPrice)
            {
                return;
            }

            lastDisplayedPrice = totalPrice;
            priceText.text = $"{pricePrefix} ${totalPrice}";
        }

        private bool ValidateSetup()
        {
            if (checkoutTrigger == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=checkoutTrigger_missing zone={name}");
                return false;
            }

            if (!checkoutTrigger.isTrigger)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=checkoutTrigger_not_trigger zone={name}");
                return false;
            }

            return true;
        }

        private int CalculateTotalPrice()
        {
            var totalPrice = 0;
            RemoveMissingItems();

            foreach (var itemObject in checkoutItems)
            {
                if (!TryGetPricedItem(itemObject, out var itemPrefabData))
                {
                    continue;
                }

                totalPrice += itemPrefabData.Price;
            }

            return totalPrice;
        }

        private int CountPricedItems()
        {
            var count = 0;
            RemoveMissingItems();

            foreach (var itemObject in checkoutItems)
            {
                if (TryGetPricedItem(itemObject, out _))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetPricedItem(UtilityItemObject itemObject, out UtilityItemPrefabData itemPrefabData)
        {
            itemPrefabData = null;

            if (itemObject == null)
            {
                return false;
            }

            if (itemObject.IsHeld)
            {
                // 손에 들린 아이템은 계산대 안에 겹쳐도 구매 대상에서 제외한다.
                return false;
            }

            itemPrefabData = itemObject.ItemPrefabData;
            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_ITEM_FAILED reason=itemData_missing zone={name} item={itemObject.name}");
                return false;
            }

            if (itemPrefabData.Price <= 0)
            {
                Debug.LogWarning($"PHS_SHOP_CHECKOUT_ITEM_FAILED reason=price_not_set zone={name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        private void RemoveMissingItems()
        {
            checkoutItems.RemoveWhere(itemObject => itemObject == null);
        }

        private void RefreshCheckoutItemsFromZone()
        {
            if (checkoutTrigger == null)
            {
                return;
            }

            checkoutItems.Clear();

            var center = checkoutTrigger.transform.TransformPoint(checkoutTrigger.center);
            var halfExtents = Vector3.Scale(checkoutTrigger.size, checkoutTrigger.transform.lossyScale) * 0.5f;
            var colliders = Physics.OverlapBox(
                center,
                halfExtents,
                checkoutTrigger.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (var itemCollider in colliders)
            {
                // 아이템 모델의 자식 Collider가 잡혀도 루트 UtilityItemObject 기준으로 계산한다.
                var itemObject = itemCollider.GetComponentInParent<UtilityItemObject>();
                if (itemObject != null)
                {
                    checkoutItems.Add(itemObject);
                }
            }
        }
    }
}
