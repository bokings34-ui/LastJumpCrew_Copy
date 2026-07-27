using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;
using PHSItemHolder = LastJumpCrew.ParkHanSol.Interaction.IItemHolder;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum PHSUtilityFamilyActionKind : byte
    {
        None = 0,
        Wrench = 1,
        FireExtinguisher = 2,
        Battery = 3
    }

    public abstract class PHSUtilityFamilyUsableItem :
        MonoBehaviour,
        IUsableItem
    {
        protected abstract PHSUtilityFamilyActionKind FamilyKind { get; }
        protected abstract PHSItemUseActionKind PresentationKind { get; }

        public bool CanUse(
            LastJumpCrew.Common.IItemHolder holder,
            IInteractable target)
        {
            if (holder is not Component holderComponent
                || holder is not PHSItemHolder phsHolder
                || !holder.HasItem
                || holder.CurrentItem == null
                || phsHolder.CurrentItemPrefabData == null
                || phsHolder.CurrentItemPrefabData.ItemId
                    != holder.CurrentItem.ItemId)
            {
                return false;
            }

            var controller = holderComponent.GetComponent<
                PHSNetworkUtilityFamilyActionController>();
            return controller != null
                && controller.CanRequestAction(
                    FamilyKind,
                    phsHolder.CurrentItemPrefabData)
                && holderComponent.GetComponent<
                    PHSNetworkItemUseActionController>() != null;
        }

        public void Use(
            LastJumpCrew.Common.IItemHolder holder,
            IInteractable target)
        {
            if (!CanUse(holder, target)
                || holder is not Component holderComponent)
            {
                return;
            }

            var controller = holderComponent.GetComponent<
                PHSNetworkUtilityFamilyActionController>();
            var presentation = holderComponent.GetComponent<
                PHSNetworkItemUseActionController>();
            if (controller == null || presentation == null)
            {
                return;
            }

            var isWrench =
                FamilyKind == PHSUtilityFamilyActionKind.Wrench;
            presentation.TryBeginImpactAction(
                PresentationKind,
                () => controller.RequestAction(FamilyKind),
                !isWrench
                    ? 0.16f
                    : 0.08f,
                !isWrench
                    ? 0.2f
                    : 0.35f);
        }
    }

}
