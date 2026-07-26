using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public sealed class LobbyCustomizationProfileSnapshot
    {
        private readonly HashSet<string> ownedItemIds;

        public LobbyCustomizationProfileSnapshot(
            IEnumerable<string> ownedItemIds,
            string equippedHeadId,
            string equippedBackId,
            Color32 bodyColor,
            int credits)
        {
            this.ownedItemIds = new HashSet<string>(
                ownedItemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            EquippedHeadId = equippedHeadId ?? string.Empty;
            EquippedBackId = equippedBackId ?? string.Empty;
            BodyColor = bodyColor;
            Credits = credits;
        }

        public IReadOnlyCollection<string> OwnedItemIds => ownedItemIds;
        public string EquippedHeadId { get; }
        public string EquippedBackId { get; }
        public Color32 BodyColor { get; }
        public int Credits { get; }

        public bool Owns(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId)
                && ownedItemIds.Contains(itemId);
        }
    }
}
