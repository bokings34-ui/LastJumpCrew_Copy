using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyLocalCustomizationService :
        MonoBehaviour,
        ILobbyCustomizationService
    {
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField, Min(0)] private int startingCredits = 300;
        [SerializeField, Min(1)] private int maximumCredits = 999999;

        private ILobbyCustomizationProfileStore profileStore;
        private HashSet<string> ownedItemIds =
            new(StringComparer.Ordinal);
        private bool isProfileReady;
        private string profileFailureReason = string.Empty;
        private int currentCredits;
        private string equippedHeadId = string.Empty;
        private string equippedBackId = string.Empty;
        private Color32 bodyColor = new(255, 255, 255, 255);
        private string previewHeadId = string.Empty;
        private string previewBackId = string.Empty;
        private Color32 previewBodyColor = new(255, 255, 255, 255);

        public CosmeticCatalog Catalog => catalog;
        public int CurrentCredits => currentCredits;
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
            profileStore = new PlayerPrefsLobbyCustomizationProfileStore(
                catalog,
                startingCredits,
                maximumCredits);
            if (!profileStore.TryLoad(out var snapshot, out var reason))
            {
                profileFailureReason = reason;
                Debug.LogError(
                    $"PHS_LOCAL_COSMETIC_PROFILE_LOAD_FAILED reason={reason}",
                    this);
                return;
            }

            ApplySnapshot(snapshot);
            isProfileReady = true;
            profileFailureReason = string.Empty;
            ResetPreviewToEquipped(false);
        }

        public bool OwnsItem(string itemId)
        {
            return isProfileReady
                && !string.IsNullOrWhiteSpace(itemId)
                && ownedItemIds.Contains(itemId);
        }

        public bool TrySelectPreviewItem(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason)
                || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }

            if (item.Slot == CosmeticSlot.Head)
            {
                previewHeadId = item.ItemId;
            }
            else if (item.Slot == CosmeticSlot.Back)
            {
                previewBackId = item.ItemId;
            }
            else
            {
                reason = $"slot_invalid:{item.Slot}";
                return false;
            }

            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TrySelectPreviewBodyColor(
            Color32 color,
            out string reason)
        {
            if (!CanUseProfile(out reason))
            {
                return false;
            }

            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            previewBodyColor = color;
            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TryResetPreview(out string reason)
        {
            if (!CanUseProfile(out reason))
            {
                return false;
            }

            ResetPreviewToEquipped(true);
            reason = null;
            return true;
        }

        public bool TryRequestPurchase(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason)
                || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }

            if (ownedItemIds.Contains(item.ItemId))
            {
                reason = $"already_owned:{item.ItemId}";
                return false;
            }

            if (currentCredits < item.Price)
            {
                reason =
                    $"insufficient_credits:{currentCredits}:{item.Price}";
                return false;
            }

            var nextOwnedItemIds = new HashSet<string>(
                ownedItemIds,
                StringComparer.Ordinal)
            {
                item.ItemId
            };
            var snapshot = CreateSnapshot(
                nextOwnedItemIds,
                equippedHeadId,
                equippedBackId,
                bodyColor,
                currentCredits - item.Price);
            if (!TryPersist(snapshot, out reason))
            {
                return false;
            }

            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestEquip(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason)
                || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }

            if (!ownedItemIds.Contains(item.ItemId))
            {
                reason = $"item_not_owned:{item.ItemId}";
                return false;
            }

            var nextHeadId = item.Slot == CosmeticSlot.Head
                ? item.ItemId
                : equippedHeadId;
            var nextBackId = item.Slot == CosmeticSlot.Back
                ? item.ItemId
                : equippedBackId;
            if (item.Slot != CosmeticSlot.Head
                && item.Slot != CosmeticSlot.Back)
            {
                reason = $"slot_invalid:{item.Slot}";
                return false;
            }

            var snapshot = CreateSnapshot(
                ownedItemIds,
                nextHeadId,
                nextBackId,
                bodyColor,
                currentCredits);
            if (!TryPersist(snapshot, out reason))
            {
                return false;
            }

            ResetPreviewToEquipped(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestUnequip(CosmeticSlot slot, out string reason)
        {
            if (!CanUseProfile(out reason))
            {
                return false;
            }

            if (slot != CosmeticSlot.Head && slot != CosmeticSlot.Back)
            {
                reason = $"slot_invalid:{slot}";
                return false;
            }

            var snapshot = CreateSnapshot(
                ownedItemIds,
                slot == CosmeticSlot.Head ? string.Empty : equippedHeadId,
                slot == CosmeticSlot.Back ? string.Empty : equippedBackId,
                bodyColor,
                currentCredits);
            if (!TryPersist(snapshot, out reason))
            {
                return false;
            }

            ResetPreviewToEquipped(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestSetBodyColor(
            Color32 color,
            out string reason)
        {
            if (!CanUseProfile(out reason))
            {
                return false;
            }

            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            var snapshot = CreateSnapshot(
                ownedItemIds,
                equippedHeadId,
                equippedBackId,
                color,
                currentCredits);
            if (!TryPersist(snapshot, out reason))
            {
                return false;
            }

            ResetPreviewToEquipped(true);
            StateChanged?.Invoke();
            return true;
        }

        private bool CanUseProfile(out string reason)
        {
            if (isProfileReady)
            {
                reason = null;
                return true;
            }

            reason = string.IsNullOrWhiteSpace(profileFailureReason)
                ? "profile_not_ready"
                : $"profile_failed:{profileFailureReason}";
            return false;
        }

        private LobbyCustomizationProfileSnapshot CreateSnapshot(
            IEnumerable<string> nextOwnedItemIds,
            string nextHeadId,
            string nextBackId,
            Color32 nextBodyColor,
            int nextCredits)
        {
            return new LobbyCustomizationProfileSnapshot(
                nextOwnedItemIds,
                nextHeadId,
                nextBackId,
                nextBodyColor,
                nextCredits);
        }

        private bool TryPersist(
            LobbyCustomizationProfileSnapshot snapshot,
            out string reason)
        {
            if (!profileStore.TrySave(snapshot, out reason))
            {
                Debug.LogError(
                    $"PHS_LOCAL_COSMETIC_PROFILE_SAVE_FAILED reason={reason}",
                    this);
                return false;
            }

            ApplySnapshot(snapshot);
            return true;
        }

        private void ApplySnapshot(
            LobbyCustomizationProfileSnapshot snapshot)
        {
            ownedItemIds = new HashSet<string>(
                snapshot.OwnedItemIds,
                StringComparer.Ordinal);
            equippedHeadId = snapshot.EquippedHeadId;
            equippedBackId = snapshot.EquippedBackId;
            bodyColor = snapshot.BodyColor;
            currentCredits = snapshot.Credits;
        }

        private void ResetPreviewToEquipped(bool notify)
        {
            previewHeadId = equippedHeadId;
            previewBackId = equippedBackId;
            previewBodyColor = bodyColor;
            if (notify)
            {
                PreviewChanged?.Invoke();
            }
        }
    }
}
