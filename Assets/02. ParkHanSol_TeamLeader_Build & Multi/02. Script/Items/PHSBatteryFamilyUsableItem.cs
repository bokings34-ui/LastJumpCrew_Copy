using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
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
            if (holder is not Component holderComponent
                || holder is not TempPlayerItemHolder phsHolder
                || !holder.HasItem
                || holder.CurrentItem == null
                || phsHolder.CurrentItemPrefabData == null
                || phsHolder.CurrentItemPrefabData.ItemId
                    != holder.CurrentItem.ItemId
                || !phsHolder.CurrentItemPrefabData.TryGetActionProfile(
                    UtilityItemActionKind.PowerRestore,
                    out _))
            {
                return false;
            }

            return holderComponent.GetComponent<
                       PHSNetworkItemUseActionController>() != null;
        }

        public void Use(
            CommonIItemHolder holder,
            CommonIInteractable target)
        {
            if (!CanUse(holder, target)
                || holder is not Component holderComponent
                || holder is not TempPlayerItemHolder phsHolder)
            {
                return;
            }

            var action = holderComponent.GetComponent<
                PHSNetworkItemUseActionController>();
            if (action == null)
            {
                return;
            }

            if (!action.TryBeginImpactAction(
                    PHSItemUseActionKind.Battery,
                    () =>
                    {
                        holder.Drop();
                    }))
            {
                return;
            }
        }
    }
}
