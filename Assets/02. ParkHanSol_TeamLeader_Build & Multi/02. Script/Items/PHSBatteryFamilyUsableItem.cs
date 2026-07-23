using System;
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
        private const float TelegraphRadius = 0.9f;
        private const float ImpactPulseRadius = 0.18f;

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
                || phsHolder.CurrentItemPrefabData.UtilityFamily
                    != PHSUtilityFamilyActionKind.Battery
                || !phsHolder.CurrentItemPrefabData.TryGetActionProfile(
                    UtilityItemActionKind.PowerRestore,
                    out _))
            {
                return false;
            }

            return holderComponent.GetComponent<
                       PHSNetworkItemUseActionController>() != null
                && holderComponent.GetComponent<
                       PHSNetworkItemUseFeedbackController>() != null;
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
            var feedback = holderComponent.GetComponent<
                PHSNetworkItemUseFeedbackController>();
            if (action == null || feedback == null)
            {
                return;
            }

            if (!action.TryBeginImpactAction(
                    PHSItemUseActionKind.Battery,
                    () =>
                    {
                        var dropPosition = phsHolder.DropPosition;
                        feedback.RequestOwnerFeedback(
                            PHSItemUseFeedbackKind.Battery,
                            PHSItemUseFeedbackShape.Sphere,
                            dropPosition,
                            holderComponent.transform.forward,
                            ImpactPulseRadius,
                            0f,
                            new[] { dropPosition });
                        holder.Drop();
                    }))
            {
                return;
            }

            feedback.RequestOwnerFeedback(
                PHSItemUseFeedbackKind.Battery,
                PHSItemUseFeedbackShape.Sphere,
                holderComponent.transform.position,
                holderComponent.transform.forward,
                TelegraphRadius,
                0f,
                Array.Empty<Vector3>());
        }
    }
}
