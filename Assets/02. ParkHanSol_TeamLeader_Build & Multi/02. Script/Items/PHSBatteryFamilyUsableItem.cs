using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;
using CommonIInteractable = LastJumpCrew.Common.IInteractable;
using CommonIItemHolder = LastJumpCrew.Common.IItemHolder;
using CommonIUsableItem = LastJumpCrew.Common.IUsableItem;

namespace LastJumpCrew.ParkHanSol.Items
{
    public class PHSBatteryFamilyUsableItem :
        MonoBehaviour,
        CommonIUsableItem
    {
        public bool CanUse(
            CommonIItemHolder holder,
            CommonIInteractable target)
        {
            if (holder is not TempPlayerItemHolder phsHolder
                || !holder.HasItem
                || holder.CurrentItem == null
                || phsHolder.CurrentItemPrefabData == null
                || phsHolder.CurrentItemPrefabData.ItemId
                    != holder.CurrentItem.ItemId
                || !phsHolder.CurrentItemPrefabData.TryGetActionProfile(
                    UtilityItemActionKind.PowerRestore,
                    out _)
                || target is not IBatteryUseTarget batteryUseTarget
                || !batteryUseTarget.CanUseBattery(holder))
            {
                return false;
            }

            return true;
        }

        public void Use(
            CommonIItemHolder holder,
            CommonIInteractable target)
        {
            if (!CanUse(holder, target))
            {
                return;
            }

            if (target is not IBatteryUseTarget batteryUseTarget)
            {
                return;
            }

            batteryUseTarget.TryUseBattery(holder);
        }
    }
}
