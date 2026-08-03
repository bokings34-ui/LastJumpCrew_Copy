using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyCustomizationItemRowView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Button previewButton;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        private CosmeticItemData item;
        private Action<CosmeticItemData> previewAction;
        private Action<CosmeticItemData> itemAction;

        public CosmeticItemData Item => item;

        public void Bind(
            CosmeticItemData nextItem,
            Action<CosmeticItemData> onPreview,
            Action<CosmeticItemData> onAction)
        {
            Clear();
            item = nextItem;
            previewAction = onPreview;
            itemAction = onAction;

            itemLabel.text = $"{item.Slot.ToString().ToUpperInvariant()}  {item.DisplayName}";
            priceLabel.text = item.Price.ToString();
            if (iconImage != null)
            {
                iconImage.sprite = item.Icon;
                iconImage.enabled = item.Icon != null;
            }

            previewButton.onClick.AddListener(Preview);
            actionButton.onClick.AddListener(InvokeAction);
        }

        public void Refresh(bool ready, bool owned, bool equipped)
        {
            previewButton.interactable = ready;
            actionLabel.text = !owned
                ? "BUY"
                : equipped
                    ? "UNEQUIP"
                    : "EQUIP";
            actionButton.interactable = ready;
        }

        public void Clear()
        {
            previewButton.onClick.RemoveListener(Preview);
            actionButton.onClick.RemoveListener(InvokeAction);
            item = null;
            previewAction = null;
            itemAction = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Preview()
        {
            previewAction?.Invoke(item);
        }

        private void InvokeAction()
        {
            itemAction?.Invoke(item);
        }
    }
}
