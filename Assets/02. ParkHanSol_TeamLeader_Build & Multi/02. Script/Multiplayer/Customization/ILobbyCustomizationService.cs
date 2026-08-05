using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public interface ILobbyCustomizationService
    {
        CosmeticCatalog Catalog { get; }
        int CurrentCredits { get; }
        bool IsProfileReady { get; }
        string ProfileFailureReason { get; }
        string EquippedHeadId { get; }
        string EquippedBackId { get; }
        string EquippedPetId { get; }
        string EquippedFrontId { get; }
        Color32 BodyColor { get; }
        string PreviewHeadId { get; }
        string PreviewBackId { get; }
        string PreviewPetId { get; }
        string PreviewFrontId { get; }
        Color32 PreviewBodyColor { get; }

        event Action StateChanged;
        event Action PreviewChanged;

        bool OwnsItem(string itemId);
        bool TrySelectPreviewItem(string itemId, out string reason);
        bool TrySelectPreviewBodyColor(Color32 color, out string reason);
        bool TryResetPreview(out string reason);
        bool TryRequestPurchase(string itemId, out string reason);
        bool TryRequestEquip(string itemId, out string reason);
        bool TryRequestUnequip(CosmeticSlot slot, out string reason);
        bool TryRequestSetBodyColor(Color32 color, out string reason);
    }
}
