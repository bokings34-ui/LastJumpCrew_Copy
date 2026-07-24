using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public sealed class ShopDisplaySlot : MonoBehaviour
    {
        [SerializeField] private Transform itemSpawnPoint;
        [SerializeField] private SpriteRenderer productIconRenderer;
        [SerializeField, Min(0.01f)] private float displayScaleMultiplier = 0.75f;

        private GameObject displayedItem;

        public ShopProductData CurrentProduct { get; private set; }

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

            var itemData = product.ItemPrefabData;
            if (productIconRenderer == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=icon_renderer_missing slot={name}", this);
                return false;
            }

            if (itemData.Icon == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=item_icon_missing slot={name} offer={product.OfferId}", itemData);
                return false;
            }

            if (!itemData.HasHeldPrefab)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=local_prefab_missing slot={name} offer={product.OfferId}", product);
                return false;
            }

            displayedItem = Instantiate(itemData.HeldPrefab, itemSpawnPoint.position, itemSpawnPoint.rotation);
            displayedItem.transform.localScale *= displayScaleMultiplier;
            displayedItem.name = $"PHS_ShopDisplay_{product.OfferId}";
            if (!displayedItem.TryGetComponent<UtilityItemObject>(out var itemObject) || itemObject.ItemPrefabData != itemData)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=item_prefab_data_mismatch slot={name} offer={product.OfferId}", displayedItem);
                Destroy(displayedItem);
                displayedItem = null;
                return false;
            }

            var pricePresentation = displayedItem.GetComponentInChildren<ShopItemPricePresentation>(true);
            if (pricePresentation == null)
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=item_price_presentation_missing slot={name} offer={product.OfferId}", displayedItem);
                Destroy(displayedItem);
                displayedItem = null;
                return false;
            }

            if (!pricePresentation.TryShow(product.PurchasePrice))
            {
                Debug.LogError($"PHS_SHOP_DISPLAY_FAILED reason=item_price_presentation_invalid slot={name} offer={product.OfferId}", displayedItem);
                Destroy(displayedItem);
                displayedItem = null;
                return false;
            }

            CurrentProduct = product;
            RefreshIcon(itemData.Icon);
            return true;
        }

        public void Clear()
        {
            CurrentProduct = null;
            if (displayedItem != null)
            {
                Destroy(displayedItem);
                displayedItem = null;
            }

            RefreshIcon(null);
        }

        private void RefreshIcon(Sprite icon)
        {
            if (productIconRenderer == null)
                return;

            productIconRenderer.sprite = icon;
            productIconRenderer.enabled = icon != null;
        }
    }
}
