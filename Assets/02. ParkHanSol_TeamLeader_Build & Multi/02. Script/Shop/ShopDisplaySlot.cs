using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public sealed class ShopDisplaySlot : MonoBehaviour
    {
        [SerializeField] private Transform itemSpawnPoint;
        [SerializeField] private TMP_Text productLabel;
        [SerializeField] private TMP_Text productNameText;
        [SerializeField] private TMP_Text priceText;
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

            CurrentProduct = product;
            RefreshLabels(product);
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

            RefreshLabels(null);
        }

        private void RefreshLabels(ShopProductData product)
        {
            if (productLabel != null)
            {
                productLabel.text = product == null
                    ? string.Empty
                    : $"{product.ItemPrefabData.DisplayName}\n{product.PurchasePrice} CR";
            }

            if (productNameText != null)
            {
                productNameText.text = product == null ? string.Empty : product.ItemPrefabData.DisplayName;
            }

            if (priceText != null)
            {
                priceText.text = product == null ? string.Empty : $"{product.PurchasePrice} CR";
            }
        }
    }
}
