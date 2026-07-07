using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolLobbyShopController : MonoBehaviour
    {
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text selectedItemNameText;
        [SerializeField] private TMP_Text selectedItemDescriptionText;
        [SerializeField] private TMP_Text selectedItemPriceText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text equippedPreviewLabel;
        [SerializeField] private Image equippedPreviewImage;
        [SerializeField] private RectTransform itemListContent;
        [SerializeField] private ShopItemView itemViewTemplate;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private List<CosmeticShopItem> items = new();
        [SerializeField] private int currency = 0;

        private readonly List<ShopItemView> runtimeItemViews = new();
        private int selectedIndex;
        private int equippedIndex = -1;

        private void Awake()
        {
            RebuildItemList();
            BindStaticButtons();
            RefreshAll();
        }

        private void OnDestroy()
        {
            UnbindStaticButtons();
            UnbindItemButtons(runtimeItemViews);
        }

        public void SetItems(List<CosmeticShopItem> shopItems)
        {
            items = shopItems ?? new List<CosmeticShopItem>();
            selectedIndex = 0;
            RebuildItemList();
            RefreshAll();
        }

        public void SetItems(IReadOnlyList<CosmeticShopItem> shopItems)
        {
            items = shopItems == null ? new List<CosmeticShopItem>() : new List<CosmeticShopItem>(shopItems);
            selectedIndex = 0;
            RebuildItemList();
            RefreshAll();
        }

        public void SetCurrency(int value)
        {
            currency = Mathf.Max(0, value);
            RefreshAll();
        }

        public void SelectItem(int index)
        {
            if (items == null || index < 0 || index >= items.Count)
            {
                Debug.LogWarning($"PHS_SHOP_SELECT_INVALID index={index}");
                return;
            }

            selectedIndex = index;
            RefreshAll();
        }

        public void BuySelected()
        {
            if (!TryGetSelectedItem(out var item))
            {
                Debug.LogWarning("PHS_SHOP_BUY_FAILED reason=no_selected_item");
                return;
            }

            if (item.IsOwned)
            {
                SetStatus("ALREADY OWNED");
                return;
            }

            if (currency < item.Price)
            {
                SetStatus("NOT ENOUGH CREDITS");
                return;
            }

            currency -= item.Price;
            item.IsOwned = true;
            items[selectedIndex] = item;
            SetStatus($"BOUGHT {item.DisplayName}");
            RefreshAll();
        }

        public void EquipSelected()
        {
            if (!TryGetSelectedItem(out var item))
            {
                Debug.LogWarning("PHS_SHOP_EQUIP_FAILED reason=no_selected_item");
                return;
            }

            if (!item.IsOwned)
            {
                SetStatus("BUY FIRST");
                return;
            }

            equippedIndex = selectedIndex;
            SetStatus($"EQUIPPED {item.DisplayName}");
            RefreshAll();
        }

        private void RebuildItemList()
        {
            UnbindItemButtons(runtimeItemViews);
            runtimeItemViews.Clear();

            if (itemListContent == null || !itemViewTemplate.HasRoot)
            {
                Debug.LogError("PHS_SHOP_LIST_NOT_READY itemListContent/template missing");
                return;
            }

            for (var i = itemListContent.childCount - 1; i >= 0; i--)
            {
                var child = itemListContent.GetChild(i);
                if (child.gameObject != itemViewTemplate.Root)
                {
                    DestroyRuntimeRow(child.gameObject);
                }
            }

            itemViewTemplate.SetActive(false);

            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var view = itemViewTemplate.CreateRuntimeView(itemListContent);
                view.Bind(this, i);
                view.SetActive(true);
                runtimeItemViews.Add(view);
            }
        }

        private void RefreshAll()
        {
            SetText(currencyText, $"CREDITS {currency}");

            if (items == null || items.Count == 0)
            {
                SetText(selectedItemNameText, "NO ITEMS");
                SetText(selectedItemDescriptionText, "SHOP ITEM DATA NOT SET");
                SetText(selectedItemPriceText, "-");
                SetInteractable(buyButton, false);
                SetInteractable(equipButton, false);
                SetText(equippedPreviewLabel, "NO EQUIPPED ITEM");
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, items.Count - 1);
            var selectedItem = items[selectedIndex];
            SetText(selectedItemNameText, selectedItem.DisplayName);
            SetText(selectedItemDescriptionText, selectedItem.Description);
            SetText(selectedItemPriceText, selectedItem.IsOwned ? "OWNED" : $"PRICE {selectedItem.Price}");
            SetInteractable(buyButton, !selectedItem.IsOwned);
            SetInteractable(equipButton, selectedItem.IsOwned && equippedIndex != selectedIndex);
            SetText(equippedPreviewLabel, equippedIndex >= 0 && equippedIndex < items.Count ? items[equippedIndex].DisplayName : "NO EQUIPPED ITEM");

            if (equippedPreviewImage != null)
            {
                equippedPreviewImage.sprite = selectedItem.Icon;
                equippedPreviewImage.color = selectedItem.Icon == null
                    ? equippedIndex >= 0
                        ? new Color(1f, 0.49f, 0f, 0.85f)
                        : new Color(0f, 0.18f, 0.42f, 0.85f)
                    : Color.white;
            }

            if (runtimeItemViews.Count != items.Count)
            {
                RebuildItemList();
            }

            for (var i = 0; i < runtimeItemViews.Count; i++)
            {
                if (i < items.Count)
                {
                    runtimeItemViews[i].Refresh(items[i], i == selectedIndex, i == equippedIndex);
                }
            }
        }

        private bool TryGetSelectedItem(out CosmeticShopItem item)
        {
            item = default;

            if (items == null || selectedIndex < 0 || selectedIndex >= items.Count)
            {
                return false;
            }

            item = items[selectedIndex];
            return true;
        }

        private void BindStaticButtons()
        {
            Bind(buyButton, BuySelected);
            Bind(equipButton, EquipSelected);
        }

        private void UnbindStaticButtons()
        {
            Unbind(buyButton, BuySelected);
            Unbind(equipButton, EquipSelected);
        }

        private static void UnbindItemButtons(List<ShopItemView> views)
        {
            if (views == null)
            {
                return;
            }

            for (var i = 0; i < views.Count; i++)
            {
                views[i].Unbind();
            }
        }

        private static void DestroyRuntimeRow(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private void SetStatus(string message)
        {
            SetText(statusText, message);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetInteractable(Selectable target, bool value)
        {
            if (target != null)
            {
                target.interactable = value;
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        [System.Serializable]
        public struct CosmeticShopItem
        {
            public string ItemId;
            public string DisplayName;
            [TextArea] public string Description;
            [Min(0)] public int Price;
            public Sprite Icon;
            public bool IsOwned;
        }

        [System.Serializable]
        private struct ShopItemView
        {
            [SerializeField] private GameObject root;
            [SerializeField] private Button button;
            [SerializeField] private TMP_Text nameText;
            [SerializeField] private TMP_Text priceText;
            [SerializeField] private TMP_Text stateText;
            [SerializeField] private Image iconImage;

            private ParkHanSolLobbyShopController owner;
            private int index;

            public GameObject Root => root;
            public bool HasRoot => root != null;

            public ShopItemView CreateRuntimeView(Transform parent)
            {
                var instance = Instantiate(root, parent);
                instance.name = root.name.Replace("Template", string.Empty).Trim();

                return new ShopItemView
                {
                    root = instance,
                    button = instance.GetComponent<Button>(),
                    nameText = FindChildText(instance.transform, "Name Text"),
                    priceText = FindChildText(instance.transform, "Price Text"),
                    stateText = FindChildText(instance.transform, "State Text"),
                    iconImage = FindChildImage(instance.transform, "Icon")
                };
            }

            public void Bind(ParkHanSolLobbyShopController controller, int itemIndex)
            {
                owner = controller;
                index = itemIndex;
                button?.onClick.AddListener(Select);
            }

            public void Unbind()
            {
                button?.onClick.RemoveListener(Select);
                owner = null;
            }

            public void SetActive(bool active)
            {
                if (root != null)
                {
                    root.SetActive(active);
                }
            }

            public void Refresh(CosmeticShopItem item, bool selected, bool equipped)
            {
                SetText(nameText, item.DisplayName);
                SetText(priceText, item.IsOwned ? "OWNED" : item.Price.ToString());
                SetText(stateText, equipped ? "EQUIPPED" : selected ? "SELECTED" : string.Empty);
                SetIcon(item.Icon);
            }

            private void Select()
            {
                owner?.SelectItem(index);
            }

            private static TMP_Text FindChildText(Transform parent, string childName)
            {
                foreach (Transform child in parent)
                {
                    if (child.name == childName)
                    {
                        return child.GetComponent<TMP_Text>();
                    }

                    var nested = FindChildText(child, childName);
                    if (nested != null)
                    {
                        return nested;
                    }
                }

                return null;
            }

            private static Image FindChildImage(Transform parent, string childName)
            {
                foreach (Transform child in parent)
                {
                    if (child.name == childName)
                    {
                        return child.GetComponent<Image>();
                    }

                    var nested = FindChildImage(child, childName);
                    if (nested != null)
                    {
                        return nested;
                    }
                }

                return null;
            }

            private void SetIcon(Sprite icon)
            {
                if (iconImage == null)
                {
                    return;
                }

                iconImage.sprite = icon;
                iconImage.color = icon == null ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
                iconImage.enabled = icon != null;
            }
        }
    }
}
