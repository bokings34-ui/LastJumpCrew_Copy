using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [CreateAssetMenu(
        fileName = "PHS_ShopCatalog",
        menuName = "LastJumpCrew/ParkHanSol/Shop/Shop Catalog")]
    public sealed class ShopCatalogSO : ScriptableObject, IShopCatalog
    {
        [SerializeField] private List<ShopProductData> products = new();

        private IReadOnlyList<ShopProductData> orderedProducts = Array.Empty<ShopProductData>();
        private Dictionary<string, ShopProductData> productsByOfferId;
        private Dictionary<UtilityItemPrefabData, ShopProductData> productsByItemData;
        private Dictionary<int, ShopProductData> productsByEconomyItemId;

        public IReadOnlyList<ShopProductData> Products
        {
            get
            {
                EnsureIndexes();
                return orderedProducts;
            }
        }

        public bool TryGetByOfferId(string offerId, out ShopProductData product)
        {
            product = null;
            if (string.IsNullOrWhiteSpace(offerId))
            {
                return false;
            }

            EnsureIndexes();
            return productsByOfferId.TryGetValue(offerId, out product);
        }

        public bool TryGetByItemData(UtilityItemPrefabData itemData, out ShopProductData product)
        {
            product = null;
            if (itemData == null)
            {
                return false;
            }

            EnsureIndexes();
            return productsByItemData.TryGetValue(itemData, out product);
        }

        public bool TryGetByEconomyItemId(int economyItemId, out ShopProductData product)
        {
            product = null;
            if (economyItemId <= 0)
            {
                return false;
            }

            EnsureIndexes();
            return productsByEconomyItemId.TryGetValue(economyItemId, out product);
        }

        private void OnEnable()
        {
            RebuildIndexes();
        }

        private void OnValidate()
        {
            RebuildIndexes();
        }

        private void EnsureIndexes()
        {
            if (productsByOfferId == null || productsByItemData == null || productsByEconomyItemId == null)
            {
                RebuildIndexes();
            }
        }

        private void RebuildIndexes()
        {
            productsByOfferId = new Dictionary<string, ShopProductData>(StringComparer.Ordinal);
            productsByItemData = new Dictionary<UtilityItemPrefabData, ShopProductData>();
            productsByEconomyItemId = new Dictionary<int, ShopProductData>();

            var duplicateOfferIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateItemData = new HashSet<UtilityItemPrefabData>();
            var duplicateEconomyItemIds = new HashSet<int>();
            var sortedProducts = new List<ShopProductData>(products?.Count ?? 0);

            if (products == null)
            {
                orderedProducts = Array.Empty<ShopProductData>();
                Debug.LogError($"PHS_SHOP_CATALOG_PRODUCTS_NULL catalog={name}", this);
                return;
            }

            for (var index = 0; index < products.Count; index++)
            {
                var product = products[index];
                if (product == null)
                {
                    Debug.LogError($"PHS_SHOP_CATALOG_PRODUCT_NULL catalog={name} index={index}", this);
                    continue;
                }

                sortedProducts.Add(product);
                ValidateProduct(product, index);
                IndexOfferId(product, index, duplicateOfferIds);
                IndexItemData(product, index, duplicateItemData);
                IndexEconomyItemId(product, index, duplicateEconomyItemIds);
            }

            sortedProducts.Sort(CompareProducts);
            orderedProducts = sortedProducts.AsReadOnly();
        }

        private void ValidateProduct(ShopProductData product, int index)
        {
            if (product.PurchasePrice <= 0)
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_PRICE_INVALID catalog={name} index={index} product={product.name} price={product.PurchasePrice}",
                    this);
            }

            var itemData = product.ItemPrefabData;
            if (itemData == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_ITEM_DATA_MISSING catalog={name} index={index} product={product.name}",
                    this);
                return;
            }

            if (!itemData.HasHeldPrefab)
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_HELD_PREFAB_MISSING catalog={name} index={index} product={product.name} item={itemData.ItemId}",
                    this);
            }

            if (!itemData.HasDroppedPrefab)
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_DROPPED_PREFAB_MISSING catalog={name} index={index} product={product.name} item={itemData.ItemId}",
                    this);
            }
        }

        private void IndexOfferId(
            ShopProductData product,
            int index,
            HashSet<string> duplicateOfferIds)
        {
            var offerId = product.OfferId;
            if (string.IsNullOrWhiteSpace(offerId))
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_OFFER_ID_MISSING catalog={name} index={index} product={product.name}",
                    this);
                return;
            }

            if (duplicateOfferIds.Contains(offerId))
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_OFFER_ID_DUPLICATE catalog={name} index={index} product={product.name} offerId={offerId}",
                    this);
                return;
            }

            if (productsByOfferId.TryAdd(offerId, product))
            {
                return;
            }

            productsByOfferId.Remove(offerId);
            duplicateOfferIds.Add(offerId);
            Debug.LogError(
                $"PHS_SHOP_CATALOG_OFFER_ID_DUPLICATE catalog={name} index={index} product={product.name} offerId={offerId}",
                this);
        }

        private void IndexItemData(
            ShopProductData product,
            int index,
            HashSet<UtilityItemPrefabData> duplicateItemData)
        {
            var itemData = product.ItemPrefabData;
            if (itemData == null)
            {
                return;
            }

            if (duplicateItemData.Contains(itemData))
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_ITEM_DATA_DUPLICATE catalog={name} index={index} product={product.name} item={itemData.ItemId}",
                    this);
                return;
            }

            if (productsByItemData.TryAdd(itemData, product))
            {
                return;
            }

            productsByItemData.Remove(itemData);
            duplicateItemData.Add(itemData);
            Debug.LogError(
                $"PHS_SHOP_CATALOG_ITEM_DATA_DUPLICATE catalog={name} index={index} product={product.name} item={itemData.ItemId}",
                this);
        }

        private void IndexEconomyItemId(
            ShopProductData product,
            int index,
            HashSet<int> duplicateEconomyItemIds)
        {
            var economyItemId = product.EconomyItemId;
            if (economyItemId <= 0)
            {
                return;
            }

            if (duplicateEconomyItemIds.Contains(economyItemId))
            {
                Debug.LogError(
                    $"PHS_SHOP_CATALOG_ECONOMY_ITEM_ID_DUPLICATE catalog={name} index={index} product={product.name} economyItemId={economyItemId}",
                    this);
                return;
            }

            if (productsByEconomyItemId.TryAdd(economyItemId, product))
            {
                return;
            }

            productsByEconomyItemId.Remove(economyItemId);
            duplicateEconomyItemIds.Add(economyItemId);
            Debug.LogError(
                $"PHS_SHOP_CATALOG_ECONOMY_ITEM_ID_DUPLICATE catalog={name} index={index} product={product.name} economyItemId={economyItemId}",
                this);
        }

        private static int CompareProducts(ShopProductData left, ShopProductData right)
        {
            var orderComparison = left.DisplayOrder.CompareTo(right.DisplayOrder);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            return string.Compare(left.OfferId, right.OfferId, StringComparison.Ordinal);
        }
    }
}
