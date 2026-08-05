using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyLocalCustomizationService : MonoBehaviour, ILobbyCustomizationService
    {
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField, Min(0)] private int startingCredits = 300;
        [SerializeField, Min(1)] private int maximumCredits = 999999;

        private ILobbyCustomizationProfileStore profileStore;
        private HashSet<string> ownedItemIds = new(StringComparer.Ordinal);
        private bool isProfileReady;
        private string profileFailureReason = string.Empty;
        private int currentCredits;
        private string equippedHeadId = string.Empty;
        private string equippedBackId = string.Empty;
        private string equippedPetId = string.Empty;
        private string equippedFrontId = string.Empty;
        private Color32 bodyColor = new(255, 255, 255, 255);
        private string previewHeadId = string.Empty;
        private string previewBackId = string.Empty;
        private string previewPetId = string.Empty;
        private string previewFrontId = string.Empty;
        private Color32 previewBodyColor = new(255, 255, 255, 255);

        public CosmeticCatalog Catalog => catalog;
        public int CurrentCredits => currentCredits;
        public bool IsProfileReady => isProfileReady;
        public string ProfileFailureReason => profileFailureReason;
        public string EquippedHeadId => equippedHeadId;
        public string EquippedBackId => equippedBackId;
        public string EquippedPetId => equippedPetId;
        public string EquippedFrontId => equippedFrontId;
        public Color32 BodyColor => bodyColor;
        public string PreviewHeadId => previewHeadId;
        public string PreviewBackId => previewBackId;
        public string PreviewPetId => previewPetId;
        public string PreviewFrontId => previewFrontId;
        public Color32 PreviewBodyColor => previewBodyColor;
        public event Action StateChanged;
        public event Action PreviewChanged;

        private void Awake()
        {
            profileStore = new PlayerPrefsLobbyCustomizationProfileStore(catalog, startingCredits, maximumCredits);
            if (!profileStore.TryLoad(out var snapshot, out var reason))
            {
                profileFailureReason = reason;
                Debug.LogError($"PHS_LOCAL_COSMETIC_PROFILE_LOAD_FAILED reason={reason}", this);
                return;
            }
            ApplySnapshot(snapshot);
            isProfileReady = true;
            ResetPreviewToEquipped(false);
        }

        public bool OwnsItem(string itemId) => isProfileReady && !string.IsNullOrWhiteSpace(itemId) && ownedItemIds.Contains(itemId);

        public bool TrySelectPreviewItem(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }
            SetPreview(item.Slot, item.ItemId);
            reason = null;
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
            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TryResetPreview(out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            ResetPreviewToEquipped(true);
            reason = null;
            return true;
        }

        public bool TryRequestPurchase(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
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
                reason = $"insufficient_credits:{currentCredits}:{item.Price}";
                return false;
            }
            var nextOwned = new HashSet<string>(ownedItemIds, StringComparer.Ordinal) { item.ItemId };
            if (!TryPersist(CreateSnapshot(nextOwned, currentCredits - item.Price), out reason)) return false;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestEquip(string itemId, out string reason)
        {
            if (!CanUseProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }
            if (!ownedItemIds.Contains(item.ItemId))
            {
                reason = $"item_not_owned:{item.ItemId}";
                return false;
            }

            var head = equippedHeadId;
            var back = equippedBackId;
            var pet = equippedPetId;
            var front = equippedFrontId;
            var current = GetEquipped(item.Slot);
            SetSlot(ref head, ref back, ref pet, ref front, item.Slot, current == item.ItemId ? string.Empty : item.ItemId);
            if (!TryPersist(CreateSnapshot(ownedItemIds, currentCredits, head, back, pet, front), out reason)) return false;
            ResetPreviewToEquipped(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRequestUnequip(CosmeticSlot slot, out string reason)
        {
            if (!CanUseProfile(out reason)) return false;
            var head = equippedHeadId;
            var back = equippedBackId;
            var pet = equippedPetId;
            var front = equippedFrontId;
            SetSlot(ref head, ref back, ref pet, ref front, slot, string.Empty);
            if (!TryPersist(CreateSnapshot(ownedItemIds, currentCredits, head, back, pet, front), out reason)) return false;
            ResetPreviewToEquipped(true);
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
            if (!TryPersist(CreateSnapshot(ownedItemIds, currentCredits, bodyColor: color), out reason)) return false;
            ResetPreviewToEquipped(true);
            StateChanged?.Invoke();
            return true;
        }

        private bool CanUseProfile(out string reason)
        {
            reason = isProfileReady ? null : string.IsNullOrWhiteSpace(profileFailureReason) ? "profile_not_ready" : $"profile_failed:{profileFailureReason}";
            return reason == null;
        }

        private LobbyCustomizationProfileSnapshot CreateSnapshot(IEnumerable<string> owned, int credits, string head = null, string back = null, string pet = null, string front = null, Color32? bodyColor = null)
        {
            return new LobbyCustomizationProfileSnapshot(owned, head ?? equippedHeadId, back ?? equippedBackId, pet ?? equippedPetId, front ?? equippedFrontId, bodyColor ?? this.bodyColor, credits);
        }

        private bool TryPersist(LobbyCustomizationProfileSnapshot snapshot, out string reason)
        {
            if (!profileStore.TrySave(snapshot, out reason))
            {
                Debug.LogError($"PHS_LOCAL_COSMETIC_PROFILE_SAVE_FAILED reason={reason}", this);
                return false;
            }
            ApplySnapshot(snapshot);
            return true;
        }

        private void ApplySnapshot(LobbyCustomizationProfileSnapshot snapshot)
        {
            ownedItemIds = new HashSet<string>(snapshot.OwnedItemIds, StringComparer.Ordinal);
            equippedHeadId = snapshot.EquippedHeadId;
            equippedBackId = snapshot.EquippedBackId;
            equippedPetId = snapshot.EquippedPetId;
            equippedFrontId = snapshot.EquippedFrontId;
            bodyColor = snapshot.BodyColor;
            currentCredits = snapshot.Credits;
        }

        private string GetEquipped(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Head => equippedHeadId,
            CosmeticSlot.Back => equippedBackId,
            CosmeticSlot.Pet => equippedPetId,
            CosmeticSlot.Front => equippedFrontId,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
        };

        private void SetPreview(CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Head: previewHeadId = itemId; break;
                case CosmeticSlot.Back: previewBackId = itemId; break;
                case CosmeticSlot.Pet: previewPetId = itemId; break;
                case CosmeticSlot.Front: previewFrontId = itemId; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private static void SetSlot(ref string head, ref string back, ref string pet, ref string front, CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Head: head = itemId; break;
                case CosmeticSlot.Back: back = itemId; break;
                case CosmeticSlot.Pet: pet = itemId; break;
                case CosmeticSlot.Front: front = itemId; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private void ResetPreviewToEquipped(bool notify)
        {
            previewHeadId = equippedHeadId;
            previewBackId = equippedBackId;
            previewPetId = equippedPetId;
            previewFrontId = equippedFrontId;
            previewBodyColor = bodyColor;
            if (notify) PreviewChanged?.Invoke();
        }
    }
}
