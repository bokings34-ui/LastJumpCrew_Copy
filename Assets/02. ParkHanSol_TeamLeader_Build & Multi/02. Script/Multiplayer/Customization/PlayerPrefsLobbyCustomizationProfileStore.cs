using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public sealed class PlayerPrefsLobbyCustomizationProfileStore :
        ILobbyCustomizationProfileStore
    {
        private const string OwnedItemsPreferenceKey =
            "PHS_CosmeticOwnedItems_v1";
        private const string HeadPreferenceKey = "PHS_CosmeticHead_v1";
        private const string BackPreferenceKey = "PHS_CosmeticBack_v1";
        private const string ColorPreferenceKey = "PHS_CosmeticColor_v1";
        private const string CreditsPreferenceKey =
            "PHS_PersonalLobbyCustomizationCredits_v1";

        private static readonly Color32 DefaultBodyColor =
            new(255, 255, 255, 255);

        private readonly CosmeticCatalog catalog;
        private readonly int startingCredits;
        private readonly int maximumCredits;

        public PlayerPrefsLobbyCustomizationProfileStore(
            CosmeticCatalog catalog,
            int startingCredits,
            int maximumCredits)
        {
            this.catalog = catalog;
            this.startingCredits = startingCredits;
            this.maximumCredits = maximumCredits;
        }

        public bool TryLoad(
            out LobbyCustomizationProfileSnapshot snapshot,
            out string reason)
        {
            snapshot = null;
            if (!ValidateConfiguration(out reason)
                || !TryLoadOwnedItems(out var ownedItemIds, out reason)
                || !TryLoadColor(out var bodyColor, out reason)
                || !TryLoadCredits(out var credits, out reason))
            {
                return false;
            }

            var headId = PlayerPrefs.GetString(
                HeadPreferenceKey,
                string.Empty);
            var backId = PlayerPrefs.GetString(
                BackPreferenceKey,
                string.Empty);
            if (!TryValidateEquipment(
                    headId,
                    CosmeticSlot.Head,
                    ownedItemIds,
                    out reason)
                || !TryValidateEquipment(
                    backId,
                    CosmeticSlot.Back,
                    ownedItemIds,
                    out reason))
            {
                return false;
            }

            snapshot = new LobbyCustomizationProfileSnapshot(
                ownedItemIds,
                headId,
                backId,
                bodyColor,
                credits);
            reason = null;
            return true;
        }

        public bool TrySave(
            LobbyCustomizationProfileSnapshot snapshot,
            out string reason)
        {
            if (!ValidateConfiguration(out reason)
                || snapshot == null)
            {
                reason ??= "snapshot_missing";
                return false;
            }

            var ownedItemIds = new HashSet<string>(
                snapshot.OwnedItemIds,
                StringComparer.Ordinal);
            foreach (var itemId in ownedItemIds)
            {
                if (!catalog.TryGetItem(itemId, out _))
                {
                    reason = $"owned_item_invalid:{itemId}";
                    return false;
                }
            }

            if (!TryValidateEquipment(
                    snapshot.EquippedHeadId,
                    CosmeticSlot.Head,
                    ownedItemIds,
                    out reason)
                || !TryValidateEquipment(
                    snapshot.EquippedBackId,
                    CosmeticSlot.Back,
                    ownedItemIds,
                    out reason))
            {
                return false;
            }

            if (!catalog.IsBodyColorAllowed(snapshot.BodyColor))
            {
                reason = $"color_not_allowed:{snapshot.BodyColor}";
                return false;
            }

            if (snapshot.Credits < 0
                || snapshot.Credits > maximumCredits)
            {
                reason = $"credits_out_of_range:{snapshot.Credits}";
                return false;
            }

            PlayerPrefs.SetString(
                OwnedItemsPreferenceKey,
                string.Join(',', ownedItemIds));
            PlayerPrefs.SetString(
                HeadPreferenceKey,
                snapshot.EquippedHeadId);
            PlayerPrefs.SetString(
                BackPreferenceKey,
                snapshot.EquippedBackId);
            var color = snapshot.BodyColor;
            PlayerPrefs.SetString(
                ColorPreferenceKey,
                $"{color.r},{color.g},{color.b},{color.a}");
            PlayerPrefs.SetInt(CreditsPreferenceKey, snapshot.Credits);
            PlayerPrefs.Save();
            reason = null;
            return true;
        }

        private bool ValidateConfiguration(out string reason)
        {
            if (catalog == null)
            {
                reason = "catalog_missing";
                return false;
            }

            if (startingCredits < 0
                || startingCredits > maximumCredits)
            {
                reason =
                    $"starting_credits_out_of_range:{startingCredits}:{maximumCredits}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryLoadOwnedItems(
            out HashSet<string> ownedItemIds,
            out string reason)
        {
            ownedItemIds = new HashSet<string>(StringComparer.Ordinal);
            var serialized = PlayerPrefs.GetString(
                OwnedItemsPreferenceKey,
                string.Empty);
            foreach (var itemId in serialized.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ownedItemIds.Add(itemId))
                {
                    reason = $"owned_item_duplicate:{itemId}";
                    return false;
                }

                if (!catalog.TryGetItem(itemId, out _))
                {
                    reason = $"owned_item_invalid:{itemId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool TryLoadColor(
            out Color32 color,
            out string reason)
        {
            color = DefaultBodyColor;
            if (!PlayerPrefs.HasKey(ColorPreferenceKey))
            {
                if (!catalog.IsBodyColorAllowed(color))
                {
                    reason = "default_color_not_allowed";
                    return false;
                }

                reason = null;
                return true;
            }

            var parts = PlayerPrefs.GetString(ColorPreferenceKey).Split(',');
            if (parts.Length != 4
                || !byte.TryParse(parts[0], out var red)
                || !byte.TryParse(parts[1], out var green)
                || !byte.TryParse(parts[2], out var blue)
                || !byte.TryParse(parts[3], out var alpha))
            {
                reason = "saved_color_invalid";
                return false;
            }

            color = new Color32(red, green, blue, alpha);
            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"saved_color_not_allowed:{color}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryLoadCredits(out int credits, out string reason)
        {
            credits = PlayerPrefs.HasKey(CreditsPreferenceKey)
                ? PlayerPrefs.GetInt(CreditsPreferenceKey)
                : startingCredits;
            if (credits < 0 || credits > maximumCredits)
            {
                reason = $"saved_credits_out_of_range:{credits}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryValidateEquipment(
            string itemId,
            CosmeticSlot expectedSlot,
            ISet<string> ownedItemIds,
            out string reason)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                reason = null;
                return true;
            }

            if (!ownedItemIds.Contains(itemId))
            {
                reason = $"equipped_item_not_owned:{itemId}";
                return false;
            }

            if (!catalog.TryGetItem(itemId, out var item)
                || item.Slot != expectedSlot)
            {
                reason = $"equipped_item_slot_invalid:{itemId}:{expectedSlot}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
