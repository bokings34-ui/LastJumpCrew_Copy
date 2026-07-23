using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyCustomizationFrontendController : MonoBehaviour
    {
        private const int RequiredItemRowCount = 6;
        private const int RequiredColorButtonCount = 6;

        [Serializable]
        private sealed class ItemRowBinding
        {
            [SerializeField] private CosmeticItemData item;
            [SerializeField] private Button previewButton;
            [SerializeField] private TMP_Text itemLabel;
            [SerializeField] private TMP_Text priceLabel;
            [SerializeField] private Button actionButton;
            [SerializeField] private TMP_Text actionLabel;

            public CosmeticItemData Item => item;
            public Button PreviewButton => previewButton;
            public TMP_Text ItemLabel => itemLabel;
            public TMP_Text PriceLabel => priceLabel;
            public Button ActionButton => actionButton;
            public TMP_Text ActionLabel => actionLabel;
        }

        [Serializable]
        private sealed class ColorButtonBinding
        {
            [SerializeField] private Color32 color =
                new Color32(255, 255, 255, 255);
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
        [SerializeField] private LobbyCustomizationPreviewPresenter previewPresenter;
        [SerializeField] private EventSystem lobbyEventSystem;
        [SerializeField] private Button[] blockedLobbyMenuButtons =
            Array.Empty<Button>();
        [SerializeField] private ItemRowBinding[] itemRows =
            Array.Empty<ItemRowBinding>();
        [SerializeField] private ColorButtonBinding[] colorButtons =
            Array.Empty<ColorButtonBinding>();
        [SerializeField] private Button applyColorButton;
        [SerializeField] private Button unequipHeadButton;
        [SerializeField] private Button unequipBackButton;
        [SerializeField] private Button resetPreviewButton;

        private INetworkLobbyCustomizationService service;
        private UnityAction[] previewActions;
        private UnityAction[] itemActions;
        private UnityAction[] colorActions;
        private bool[] blockedLobbyMenuInteractableStates;
        private bool isLobbyMenuBlocked;

        private void Awake()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            BindUiActions();
            panelRoot.SetActive(false);
            openButton.gameObject.SetActive(true);
            statusLabel.text = string.Empty;
            RefreshView();
        }

        private void OnDestroy()
        {
            RestoreLobbyMenuInteraction();
            UnbindService();
            UnbindUiActions();
        }

        private void OnDisable()
        {
            RestoreLobbyMenuInteraction();
        }

        private void OpenPanel()
        {
            if (!TryResolveService(out var reason))
            {
                SetStatus(reason);
                return;
            }

            if (!service.IsProfileReady)
            {
                SetStatus(string.IsNullOrWhiteSpace(service.ProfileFailureReason)
                    ? "PROFILE NOT READY"
                    : $"PROFILE ERROR: {service.ProfileFailureReason}");
                return;
            }

            if (!previewPresenter.TryBind(service, out reason))
            {
                SetStatus($"PREVIEW ERROR: {reason}");
                Debug.LogError(
                    $"PHS_NETWORK_LOBBY_CUSTOMIZATION_PREVIEW_FAILED reason={reason}",
                    this);
                return;
            }

            panelRoot.SetActive(true);
            openButton.gameObject.SetActive(false);
            BlockLobbyMenuInteraction();
            SelectButton(closeButton);
            SetStatus("SELECT AN ITEM TO PREVIEW");
            RefreshView();
        }

        private void ClosePanel()
        {
            if (service != null
                && service.IsProfileReady
                && !service.TryResetPreview(out var reason))
            {
                SetStatus(reason);
                return;
            }

            previewPresenter.ClearBinding();
            panelRoot.SetActive(false);
            openButton.gameObject.SetActive(true);
            RestoreLobbyMenuInteraction();
            SelectButton(openButton);
        }

        private void BlockLobbyMenuInteraction()
        {
            if (isLobbyMenuBlocked)
            {
                return;
            }

            blockedLobbyMenuInteractableStates =
                new bool[blockedLobbyMenuButtons.Length];
            for (var index = 0;
                 index < blockedLobbyMenuButtons.Length;
                 index++)
            {
                var button = blockedLobbyMenuButtons[index];
                blockedLobbyMenuInteractableStates[index] = button.interactable;
                button.interactable = false;
            }

            isLobbyMenuBlocked = true;
        }

        private void RestoreLobbyMenuInteraction()
        {
            if (!isLobbyMenuBlocked)
            {
                return;
            }

            for (var index = 0;
                 index < blockedLobbyMenuButtons.Length;
                 index++)
            {
                var button = blockedLobbyMenuButtons[index];
                if (button != null
                    && blockedLobbyMenuInteractableStates != null
                    && index < blockedLobbyMenuInteractableStates.Length)
                {
                    button.interactable =
                        blockedLobbyMenuInteractableStates[index];
                }
            }

            blockedLobbyMenuInteractableStates = null;
            isLobbyMenuBlocked = false;
        }

        private void SelectButton(Button button)
        {
            lobbyEventSystem.SetSelectedGameObject(null);
            lobbyEventSystem.SetSelectedGameObject(button.gameObject);
        }

        private bool TryResolveService(out string reason)
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject = networkManager != null && networkManager.IsListening
                ? networkManager.LocalClient?.PlayerObject
                : null;
            if (playerObject == null
                || !playerObject.TryGetComponent<NetworkPlayerCustomization>(
                    out var customization))
            {
                reason = "LOCAL PLAYER NOT READY";
                Debug.LogError(
                    "PHS_NETWORK_LOBBY_CUSTOMIZATION_BIND_FAILED reason=local_player_missing",
                    this);
                return false;
            }

            if (customization.Catalog != catalog)
            {
                reason = "CATALOG MISMATCH";
                Debug.LogError(
                    "PHS_NETWORK_LOBBY_CUSTOMIZATION_BIND_FAILED reason=catalog_mismatch",
                    this);
                return false;
            }

            if (ReferenceEquals(service, customization))
            {
                reason = null;
                return true;
            }

            UnbindService();
            service = customization;
            service.StateChanged += HandleStateChanged;
            service.PreviewChanged += HandlePreviewChanged;
            reason = null;
            return true;
        }

        private void UnbindService()
        {
            if (service == null)
            {
                return;
            }

            service.StateChanged -= HandleStateChanged;
            service.PreviewChanged -= HandlePreviewChanged;
            service = null;
        }

        private void HandleStateChanged()
        {
            RefreshView();
        }

        private void HandlePreviewChanged()
        {
            if (!previewPresenter.TryRefresh(out var reason))
            {
                SetStatus($"PREVIEW ERROR: {reason}");
                Debug.LogError(
                    $"PHS_NETWORK_LOBBY_CUSTOMIZATION_PREVIEW_FAILED reason={reason}",
                    this);
            }

            RefreshView();
        }

        private void SelectPreviewItem(CosmeticItemData item)
        {
            if (!TryRequireReadyService(out var reason)
                || !service.TrySelectPreviewItem(item.ItemId, out reason))
            {
                SetStatus(reason);
                return;
            }

            SetStatus($"PREVIEWING {item.DisplayName}");
        }

        private void RequestItemAction(CosmeticItemData item)
        {
            if (!TryRequireReadyService(out var reason))
            {
                SetStatus(reason);
                return;
            }

            var requested = service.OwnsItem(item.ItemId)
                ? service.TryRequestEquip(item.ItemId, out reason)
                : service.TryRequestPurchase(item.ItemId, out reason);
            SetStatus(requested ? "REQUEST SENT" : reason);
        }

        private void SelectPreviewColor(Color32 color)
        {
            if (!TryRequireReadyService(out var reason)
                || !service.TrySelectPreviewBodyColor(color, out reason))
            {
                SetStatus(reason);
                return;
            }

            SetStatus("COLOR PREVIEW");
        }

        private void ApplyPreviewColor()
        {
            if (!TryRequireReadyService(out var reason)
                || !service.TryRequestSetBodyColor(
                    service.PreviewBodyColor,
                    out reason))
            {
                SetStatus(reason);
                return;
            }

            SetStatus("COLOR REQUEST SENT");
        }

        private void RequestUnequip(CosmeticSlot slot)
        {
            if (!TryRequireReadyService(out var reason)
                || !service.TryRequestUnequip(slot, out reason))
            {
                SetStatus(reason);
                return;
            }

            SetStatus($"{slot.ToString().ToUpperInvariant()} UNEQUIP REQUEST SENT");
        }

        private void ResetPreview()
        {
            if (!TryRequireReadyService(out var reason)
                || !service.TryResetPreview(out reason))
            {
                SetStatus(reason);
                return;
            }

            SetStatus("PREVIEW RESET");
        }

        private bool TryRequireReadyService(out string reason)
        {
            if (!TryResolveService(out reason))
            {
                return false;
            }

            if (!service.IsProfileReady)
            {
                reason = string.IsNullOrWhiteSpace(service.ProfileFailureReason)
                    ? "PROFILE NOT READY"
                    : $"PROFILE ERROR: {service.ProfileFailureReason}";
                return false;
            }

            reason = null;
            return true;
        }

        private void RefreshView()
        {
            var ready = service != null && service.IsProfileReady;
            creditsLabel.text = ready && service.PersonalCreditsWallet != null
                ? $"CUSTOM CREDITS  {service.PersonalCreditsWallet.CurrentCredits}"
                : "CUSTOM CREDITS  ---";

            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = itemRows[index];
                var owned = ready && service.OwnsItem(row.Item.ItemId);
                var equipped = owned && IsEquipped(row.Item);
                row.PreviewButton.interactable = ready;
                row.ActionLabel.text = !owned
                    ? "BUY"
                    : equipped
                        ? "EQUIPPED"
                        : "EQUIP";
                row.ActionButton.interactable = ready && !equipped;
            }

            for (var index = 0; index < colorButtons.Length; index++)
            {
                colorButtons[index].Button.interactable = ready;
            }

            applyColorButton.interactable = ready
                && !service.PreviewBodyColor.Equals(service.BodyColor);
            unequipHeadButton.interactable = ready
                && !string.IsNullOrEmpty(service.EquippedHeadId);
            unequipBackButton.interactable = ready
                && !string.IsNullOrEmpty(service.EquippedBackId);
            resetPreviewButton.interactable = ready && IsPreviewChanged();
        }

        private bool IsEquipped(CosmeticItemData item)
        {
            return item.Slot == CosmeticSlot.Head
                ? service.EquippedHeadId == item.ItemId
                : service.EquippedBackId == item.ItemId;
        }

        private bool IsPreviewChanged()
        {
            return service.PreviewHeadId != service.EquippedHeadId
                || service.PreviewBackId != service.EquippedBackId
                || !service.PreviewBodyColor.Equals(service.BodyColor);
        }

        private void BindUiActions()
        {
            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            applyColorButton.onClick.AddListener(ApplyPreviewColor);
            unequipHeadButton.onClick.AddListener(RequestUnequipHead);
            unequipBackButton.onClick.AddListener(RequestUnequipBack);
            resetPreviewButton.onClick.AddListener(ResetPreview);

            previewActions = new UnityAction[itemRows.Length];
            itemActions = new UnityAction[itemRows.Length];
            for (var index = 0; index < itemRows.Length; index++)
            {
                var capturedItem = itemRows[index].Item;
                previewActions[index] = () => SelectPreviewItem(capturedItem);
                itemActions[index] = () => RequestItemAction(capturedItem);
                itemRows[index].PreviewButton.onClick.AddListener(
                    previewActions[index]);
                itemRows[index].ActionButton.onClick.AddListener(
                    itemActions[index]);
            }

            colorActions = new UnityAction[colorButtons.Length];
            for (var index = 0; index < colorButtons.Length; index++)
            {
                var capturedColor = colorButtons[index].Color;
                colorActions[index] = () => SelectPreviewColor(capturedColor);
                colorButtons[index].Button.onClick.AddListener(colorActions[index]);
            }
        }

        private void UnbindUiActions()
        {
            if (openButton != null) openButton.onClick.RemoveListener(OpenPanel);
            if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
            if (applyColorButton != null) applyColorButton.onClick.RemoveListener(ApplyPreviewColor);
            if (unequipHeadButton != null) unequipHeadButton.onClick.RemoveListener(RequestUnequipHead);
            if (unequipBackButton != null) unequipBackButton.onClick.RemoveListener(RequestUnequipBack);
            if (resetPreviewButton != null) resetPreviewButton.onClick.RemoveListener(ResetPreview);

            if (previewActions != null && itemActions != null)
            {
                for (var index = 0; index < itemRows.Length; index++)
                {
                    itemRows[index].PreviewButton.onClick.RemoveListener(previewActions[index]);
                    itemRows[index].ActionButton.onClick.RemoveListener(itemActions[index]);
                }
            }

            if (colorActions != null)
            {
                for (var index = 0; index < colorButtons.Length; index++)
                {
                    colorButtons[index].Button.onClick.RemoveListener(colorActions[index]);
                }
            }
        }

        private void RequestUnequipHead()
        {
            RequestUnequip(CosmeticSlot.Head);
        }

        private void RequestUnequipBack()
        {
            RequestUnequip(CosmeticSlot.Back);
        }

        private void SetStatus(string message)
        {
            statusLabel.text = string.IsNullOrWhiteSpace(message)
                ? "REQUEST FAILED"
                : message;
        }

        private bool ValidateSetup()
        {
            if (catalog == null
                || panelRoot == null
                || openButton == null
                || closeButton == null
                || creditsLabel == null
                || statusLabel == null
                || previewPresenter == null
                || lobbyEventSystem == null
                || blockedLobbyMenuButtons == null
                || blockedLobbyMenuButtons.Length != 4
                || applyColorButton == null
                || unequipHeadButton == null
                || unequipBackButton == null
                || resetPreviewButton == null
                || itemRows == null
                || itemRows.Length != RequiredItemRowCount
                || colorButtons == null
                || colorButtons.Length != RequiredColorButtonCount)
            {
                return FailSetup("root_reference_or_count_invalid");
            }

            var blockedButtons = new HashSet<Button>();
            for (var index = 0;
                 index < blockedLobbyMenuButtons.Length;
                 index++)
            {
                if (blockedLobbyMenuButtons[index] == null
                    || !blockedButtons.Add(blockedLobbyMenuButtons[index]))
                {
                    return FailSetup(
                        $"blocked_lobby_menu_button_invalid:index={index}");
                }
            }

            if (catalog.Items.Count != RequiredItemRowCount
                || catalog.AllowedBodyColors.Count != RequiredColorButtonCount)
            {
                return FailSetup("catalog_count_invalid");
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = itemRows[index];
                if (row == null
                    || row.Item == null
                    || row.PreviewButton == null
                    || row.ItemLabel == null
                    || row.PriceLabel == null
                    || row.ActionButton == null
                    || row.ActionLabel == null
                    || !itemIds.Add(row.Item.ItemId)
                    || !catalog.TryGetItem(row.Item.ItemId, out var catalogItem)
                    || catalogItem != row.Item)
                {
                    return FailSetup($"item_row_invalid:index={index}");
                }

                row.ItemLabel.text = row.Item.DisplayName;
                row.PriceLabel.text = row.Item.Price.ToString();
            }

            var colors = new HashSet<Color32>();
            for (var index = 0; index < colorButtons.Length; index++)
            {
                var binding = colorButtons[index];
                if (binding == null
                    || binding.Button == null
                    || binding.Swatch == null
                    || !catalog.IsBodyColorAllowed(binding.Color)
                    || !colors.Add(binding.Color))
                {
                    return FailSetup($"color_binding_invalid:index={index}");
                }

                binding.Swatch.color = binding.Color;
            }

            if (!previewPresenter.ValidateCatalog(catalog, out var reason))
            {
                return FailSetup(reason);
            }

            return true;
        }

        private bool FailSetup(string reason)
        {
            Debug.LogError(
                $"PHS_NETWORK_LOBBY_CUSTOMIZATION_SETUP_FAILED reason={reason}",
                this);
            return false;
        }
    }
}
