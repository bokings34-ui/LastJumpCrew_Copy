using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyCustomizationFrontendController : MonoBehaviour
    {
        private enum ItemFilter
        {
            All,
            Head,
            Back,
            Pet
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
        [SerializeField] private LobbyLocalCustomizationService localService;
        [SerializeField] private LobbyCustomizationPreviewPresenter previewPresenter;
        [SerializeField] private EventSystem lobbyEventSystem;
        [SerializeField] private Button[] blockedLobbyMenuButtons = Array.Empty<Button>();
        [SerializeField] private RectTransform itemContent;
        [SerializeField] private LobbyCustomizationItemRowView itemRowTemplate;
        [SerializeField] private Button allItemsButton;
        [SerializeField] private Button headItemsButton;
        [SerializeField] private Button backItemsButton;
        [SerializeField] private Button petItemsButton;
        [SerializeField] private ColorButtonBinding[] colorButtons = Array.Empty<ColorButtonBinding>();
        [SerializeField] private Button applyColorButton;
        [SerializeField] private Button unequipHeadButton;
        [SerializeField] private Button unequipBackButton;
        [SerializeField] private Button resetPreviewButton;

        private readonly List<LobbyCustomizationItemRowView> itemRows = new();
        private ILobbyCustomizationService service;
        private bool[] blockedLobbyMenuInteractableStates;
        private bool isLobbyMenuBlocked;
        private ItemFilter activeFilter;

        private void Awake()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            CreateItemRows();
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
            ClearItemRows();
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
                Debug.LogError($"PHS_NETWORK_LOBBY_CUSTOMIZATION_PREVIEW_FAILED reason={reason}", this);
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
            if (service != null && service.IsProfileReady
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

        private void CreateItemRows()
        {
            itemRowTemplate.gameObject.SetActive(false);
            foreach (var item in catalog.Items)
            {
                if (item == null)
                {
                    Debug.LogError("PHS_NETWORK_LOBBY_CUSTOMIZATION_SETUP_FAILED reason=catalog_item_missing", this);
                    continue;
                }

                var row = Instantiate(itemRowTemplate, itemContent);
                row.name = $"ItemRow_{item.ItemId}";
                row.gameObject.SetActive(true);
                row.Bind(item, SelectPreviewItem, RequestItemAction);
                itemRows.Add(row);
            }
        }

        private void ClearItemRows()
        {
            foreach (var row in itemRows)
            {
                if (row != null)
                {
                    row.Clear();
                }
            }

            itemRows.Clear();
        }

        private void SetFilter(ItemFilter filter)
        {
            activeFilter = filter;
            RefreshView();
        }

        private bool IsVisible(CosmeticItemData item)
        {
            return activeFilter == ItemFilter.All
                || activeFilter == ItemFilter.Head && item.Slot == CosmeticSlot.Head
                || activeFilter == ItemFilter.Back && item.Slot == CosmeticSlot.Back
                || activeFilter == ItemFilter.Pet && item.Slot == CosmeticSlot.Pet;
        }

        private void BlockLobbyMenuInteraction()
        {
            if (isLobbyMenuBlocked)
            {
                return;
            }

            blockedLobbyMenuInteractableStates = new bool[blockedLobbyMenuButtons.Length];
            for (var index = 0; index < blockedLobbyMenuButtons.Length; index++)
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

            for (var index = 0; index < blockedLobbyMenuButtons.Length; index++)
            {
                var button = blockedLobbyMenuButtons[index];
                if (button != null && blockedLobbyMenuInteractableStates != null
                    && index < blockedLobbyMenuInteractableStates.Length)
                {
                    button.interactable = blockedLobbyMenuInteractableStates[index];
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
            ILobbyCustomizationService resolvedService = null;
            if (playerObject != null && playerObject.TryGetComponent<NetworkPlayerCustomization>(out var customization))
            {
                resolvedService = customization;
            }
            else if (localService != null)
            {
                resolvedService = localService;
            }

            if (resolvedService == null)
            {
                reason = "CUSTOMIZATION SERVICE NOT READY";
                Debug.LogError("PHS_NETWORK_LOBBY_CUSTOMIZATION_BIND_FAILED reason=service_missing", this);
                return false;
            }

            if (resolvedService.Catalog != catalog)
            {
                reason = "CATALOG MISMATCH";
                Debug.LogError("PHS_NETWORK_LOBBY_CUSTOMIZATION_BIND_FAILED reason=catalog_mismatch", this);
                return false;
            }

            if (ReferenceEquals(service, resolvedService))
            {
                reason = null;
                return true;
            }

            UnbindService();
            service = resolvedService;
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
                Debug.LogError($"PHS_NETWORK_LOBBY_CUSTOMIZATION_PREVIEW_FAILED reason={reason}", this);
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
                || !service.TryRequestSetBodyColor(service.PreviewBodyColor, out reason))
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
            creditsLabel.text = ready ? $"CUSTOM CREDITS  {service.CurrentCredits}" : "CUSTOM CREDITS  ---";

            foreach (var row in itemRows)
            {
                var item = row.Item;
                row.gameObject.SetActive(IsVisible(item));
                var owned = ready && service.OwnsItem(item.ItemId);
                row.Refresh(ready, owned, owned && IsEquipped(item));
            }

            for (var index = 0; index < colorButtons.Length; index++)
            {
                colorButtons[index].Button.interactable = ready;
            }

            applyColorButton.interactable = ready && !service.PreviewBodyColor.Equals(service.BodyColor);
            unequipHeadButton.interactable = ready && !string.IsNullOrEmpty(service.EquippedHeadId);
            unequipBackButton.interactable = ready && !string.IsNullOrEmpty(service.EquippedBackId);
            resetPreviewButton.interactable = ready && IsPreviewChanged();
        }

        private bool IsEquipped(CosmeticItemData item)
        {
            return item.Slot switch
            {
                CosmeticSlot.Head => service.EquippedHeadId == item.ItemId,
                CosmeticSlot.Back => service.EquippedBackId == item.ItemId,
                CosmeticSlot.Pet => service.EquippedPetId == item.ItemId,
                CosmeticSlot.Front => service.EquippedFrontId == item.ItemId,
                _ => false
            };
        }

        private bool IsPreviewChanged()
        {
            return service.PreviewHeadId != service.EquippedHeadId
                || service.PreviewBackId != service.EquippedBackId
                || service.PreviewPetId != service.EquippedPetId
                || service.PreviewFrontId != service.EquippedFrontId
                || !service.PreviewBodyColor.Equals(service.BodyColor);
        }

        private void BindUiActions()
        {
            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            allItemsButton.onClick.AddListener(ShowAllItems);
            headItemsButton.onClick.AddListener(ShowHeadItems);
            backItemsButton.onClick.AddListener(ShowBackItems);
            petItemsButton.onClick.AddListener(ShowPetItems);
            applyColorButton.onClick.AddListener(ApplyPreviewColor);
            unequipHeadButton.onClick.AddListener(RequestUnequipHead);
            unequipBackButton.onClick.AddListener(RequestUnequipBack);
            resetPreviewButton.onClick.AddListener(ResetPreview);
            for (var index = 0; index < colorButtons.Length; index++)
            {
                var color = colorButtons[index].Color;
                colorButtons[index].Button.onClick.AddListener(() => SelectPreviewColor(color));
            }
        }

        private void UnbindUiActions()
        {
            if (openButton != null) openButton.onClick.RemoveListener(OpenPanel);
            if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
            if (allItemsButton != null) allItemsButton.onClick.RemoveListener(ShowAllItems);
            if (headItemsButton != null) headItemsButton.onClick.RemoveListener(ShowHeadItems);
            if (backItemsButton != null) backItemsButton.onClick.RemoveListener(ShowBackItems);
            if (petItemsButton != null) petItemsButton.onClick.RemoveListener(ShowPetItems);
            if (applyColorButton != null) applyColorButton.onClick.RemoveListener(ApplyPreviewColor);
            if (unequipHeadButton != null) unequipHeadButton.onClick.RemoveListener(RequestUnequipHead);
            if (unequipBackButton != null) unequipBackButton.onClick.RemoveListener(RequestUnequipBack);
            if (resetPreviewButton != null) resetPreviewButton.onClick.RemoveListener(ResetPreview);
        }

        private void ShowAllItems() => SetFilter(ItemFilter.All);
        private void ShowHeadItems() => SetFilter(ItemFilter.Head);
        private void ShowBackItems() => SetFilter(ItemFilter.Back);
        private void ShowPetItems() => SetFilter(ItemFilter.Pet);
        private void RequestUnequipHead() => RequestUnequip(CosmeticSlot.Head);
        private void RequestUnequipBack() => RequestUnequip(CosmeticSlot.Back);

        private void SetStatus(string message)
        {
            statusLabel.text = string.IsNullOrWhiteSpace(message) ? "REQUEST FAILED" : message;
        }

        private bool ValidateSetup()
        {
            if (catalog == null || panelRoot == null
                || openButton == null || closeButton == null || creditsLabel == null
                || statusLabel == null || localService == null || previewPresenter == null
                || lobbyEventSystem == null || blockedLobbyMenuButtons == null
                || itemContent == null || itemRowTemplate == null || allItemsButton == null
                || headItemsButton == null || backItemsButton == null || petItemsButton == null
                || applyColorButton == null
                || unequipHeadButton == null || unequipBackButton == null || resetPreviewButton == null
                || colorButtons == null || colorButtons.Length == 0)
            {
                return FailSetup("root_reference_missing");
            }

            var blockedButtons = new HashSet<Button>();
            foreach (var button in blockedLobbyMenuButtons)
            {
                if (button == null || !blockedButtons.Add(button))
                {
                    return FailSetup("blocked_lobby_menu_button_invalid");
                }
            }

            foreach (var binding in colorButtons)
            {
                if (binding == null || binding.Button == null || binding.Swatch == null
                    || !catalog.IsBodyColorAllowed(binding.Color))
                {
                    return FailSetup("color_binding_invalid");
                }

                binding.Swatch.color = binding.Color;
            }

            return previewPresenter.ValidateCatalog(catalog, out var reason)
                ? true
                : FailSetup(reason);
        }

        private bool FailSetup(string reason)
        {
            Debug.LogError($"PHS_NETWORK_LOBBY_CUSTOMIZATION_SETUP_FAILED reason={reason}", this);
            return false;
        }
    }
}
