using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public sealed class ShopDisplaySlot : MonoBehaviour, IShopStockPresentation
    {
        [SerializeField] private Transform itemSpawnPoint;
        [SerializeField] private NetworkShopStockRegistry stockRegistry;
        [SerializeField] private TMP_Text productLabel;
        [SerializeField] private TMP_Text productNameText;
        [SerializeField] private TMP_Text priceText;

        public ShopProductData CurrentProduct { get; private set; }
        public Transform PresentationAnchor => itemSpawnPoint;
        public bool IsInStock { get; private set; }

        public bool TryPresent(ShopProductData product)
        {
            Clear();
            if (itemSpawnPoint == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=spawn_point_missing slot={name}", this);
                return false;
            }

            if (product == null || !product.IsConfigured)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=product_invalid slot={name}", this);
                return false;
            }

            if (stockRegistry == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=stock_registry_missing slot={name}", this);
                return false;
            }

            CurrentProduct = product;
            IsInStock = false;
            RefreshLabels(product);
            if (!stockRegistry.TryPresent(this, product))
            {
                CurrentProduct = null;
                RefreshLabels(null);
                return false;
            }

            return true;
        }

        public void Clear()
        {
            stockRegistry?.Clear(this);
            CurrentProduct = null;
            IsInStock = false;
            RefreshLabels(null);
        }

        public void ApplyStockAvailability(bool isAvailable)
        {
            IsInStock = CurrentProduct != null && isAvailable;
            RefreshLabelVisibility();
        }

        private void RefreshLabels(ShopProductData product)
        {
            if (productLabel != null)
            {
                productLabel.text = product == null
                    ? string.Empty
                    : $"${product.PurchasePrice}";
            }

            if (productNameText != null)
            {
                productNameText.text = string.Empty;
                productNameText.gameObject.SetActive(false);
            }

            if (priceText != null)
            {
                priceText.text = product == null ? string.Empty : $"${product.PurchasePrice}";
            }

            RefreshLabelVisibility();
        }

        private void RefreshLabelVisibility()
        {
            var shouldShow = CurrentProduct != null && IsInStock;
            if (productLabel != null)
            {
                productLabel.gameObject.SetActive(shouldShow);
            }

            if (priceText != null && priceText != productLabel)
            {
                priceText.gameObject.SetActive(shouldShow);
            }
        }
    }
}
