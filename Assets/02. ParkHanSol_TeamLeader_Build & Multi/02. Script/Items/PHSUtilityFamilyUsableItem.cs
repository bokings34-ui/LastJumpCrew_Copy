using System;
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
                    PHSNetworkItemUseActionController>() != null
                && holderComponent.GetComponent<
                    PHSNetworkItemUseFeedbackController>() != null;
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
            var feedback = holderComponent.GetComponent<
                PHSNetworkItemUseFeedbackController>();
            if (controller == null || presentation == null || feedback == null)
            {
                return;
            }

            var isWrench =
                FamilyKind == PHSUtilityFamilyActionKind.Wrench;
            feedback.RequestOwnerFeedback(
                isWrench
                    ? PHSItemUseFeedbackKind.Wrench
                    : PHSItemUseFeedbackKind.FireExtinguisher,
                isWrench
                    ? PHSItemUseFeedbackShape.Sphere
                    : PHSItemUseFeedbackShape.Cast,
                holderComponent.transform.position + Vector3.up * 0.75f,
                holderComponent.transform.forward,
                isWrench ? 1.1f : 0.45f,
                isWrench ? 0f : 4f,
                Array.Empty<Vector3>());

            presentation.TryBeginImpactAction(
                PresentationKind,
                () => controller.RequestAction(FamilyKind),
                !isWrench
                    ? 0.24f
                    : 0.32f,
                !isWrench
                    ? 0.2f
                    : 0.52f);
        }
    }

}
