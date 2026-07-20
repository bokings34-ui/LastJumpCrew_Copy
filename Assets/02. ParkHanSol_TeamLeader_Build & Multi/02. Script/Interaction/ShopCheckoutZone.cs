using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Calculates physical shop items from ShopProductData and queues paid items for ship delivery.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopCheckoutZone : NetworkBehaviour
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
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private MonoBehaviour purchaseServiceSource;
        [SerializeField, Min(0.1f)] private float statusDuration = 2f;
        [SerializeField] private GameObject teleportEffectPrefab;
        [SerializeField] private Transform teleportEffectAnchor;
        [SerializeField, Min(0.01f)] private float teleportEffectScale = 0.1f;
        [SerializeField, Min(0.1f)] private float teleportEffectDuration = 3.2f;

        private readonly HashSet<UtilityItemObject> checkoutItems = new();
        private IShopPurchaseService purchaseService;
        private bool checkoutPending;
        private int lastDisplayedPrice = -1;
        private int lastDisplayedCredits = -1;
        private string temporaryStatus;
        private float temporaryStatusUntil;

        public int CurrentTotalPrice => CalculateTotalPrice();

        private void Awake()
        {
            purchaseService = purchaseServiceSource as IShopPurchaseService;
            ValidateSetup();
            RefreshPriceText(true);
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
            if (checkoutPending || !IsTriggerConfigured())
            {
                return false;
            }

            RefreshCheckoutItemsFromZone();
            return BuildCheckoutSnapshot(null, out var totalPrice, false) &&
                totalPrice > 0 &&
                purchaseService != null &&
                totalPrice <= purchaseService.AvailableCredits;
        }

        public bool TryCheckout()
        {
            if (checkoutPending)
            {
                ShowTemporaryStatus("PURCHASE PENDING");
                return false;
            }

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

            var availableCredits = purchaseService.AvailableCredits;
            if (totalPrice > availableCredits)
            {
                ShowTemporaryStatus($"NOT ENOUGH CR\nNEED {totalPrice} / HAVE {availableCredits}");
                return false;
            }

            var requests = new List<ShopPurchaseRequest>(entries.Count);
            foreach (var entry in entries)
            {
                requests.Add(new ShopPurchaseRequest(entry.ItemObject.GetEntityId().ToString(), entry.ProductData));
            }

            checkoutPending = true;
            ShowTemporaryStatus("PURCHASE PENDING");
            if (!purchaseService.RequestPurchase(
                    requests,
                    result => HandlePurchaseCompleted(entries, totalPrice, result)))
            {
                checkoutPending = false;
                return false;
            }

            return true;
        }

        private void HandlePurchaseCompleted(
            IReadOnlyList<CheckoutEntry> entries,
            int requestedTotalPrice,
            ShopPurchaseResult result)
        {
            checkoutPending = false;
            if (!result.Success)
            {
                var status = result.Reason switch
                {
                    "insufficient_credits" => $"NEED {requestedTotalPrice} CR",
                    "out_of_stock" => "ITEM SOLD OUT",
                    _ => "PURCHASE FAILED"
                };
                ShowTemporaryStatus(status);
                return;
            }

            foreach (var entry in entries)
            {
                checkoutItems.Remove(entry.ItemObject);
            }

            CompletePurchasedItems(entries);

            Debug.Log($"PHS_SHOP_CHECKOUT_COMPLETED zone={name} totalPrice={result.TotalPrice} itemCount={result.PurchasedCount}");
            ShowTemporaryStatus($"PAID {result.TotalPrice} CR\nSHIP DELIVERY");
        }

        private void CompletePurchasedItems(IReadOnlyList<CheckoutEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                PlayCheckoutTeleportEffect();
                foreach (var entry in entries)
                {
                    if (entry.ItemObject == null)
                    {
                        continue;
                    }

                    Destroy(entry.ItemObject.gameObject);
                }

                return;
            }

            var itemReferences = new NetworkObjectReference[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var itemObject = entries[index].ItemObject;
                if (itemObject == null ||
                    !itemObject.TryGetComponent<NetworkObject>(out var itemNetworkObject) ||
                    !itemNetworkObject.IsSpawned)
                {
                    Debug.LogError(
                        $"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=item_network_object_missing zone={name} item={itemObject?.name}",
                        this);
                    return;
                }

                itemReferences[index] = new NetworkObjectReference(itemNetworkObject);
            }

            if (!IsSpawned)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=checkout_zone_not_spawned zone={name}", this);
                return;
            }

            if (IsServer)
            {
                CompletePurchasedItemsServer(itemReferences);
                return;
            }

            CompletePurchasedItemsServerRpc(itemReferences);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CompletePurchasedItemsServerRpc(NetworkObjectReference[] itemReferences)
        {
            CompletePurchasedItemsServer(itemReferences);
        }

        private void CompletePurchasedItemsServer(NetworkObjectReference[] itemReferences)
        {
            if (itemReferences == null || itemReferences.Length == 0)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=item_references_missing zone={name}", this);
                return;
            }

            for (var index = 0; index < itemReferences.Length; index++)
            {
                if (!itemReferences[index].TryGet(out var itemNetworkObject) ||
                    itemNetworkObject == null ||
                    !itemNetworkObject.IsSpawned ||
                    itemNetworkObject.GetComponent<UtilityItemObject>() == null)
                {
                    Debug.LogError($"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=item_reference_invalid zone={name} index={index}", this);
                    return;
                }

            }

            PlayCheckoutTeleportEffectClientRpc();
            foreach (var itemReference in itemReferences)
            {
                if (itemReference.TryGet(out var itemNetworkObject) && itemNetworkObject != null && itemNetworkObject.IsSpawned)
                {
                    itemNetworkObject.Despawn(true);
                }
            }
        }

        [ClientRpc]
        private void PlayCheckoutTeleportEffectClientRpc()
        {
            PlayCheckoutTeleportEffect();
        }

        private void PlayCheckoutTeleportEffect()
        {
            if (teleportEffectAnchor == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_EFFECT_FAILED reason=teleport_effect_anchor_missing zone={name}", this);
                return;
            }

            PlayTeleportEffect(teleportEffectAnchor.position);
        }

        private void PlayTeleportEffect(Vector3 position)
        {
            if (teleportEffectPrefab == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_EFFECT_FAILED reason=teleport_effect_missing zone={name}", this);
                return;
            }

            var effectInstance = Instantiate(teleportEffectPrefab, position, Quaternion.identity);
            effectInstance.transform.localScale = Vector3.one * teleportEffectScale;
            foreach (var particleSystem in effectInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particleSystem.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                particleSystem.Play(true);
            }

            Destroy(effectInstance, teleportEffectDuration);
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

            if (catalog == null || !catalog.TryGetByItemData(itemPrefabData, out productData))
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
            var availableCredits = purchaseService?.AvailableCredits ?? -1;
            if (!force &&
                totalPrice == lastDisplayedPrice &&
                availableCredits == lastDisplayedCredits)
            {
                return;
            }

            lastDisplayedPrice = totalPrice;
            lastDisplayedCredits = availableCredits;
            priceText.text = totalPrice > 0 &&
                availableCredits >= 0 &&
                totalPrice > availableCredits
                    ? $"{pricePrefix} ${totalPrice}\nHAVE ${availableCredits} - NOT ENOUGH"
                    : $"{pricePrefix} ${totalPrice}";
        }

        private void ShowTemporaryStatus(string message)
        {
            temporaryStatus = message;
            temporaryStatusUntil = Time.unscaledTime + statusDuration;
            lastDisplayedPrice = -1;
            lastDisplayedCredits = -1;
            RefreshPriceText(true);
        }

        private bool ValidateSetup()
        {
            var isValid = IsTriggerConfigured();
            if (!isValid)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=checkout_trigger_invalid zone={name}");
            }

            if (catalog == null || catalog.Products.Count == 0)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=catalog_missing zone={name}");
                isValid = false;
            }

            if (purchaseService == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=purchase_service_missing zone={name}");
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
