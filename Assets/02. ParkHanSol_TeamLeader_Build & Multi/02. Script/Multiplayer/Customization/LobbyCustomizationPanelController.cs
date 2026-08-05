using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyCustomizationPanelController : MonoBehaviour
    {
        [Serializable]
        private sealed class ItemRowBinding
        {
            [SerializeField] private CosmeticItemData item;
            [SerializeField] private TMP_Text itemLabel;
            [SerializeField] private Button actionButton;
            [SerializeField] private TMP_Text actionLabel;

            public CosmeticItemData Item => item;
            public TMP_Text ItemLabel => itemLabel;
            public Button ActionButton => actionButton;
            public TMP_Text ActionLabel => actionLabel;
        }

        [Serializable]
        private sealed class ColorButtonBinding
        {
            [SerializeField] private Color32 color = new(255, 255, 255, 255);
            [SerializeField] private Button button;
            [SerializeField] private Image swatch;

            public Color32 Color => color;
            public Button Button => button;
            public Image Swatch => swatch;
        }

        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text creditsLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private ItemRowBinding[] itemRows = Array.Empty<ItemRowBinding>();
        [SerializeField] private ColorButtonBinding[] colorButtons = Array.Empty<ColorButtonBinding>();
        [SerializeField] private Button unequipHeadButton;
        [SerializeField] private Button unequipBackButton;
        [SerializeField, Min(0.05f)] private float refreshIntervalSeconds = 0.15f;

        private NetworkPlayerCustomization customization;
        private float nextRefreshTime;

        private void Awake()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            unequipHeadButton.onClick.AddListener(() => RequestUnequip(CosmeticSlot.Head));
            unequipBackButton.onClick.AddListener(() => RequestUnequip(CosmeticSlot.Back));

            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = itemRows[index];
                var capturedItem = row.Item;
                row.ItemLabel.text = $"{capturedItem.DisplayName}  [{capturedItem.Slot}]";
                row.ActionButton.onClick.AddListener(() => RequestItemAction(capturedItem));
            }

            for (var index = 0; index < colorButtons.Length; index++)
            {
                var binding = colorButtons[index];
                var capturedColor = binding.Color;
                binding.Swatch.color = capturedColor;
                binding.Button.onClick.AddListener(() => RequestColor(capturedColor));
            }

            panelRoot.SetActive(false);
            openButton.gameObject.SetActive(true);
            statusLabel.text = string.Empty;
        }

        private void Update()
        {
            if (!panelRoot.activeSelf || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            ResolveCustomization(false);
            RefreshView();
        }

        private void OnDestroy()
        {
            if (customization != null)
            {
                customization.StateChanged -= RefreshView;
            }
        }

        private void OpenPanel()
        {
            if (!ResolveCustomization(true))
            {
                SetStatus("프로필 동기화 대기 중");
                return;
            }

            if (!customization.IsProfileReady)
            {
                SetProfileUnavailableStatus();
                return;
            }

            panelRoot.SetActive(true);
            openButton.gameObject.SetActive(false);
            SetStatus("구매 후 장착 버튼을 누르세요");
            RefreshView();
        }

        private void ClosePanel()
        {
            panelRoot.SetActive(false);
            openButton.gameObject.SetActive(true);
        }

        private bool ResolveCustomization(bool logFailure)
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject = networkManager != null && networkManager.IsListening
                ? networkManager.LocalClient?.PlayerObject
                : null;
            var resolved = playerObject == null
                ? null
                : playerObject.GetComponent<NetworkPlayerCustomization>();
            if (resolved == customization)
            {
                return customization != null;
            }

            if (customization != null)
            {
                customization.StateChanged -= RefreshView;
            }

            customization = resolved;
            if (customization == null)
            {
                if (logFailure)
                {
                    Debug.LogError("PHS_LOBBY_CUSTOMIZATION_BIND_FAILED reason=local_player_missing", this);
                }

                return false;
            }

            if (customization.Catalog != catalog)
            {
                Debug.LogError(
                    $"PHS_LOBBY_CUSTOMIZATION_BIND_FAILED reason=catalog_mismatch player={customization.name}",
                    this);
                customization = null;
                return false;
            }

            customization.StateChanged += RefreshView;
            return true;
        }

        private void RequestItemAction(CosmeticItemData item)
        {
            if (!ResolveCustomization(true))
            {
                SetStatus("프로필 동기화 대기 중");
                return;
            }

            if (!customization.IsProfileReady)
            {
                SetProfileUnavailableStatus();
                return;
            }

            if (customization.OwnsItem(item.ItemId))
            {
                customization.RequestEquip(item.ItemId);
                SetStatus($"{item.DisplayName} 장착 요청");
            }
            else
            {
                customization.RequestPurchase(item.ItemId);
                SetStatus($"{item.DisplayName} 구매 요청");
            }
        }

        private void RequestColor(Color32 color)
        {
            if (!ResolveCustomization(true))
            {
                SetStatus("프로필 동기화 대기 중");
                return;
            }

            if (!customization.IsProfileReady)
            {
                SetProfileUnavailableStatus();
                return;
            }

            customization.RequestSetBodyColor(color);
            SetStatus("몸 색상 변경 요청");
        }

        private void RequestUnequip(CosmeticSlot slot)
        {
            if (!ResolveCustomization(true))
            {
                SetStatus("프로필 동기화 대기 중");
                return;
            }

            if (!customization.IsProfileReady)
            {
                SetProfileUnavailableStatus();
                return;
            }

            customization.RequestUnequip(slot);
            SetStatus($"{slot} 장식 해제 요청");
        }

        private void RefreshView()
        {
            if (!panelRoot.activeSelf)
            {
                return;
            }

            var ready = customization != null && customization.IsProfileReady;
            creditsLabel.text = ready
                ? $"CUSTOM CREDITS  {customization.PersonalCreditsWallet.CurrentCredits}"
                : customization != null && !string.IsNullOrWhiteSpace(customization.ProfileFailureReason)
                    ? "CUSTOM PROFILE ERROR"
                    : "CUSTOM CREDITS  ---";

            if (!ready
                && customization != null
                && !string.IsNullOrWhiteSpace(customization.ProfileFailureReason))
            {
                SetProfileUnavailableStatus();
            }

            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = itemRows[index];
                var owned = ready && customization.OwnsItem(row.Item.ItemId);
                var equipped = owned && IsEquipped(row.Item);
                row.ActionLabel.text = !owned
                    ? $"구매 {row.Item.Price}"
                    : equipped
                        ? "장착 중"
                        : "장착";
                row.ActionButton.interactable = ready && !equipped;
            }

            unequipHeadButton.interactable = ready && !string.IsNullOrEmpty(customization.EquippedHeadId);
            unequipBackButton.interactable = ready && !string.IsNullOrEmpty(customization.EquippedBackId);
            for (var index = 0; index < colorButtons.Length; index++)
            {
                colorButtons[index].Button.interactable = ready
                    && !colorButtons[index].Color.Equals(customization.BodyColor);
            }
        }

        private bool IsEquipped(CosmeticItemData item)
        {
            return item.Slot == CosmeticSlot.Head
                ? customization.EquippedHeadId == item.ItemId
                : customization.EquippedBackId == item.ItemId;
        }

        private void SetStatus(string message)
        {
            statusLabel.text = message;
        }

        private void SetProfileUnavailableStatus()
        {
            if (string.IsNullOrWhiteSpace(customization.ProfileFailureReason))
            {
                SetStatus("프로필 동기화 대기 중");
                return;
            }

            panelRoot.SetActive(true);
            openButton.gameObject.SetActive(false);
            SetStatus($"프로필 오류: {customization.ProfileFailureReason}");
        }

        private bool ValidateSetup()
        {
            if (catalog == null
                || panelRoot == null
                || openButton == null
                || closeButton == null
                || creditsLabel == null
                || statusLabel == null
                || unequipHeadButton == null
                || unequipBackButton == null
                || itemRows == null
                || itemRows.Length == 0
                || colorButtons == null
                || colorButtons.Length == 0)
            {
                Debug.LogError("PHS_LOBBY_CUSTOMIZATION_SETUP_FAILED reason=root_reference_missing", this);
                return false;
            }

            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = itemRows[index];
                if (row == null
                    || row.Item == null
                    || row.ItemLabel == null
                    || row.ActionButton == null
                    || row.ActionLabel == null)
                {
                    Debug.LogError($"PHS_LOBBY_CUSTOMIZATION_SETUP_FAILED reason=item_row_invalid index={index}", this);
                    return false;
                }
            }

            for (var index = 0; index < colorButtons.Length; index++)
            {
                var binding = colorButtons[index];
                if (binding == null
                    || binding.Button == null
                    || binding.Swatch == null
                    || !catalog.IsBodyColorAllowed(binding.Color))
                {
                    Debug.LogError($"PHS_LOBBY_CUSTOMIZATION_SETUP_FAILED reason=color_binding_invalid index={index}", this);
                    return false;
                }
            }

            return true;
        }
    }
}
