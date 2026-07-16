using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public abstract class EventRepairUsableItem : UtilityItemUseBehaviour
    {
        private uint requestSequence;

        protected abstract string RequiredItemId { get; }
        protected abstract EventEffectKind RequiredEffectKind { get; }

        protected override bool CanUseItem(IItemHolder user, IInteractable target)
        {
            return user.HasItem
                && user.CurrentItem != null
                && user.CurrentItem.ItemId == RequiredItemId
                && TryGetTarget<IEventRepairTargetHandle>(target, out var repairTarget)
                && repairTarget.EffectKind == RequiredEffectKind
                && repairTarget.RequiredItemId == RequiredItemId
                && repairTarget.CanInteract(user);
        }

        protected override void OnUseFinished(IItemHolder user, IInteractable target)
        {
            if (!TryGetTarget<IEventRepairTargetHandle>(target, out var repairTarget))
            {
                Debug.LogWarning($"PHS_EVENT_REPAIR_USE_FAILED reason=target_missing item={RequiredItemId}", this);
                return;
            }

            var userComponent = user as Component;
            var itemRecord = userComponent == null
                ? null
                : userComponent.GetComponent<NetworkPlayerItemRecord>();
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || itemRecord == null)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_REPAIR_USE_FAILED reason=network_contract_missing item={RequiredItemId}",
                    this);
                return;
            }

            requestSequence++;
            if (requestSequence == 0U)
            {
                requestSequence = 1U;
            }

            if (!coordinator.RequestEffectRepair(repairTarget, itemRecord, requestSequence))
            {
                Debug.LogWarning(
                    $"PHS_EVENT_REPAIR_USE_FAILED reason=request_rejected item={RequiredItemId} event={repairTarget.EventInstanceId} effect={repairTarget.EffectInstanceId}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_EVENT_REPAIR_USE_SENT item={RequiredItemId} event={repairTarget.EventInstanceId} effect={repairTarget.EffectInstanceId} sequence={requestSequence}",
                this);
        }
    }
}
