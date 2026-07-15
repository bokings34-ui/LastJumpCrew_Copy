using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopRandomDisplayController : NetworkBehaviour
    {
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private ShopDisplaySlot[] displaySlots;
        [SerializeField] private MonoBehaviour purchaseServiceSource;
        [SerializeField, Min(1)] private int minimumDisplayCount = 8;
        [SerializeField, Min(1)] private int maximumDisplayCount = 10;
        [SerializeField] private bool allowDuplicateProducts = true;

        private readonly NetworkList<FixedString64Bytes> displayedOfferIds = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private IShopPurchaseService purchaseService;
        private bool hasPopulatedThisVisit;

        private void Awake()
        {
            purchaseService = purchaseServiceSource as IShopPurchaseService;
            if (purchaseService != null)
            {
                purchaseService.ProductPurchased += HandleProductPurchased;
            }
        }

        private void Start()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                PopulateLocalDisplays();
            }
        }

        public override void OnNetworkSpawn()
        {
            displayedOfferIds.OnListChanged += HandleDisplayedOffersChanged;
            if (IsServer && displayedOfferIds.Count == 0)
            {
                PopulateNetworkDisplays();
            }

            RefreshDisplaysFromNetworkState();
        }

        public override void OnNetworkDespawn()
        {
            displayedOfferIds.OnListChanged -= HandleDisplayedOffersChanged;
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            if (purchaseService != null)
            {
                purchaseService.ProductPurchased -= HandleProductPurchased;
            }
        }

        public void PopulateDisplays()
        {
            if (hasPopulatedThisVisit)
            {
                Debug.Log($"PHS_SHOP_RESTOCK_BLOCKED reason=initial_stock_only controller={name}", this);
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                PopulateLocalDisplays();
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=server_required controller={name}", this);
                return;
            }

            PopulateNetworkDisplays();
        }

        private void PopulateNetworkDisplays()
        {
            if (hasPopulatedThisVisit)
            {
                return;
            }

            if (!ValidateSetup())
            {
                return;
            }

            var displayCount = GetRandomDisplayCount();
            var candidates = CreateRandomCandidates(displayCount);
            if (candidates.Count == 0)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=display_products_missing controller={name}", this);
                return;
            }

            displayedOfferIds.Clear();
            for (var slotIndex = 0; slotIndex < displaySlots.Length; slotIndex++)
            {
                var offerId = slotIndex < displayCount && slotIndex < candidates.Count
                    ? candidates[slotIndex].OfferId
                    : string.Empty;
                displayedOfferIds.Add(new FixedString64Bytes(offerId));
            }

            RefreshDisplaysFromNetworkState();
            hasPopulatedThisVisit = true;
        }

        private void PopulateLocalDisplays()
        {
            if (hasPopulatedThisVisit)
            {
                return;
            }

            if (!ValidateSetup())
            {
                return;
            }

            var displayCount = GetRandomDisplayCount();
            var candidates = CreateRandomCandidates(displayCount);
            if (candidates.Count == 0)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=display_products_missing controller={name}", this);
                return;
            }

            for (var slotIndex = 0; slotIndex < displaySlots.Length; slotIndex++)
            {
                var slot = displaySlots[slotIndex];
                if (slot == null)
                {
                    Debug.LogError(
                        $"PHS_SHOP_DISPLAY_FAILED reason=slot_missing controller={name} index={slotIndex}",
                        this);
                    continue;
                }

                if (slotIndex < displayCount && slotIndex < candidates.Count)
                {
                    slot.TryPresent(candidates[slotIndex]);
                }
                else
                {
                    slot.Clear();
                }
            }

            hasPopulatedThisVisit = true;
        }

        private List<ShopProductData> CreateRandomCandidates(int requestedCount)
        {
            var productPool = new List<ShopProductData>();
            foreach (var product in catalog.Products)
            {
                if (product != null && product.IsConfigured && product.IsDisplayed)
                {
                    productPool.Add(product);
                }
            }

            if (productPool.Count == 0)
            {
                return productPool;
            }

            var candidates = new List<ShopProductData>(requestedCount);
            if (allowDuplicateProducts)
            {
                for (var index = 0; index < requestedCount; index++)
                {
                    candidates.Add(productPool[Random.Range(0, productPool.Count)]);
                }

                return candidates;
            }

            for (var index = productPool.Count - 1; index > 0; index--)
            {
                var swapIndex = Random.Range(0, index + 1);
                (productPool[index], productPool[swapIndex]) = (productPool[swapIndex], productPool[index]);
            }

            candidates.AddRange(productPool.GetRange(0, Mathf.Min(requestedCount, productPool.Count)));
            return candidates;
        }

        private int GetRandomDisplayCount()
        {
            var minimum = Mathf.Clamp(minimumDisplayCount, 1, displaySlots.Length);
            var maximum = Mathf.Clamp(maximumDisplayCount, minimum, displaySlots.Length);
            return Random.Range(minimum, maximum + 1);
        }

        private void HandleDisplayedOffersChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
        {
            RefreshDisplaysFromNetworkState();
        }

        private void RefreshDisplaysFromNetworkState()
        {
            if (!ValidateSetup())
            {
                return;
            }

            for (var slotIndex = 0; slotIndex < displaySlots.Length; slotIndex++)
            {
                var slot = displaySlots[slotIndex];
                if (slot == null)
                {
                    continue;
                }

                if (slotIndex >= displayedOfferIds.Count || displayedOfferIds[slotIndex].IsEmpty)
                {
                    slot.Clear();
                    continue;
                }

                var offerId = displayedOfferIds[slotIndex].ToString();
                if (!catalog.TryGetByOfferId(offerId, out var product))
                {
                    Debug.LogError(
                        $"PHS_SHOP_DISPLAY_FAILED reason=offer_missing controller={name} offer={offerId}",
                        this);
                    slot.Clear();
                    continue;
                }

                if (slot.CurrentProduct != product)
                {
                    slot.TryPresent(product);
                }
            }
        }

        private void HandleProductPurchased(ShopProductData product)
        {
            if (product == null || product.StockPolicy != ShopStockPolicy.OnePerVisit)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                ClearLocalProduct(product);
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                return;
            }

            for (var index = 0; index < displayedOfferIds.Count; index++)
            {
                if (displayedOfferIds[index].ToString() == product.OfferId)
                {
                    displayedOfferIds[index] = default;
                    return;
                }
            }
        }

        private void ClearLocalProduct(ShopProductData product)
        {
            foreach (var slot in displaySlots)
            {
                if (slot != null && slot.CurrentProduct == product)
                {
                    slot.Clear();
                    return;
                }
            }
        }

        private bool ValidateSetup()
        {
            var valid = true;
            if (catalog == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_SETUP_FAILED reason=catalog_missing controller={name}", this);
                valid = false;
            }

            if (displaySlots == null || displaySlots.Length == 0)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_SETUP_FAILED reason=slots_missing controller={name}", this);
                valid = false;
            }

            else if (displaySlots.Length < minimumDisplayCount)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_SETUP_FAILED reason=insufficient_slots controller={name} slots={displaySlots.Length} required={minimumDisplayCount}", this);
                valid = false;
            }

            if (purchaseService == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_DISPLAY_SETUP_FAILED reason=purchase_service_missing controller={name}",
                    this);
                valid = false;
            }

            return valid;
        }
    }
}
