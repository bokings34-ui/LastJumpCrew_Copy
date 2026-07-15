using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Calculates physical shop items from ShopProductData and queues paid items for ship delivery.</summary>
    public sealed class ShopCheckoutZone : MonoBehaviour
    {
        private readonly struct CheckoutEntry
        {
            public CheckoutEntry(UtilityItemObject itemObject, ShopProductData productData)
            {
                ItemObject = itemObject;
                ProductData = productData;
            }

            public UtilityItemObject ItemObject { get; }
            public ShopProductData ProductData { get; }
        }

        [SerializeField] private BoxCollider checkoutTrigger;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private string pricePrefix = "TOTAL";
        [SerializeField] private ShopProductData[] products;
        [SerializeField] private SessionPartyCreditsWallet wallet;
        [SerializeField, Min(0.1f)] private float statusDuration = 2f;

        private readonly HashSet<UtilityItemObject> checkoutItems = new();
        private readonly Dictionary<UtilityItemPrefabData, ShopProductData> productsByItem = new();
        private int lastDisplayedPrice = -1;
        private string temporaryStatus;
        private float temporaryStatusUntil;

        public int CurrentTotalPrice => CalculateTotalPrice();

        private void Awake()
        {
            RebuildProductLookup(true);
            ValidateSetup();
            RefreshPriceText(true);
        }

        private void OnValidate()
        {
            RebuildProductLookup(false);
        }

        private void Update()
        {
            RefreshCheckoutItemsFromZone();
            RefreshPriceText(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject != null)
            {
                checkoutItems.Add(itemObject);
                RefreshPriceText(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject != null)
            {
                checkoutItems.Remove(itemObject);
                RefreshPriceText(true);
            }
        }

        public bool CanCheckout()
        {
            if (!IsTriggerConfigured())
            {
                return false;
            }

            RefreshCheckoutItemsFromZone();
            return BuildCheckoutSnapshot(null, out var totalPrice, false) && totalPrice > 0;
        }

        public bool TryCheckout()
        {
            if (!ValidateSetup())
            {
                ShowTemporaryStatus("CHECKOUT ERROR");
                return false;
            }

            RefreshCheckoutItemsFromZone();
            var entries = new List<CheckoutEntry>();
            if (!BuildCheckoutSnapshot(entries, out var totalPrice, true) || totalPrice <= 0)
            {
                ShowTemporaryStatus("NO SHOP ITEMS");
                return false;
            }

            wallet ??= SessionPartyCreditsWallet.Instance;
            if (wallet == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_FAILED reason=wallet_missing zone={name}");
                ShowTemporaryStatus("WALLET OFFLINE");
                return false;
            }

            var deliveryService = SessionPurchaseDeliveryService.Instance;
            if (deliveryService == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_FAILED reason=delivery_service_missing zone={name}");
                ShowTemporaryStatus("DELIVERY OFFLINE");
                return false;
            }

            if (!wallet.TrySpendCredits(totalPrice))
            {
                ShowTemporaryStatus($"NEED {totalPrice} CR");
                return false;
            }

            foreach (var entry in entries)
            {
                deliveryService.QueueDelivery(entry.ProductData.ItemPrefabData);
                checkoutItems.Remove(entry.ItemObject);
                Destroy(entry.ItemObject.gameObject);
            }

            Debug.Log($"PHS_SHOP_CHECKOUT_COMPLETED zone={name} totalPrice={totalPrice} itemCount={entries.Count} pendingDelivery={deliveryService.PendingCount}");
            ShowTemporaryStatus($"PAID {totalPrice} CR\nSHIP DELIVERY");
            return true;
        }

        private bool BuildCheckoutSnapshot(List<CheckoutEntry> entries, out int totalPrice, bool shouldLog)
        {
            totalPrice = 0;
            RemoveMissingItems();

            foreach (var itemObject in checkoutItems)
            {
                if (!TryResolveProduct(itemObject, out var productData, shouldLog))
                {
                    continue;
                }

                entries?.Add(new CheckoutEntry(itemObject, productData));
                totalPrice += productData.PurchasePrice;
            }

            return totalPrice > 0;
        }

        private bool TryResolveProduct(UtilityItemObject itemObject, out ShopProductData productData, bool shouldLog)
        {
            productData = null;
            if (itemObject == null || itemObject.IsHeld)
            {
                return false;
            }

            var itemPrefabData = itemObject.ItemPrefabData;
            if (itemPrefabData == null)
            {
                if (shouldLog)
                {
                    Debug.LogError($"PHS_SHOP_CHECKOUT_ITEM_FAILED reason=item_data_missing zone={name} item={itemObject.name}");
                }

                return false;
            }

            if (!productsByItem.TryGetValue(itemPrefabData, out productData))
            {
                if (shouldLog)
                {
                    Debug.LogWarning($"PHS_SHOP_CHECKOUT_ITEM_IGNORED reason=product_missing zone={name} item={itemPrefabData.ItemId}");
                }

                return false;
            }

            return productData.IsConfigured;
        }

        private int CalculateTotalPrice()
        {
            BuildCheckoutSnapshot(null, out var totalPrice, false);
            return totalPrice;
        }

        private void RebuildProductLookup(bool shouldLog)
        {
            productsByItem.Clear();
            if (products == null)
            {
                return;
            }

            foreach (var productData in products)
            {
                if (productData == null || !productData.IsConfigured)
                {
                    if (shouldLog)
                    {
                        Debug.LogError($"PHS_SHOP_PRODUCT_SETUP_FAILED reason=product_invalid zone={name}", this);
                    }

                    continue;
                }

                if (!productsByItem.TryAdd(productData.ItemPrefabData, productData) && shouldLog)
                {
                    Debug.LogError($"PHS_SHOP_PRODUCT_SETUP_FAILED reason=item_duplicate zone={name} item={productData.ItemPrefabData.ItemId}", productData);
                }
            }
        }

        private void RefreshPriceText(bool force)
        {
            if (priceText == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(temporaryStatus) && Time.unscaledTime < temporaryStatusUntil)
            {
                priceText.text = temporaryStatus;
                return;
            }

            temporaryStatus = string.Empty;
            var totalPrice = CalculateTotalPrice();
            if (!force && totalPrice == lastDisplayedPrice)
            {
                return;
            }

            lastDisplayedPrice = totalPrice;
            priceText.text = $"{pricePrefix} ${totalPrice}";
        }

        private void ShowTemporaryStatus(string message)
        {
            temporaryStatus = message;
            temporaryStatusUntil = Time.unscaledTime + statusDuration;
            lastDisplayedPrice = -1;
            RefreshPriceText(true);
        }

        private bool ValidateSetup()
        {
            var isValid = IsTriggerConfigured();
            if (!isValid)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=checkout_trigger_invalid zone={name}");
            }

            if (productsByItem.Count == 0)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=products_missing zone={name}");
                isValid = false;
            }

            return isValid;
        }

        private bool IsTriggerConfigured()
        {
            return checkoutTrigger != null && checkoutTrigger.isTrigger;
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
                var itemObject = itemCollider.GetComponentInParent<UtilityItemObject>();
                if (itemObject != null)
                {
                    checkoutItems.Add(itemObject);
                }
            }
        }
    }
}
