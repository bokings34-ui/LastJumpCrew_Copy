using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LocalLobbyCustomizationService : MonoBehaviour,
        INetworkLobbyCustomizationService
    {
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField, Min(0)] private int startingCredits = 300;
        [SerializeField, Min(1)] private int maximumCredits = 999999;

        private readonly HashSet<string> ownedItemIds =
            new(StringComparer.Ordinal);
        private int currentCredits;
        private string equippedHeadId = string.Empty;
        private string equippedBackId = string.Empty;
        private Color32 bodyColor = new(255, 255, 255, 255);
        private string previewHeadId = string.Empty;
        private string previewBackId = string.Empty;
        private Color32 previewBodyColor = new(255, 255, 255, 255);
        private bool isProfileReady;
        private string profileFailureReason = string.Empty;
        private string creditsFailureReason = string.Empty;

        public CosmeticCatalog Catalog => catalog;
        public int CurrentCredits => currentCredits;
        public string CreditsFailureReason => creditsFailureReason;
        public bool IsProfileReady => isProfileReady;
        public string ProfileFailureReason => profileFailureReason;
        public string EquippedHeadId => equippedHeadId;
        public string EquippedBackId => equippedBackId;
        public Color32 BodyColor => bodyColor;
        public string PreviewHeadId => previewHeadId;
        public string PreviewBackId => previewBackId;
        public Color32 PreviewBodyColor => previewBodyColor;

        public event Action StateChanged;
        public event Action PreviewChanged;

        private void Awake()
        {
            var appearanceLoaded = TryLoadAppearanceProfile(
                out var appearanceReason);
            var creditsLoaded = TryLoadCredits(out var creditsReason);
            profileFailureReason = !string.IsNullOrWhiteSpace(appearanceReason)
                ? appearanceReason
                : creditsReason;
            creditsFailureReason = creditsReason;
            if (!appearanceLoaded || !creditsLoaded)
            {
                Debug.LogError(
                    "PHS_LOCAL_COSMETIC_PROFILE_LOAD_FAILED " +
                    $"appearance={appearanceReason} credits={creditsReason}",
                    this);
                return;
            }

            isProfileReady = true;
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
        }

        public bool OwnsItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId)
                && ownedItemIds.Contains(itemId);
        }

        public bool TrySelectPreviewItem(string itemId, out string reason)
        {
            if (!TryGetReadyItem(itemId, out var item, out reason)) return false;
            if (item.Slot == CosmeticSlot.Head) previewHeadId = item.ItemId;
            else if (item.Slot == CosmeticSlot.Back) previewBackId = item.ItemId;
            else
            {
                reason = $"slot_invalid:{item.Slot}";
                return false;
            }

            PreviewChanged?.Invoke();
            return true;
        }

        public bool TrySelectPreviewBodyColor(Color32 color, out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            previewBodyColor = color;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TryResetPreview(out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            ResetPreviewToEquipped();
            return true;
        }

        public bool TryRequestPurchase(string itemId, out string reason)
        {
            if (!TryGetReadyItem(itemId, out var item, out reason)) return false;
            if (ownedItemIds.Contains(item.ItemId))
            {
                reason = $"already_owned:{item.ItemId}";
                return false;
            }
            if (item.Price <= 0 || currentCredits < item.Price)
            {
                reason = $"credits_insufficient:{currentCredits}:{item.Price}";
                return false;
            }

            currentCredits -= item.Price;
            ownedItemIds.Add(item.ItemId);
            SaveProfile();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestEquip(string itemId, out string reason)
        {
            if (!TryGetReadyItem(itemId, out var item, out reason)) return false;
            if (!ownedItemIds.Contains(item.ItemId))
            {
                reason = $"item_not_owned:{item.ItemId}";
                return false;
            }

            if (item.Slot == CosmeticSlot.Head) equippedHeadId = item.ItemId;
            else if (item.Slot == CosmeticSlot.Back) equippedBackId = item.ItemId;
            else
            {
                reason = $"slot_invalid:{item.Slot}";
                return false;
            }

            SaveProfile();
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestUnequip(CosmeticSlot slot, out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            if (slot == CosmeticSlot.Head) equippedHeadId = string.Empty;
            else if (slot == CosmeticSlot.Back) equippedBackId = string.Empty;
            else
            {
                reason = $"slot_invalid:{slot}";
                return false;
            }

            SaveProfile();
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestSetBodyColor(Color32 color, out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            bodyColor = color;
            SaveProfile();
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
            return true;
        }

        private bool TryLoadAppearanceProfile(out string reason)
        {
            if (catalog == null)
            {
                reason = "catalog_missing";
                return false;
            }

            ownedItemIds.Clear();
            var serializedOwned = PlayerPrefs.GetString(
                LobbyCustomizationProfileKeys.OwnedItems,
                string.Empty);
            foreach (var id in serializedOwned.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ownedItemIds.Add(id) || !catalog.TryGetItem(id, out _))
                {
                    reason = $"owned_item_invalid:{id}";
                    return false;
                }
            }

            equippedHeadId = PlayerPrefs.GetString(
                LobbyCustomizationProfileKeys.Head,
                string.Empty);
            equippedBackId = PlayerPrefs.GetString(
                LobbyCustomizationProfileKeys.Back,
                string.Empty);
            if (!ValidateEquipment(equippedHeadId, CosmeticSlot.Head, out reason)
                || !ValidateEquipment(equippedBackId, CosmeticSlot.Back, out reason)
                || !TryLoadColor(out bodyColor, out reason))
            {
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryLoadCredits(out string reason)
        {
            var hasSavedCredits = PlayerPrefs.HasKey(
                LobbyCustomizationProfileKeys.Credits);
            currentCredits = hasSavedCredits
                ? PlayerPrefs.GetInt(LobbyCustomizationProfileKeys.Credits)
                : startingCredits;
            if (currentCredits < 0 || currentCredits > maximumCredits)
            {
                reason = hasSavedCredits
                    ? $"saved_credits_out_of_range:{currentCredits}:{maximumCredits}"
                    : $"starting_credits_out_of_range:{currentCredits}:{maximumCredits}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateEquipment(
            string itemId,
            CosmeticSlot slot,
            out string reason)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                reason = null;
                return true;
            }
            if (!ownedItemIds.Contains(itemId)
                || !catalog.TryGetItem(itemId, out var item)
                || item.Slot != slot)
            {
                reason = $"equipped_item_invalid:{itemId}:{slot}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryLoadColor(out Color32 color, out string reason)
        {
            if (!PlayerPrefs.HasKey(LobbyCustomizationProfileKeys.Color))
            {
                color = new Color32(255, 255, 255, 255);
            }
            else
            {
                var parts = PlayerPrefs.GetString(
                    LobbyCustomizationProfileKeys.Color).Split(',');
                if (parts.Length != 4
                    || !byte.TryParse(parts[0], out var r)
                    || !byte.TryParse(parts[1], out var g)
                    || !byte.TryParse(parts[2], out var b)
                    || !byte.TryParse(parts[3], out var a))
                {
                    color = default;
                    reason = "saved_color_invalid";
                    return false;
                }
                color = new Color32(r, g, b, a);
            }

            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = PlayerPrefs.HasKey(LobbyCustomizationProfileKeys.Color)
                    ? $"saved_color_not_allowed:{color}"
                    : "default_color_not_allowed";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryGetReadyItem(
            string itemId,
            out CosmeticItemData item,
            out string reason)
        {
            item = null;
            if (!CanUseProfile(out reason)) return false;
            if (!catalog.TryGetItem(itemId, out item))
            {
                reason = $"catalog_item_missing:{itemId}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool CanUseProfile(out string reason)
        {
            if (!isProfileReady)
            {
                reason = string.IsNullOrWhiteSpace(profileFailureReason)
                    ? "profile_not_ready"
                    : $"profile_failed:{profileFailureReason}";
                return false;
            }

            reason = null;
            return true;
        }

        private void ResetPreviewToEquipped()
        {
            previewHeadId = equippedHeadId;
            previewBackId = equippedBackId;
            previewBodyColor = bodyColor;
            PreviewChanged?.Invoke();
        }

        private void SaveProfile()
        {
            PlayerPrefs.SetInt(
                LobbyCustomizationProfileKeys.Credits,
                currentCredits);
            PlayerPrefs.SetString(
                LobbyCustomizationProfileKeys.OwnedItems,
                string.Join(',', ownedItemIds));
            PlayerPrefs.SetString(
                LobbyCustomizationProfileKeys.Head,
                equippedHeadId);
            PlayerPrefs.SetString(
                LobbyCustomizationProfileKeys.Back,
                equippedBackId);
            PlayerPrefs.SetString(
                LobbyCustomizationProfileKeys.Color,
                $"{bodyColor.r},{bodyColor.g},{bodyColor.b},{bodyColor.a}");
            PlayerPrefs.Save();
        }
    }
}
