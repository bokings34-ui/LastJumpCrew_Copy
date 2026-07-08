using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface IItemHolder
    {
        UtilityItemPrefabData CurrentItemPrefabData { get; }
        bool CanReplaceHeldItem(UtilityItemPrefabData itemPrefabData);
        void ReplaceHeldItem(UtilityItemPrefabData itemPrefabData, Transform interactionSource);
        void PlaceHeldItem();
        bool TryConsumeHeldItem(string itemId);
    }
}
