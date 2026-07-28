using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using UnityEngine;
using PHSItemHolder = LastJumpCrew.ParkHanSol.Interaction.IItemHolder;

namespace LastJumpCrew.ParkHanSol.Items
{
    public abstract class ProfiledRepairUsableItem : UtilityItemUseBehaviour
    {
        private uint requestSequence;

        protected abstract string ExpectedItemId { get; }

        protected abstract bool SupportsAction(
            UtilityItemActionKind actionKind);

        protected override bool CanUseItem(
            IItemHolder user,
            IInteractable target)
        {
            if (user == null
                || !user.HasItem
                || user.CurrentItem == null
                || user.CurrentItem.ItemId != ExpectedItemId
                || user is not PHSItemHolder phsItemHolder
                || phsItemHolder.CurrentItemPrefabData == null
                || phsItemHolder.CurrentItemPrefabData.ItemId
                    != user.CurrentItem.ItemId
                || !TryResolveTargetAction(target, out var actionKind)
                || !SupportsAction(actionKind)
                || !phsItemHolder.CurrentItemPrefabData.TryGetActionProfile(
                    actionKind,
                    out _))
            {
                return false;
            }

            return target.CanInteract(user);
        }

        protected override void OnUseFinished(
            IItemHolder user,
            IInteractable target)
        {
            if (TryGetTarget<PHSShipAccidentAnchor>(target, out var anchor))
            {
                if (!anchor.RequestRepair(user))
                {
                    Debug.LogWarning(
                        $"PHS_PROFILED_REPAIR_FAILED reason=ship_request_rejected item={ExpectedItemId}",
                        this);
                }

                return;
            }

            if (!TryGetTarget<IEventRepairTargetHandle>(target, out var eventTarget))
            {
                Debug.LogWarning(
                    $"PHS_PROFILED_REPAIR_FAILED reason=target_missing item={ExpectedItemId}",
                    this);
                return;
            }

            var userComponent = user as Component;
            var itemRecord = userComponent == null
                ? null
                : userComponent.GetComponent<NetworkPlayerItemRecord>();
            var coordinator = NetworkEventCoordinator.Instance;
            if (itemRecord == null || coordinator == null)
            {
                Debug.LogWarning(
                    $"PHS_PROFILED_REPAIR_FAILED reason=network_contract_missing item={ExpectedItemId}",
                    this);
                return;
            }

            requestSequence++;
            if (requestSequence == 0U)
            {
                requestSequence = 1U;
            }

            if (!coordinator.RequestEffectRepair(
                    eventTarget,
                    itemRecord,
                    requestSequence))
            {
                Debug.LogWarning(
                    $"PHS_PROFILED_REPAIR_FAILED reason=event_request_rejected item={ExpectedItemId}",
                    this);
            }
        }

        private bool TryResolveTargetAction(
            IInteractable target,
            out UtilityItemActionKind actionKind)
        {
            if (TryGetTarget(
                    target,
                    out IShipAccidentRepairTarget shipTarget))
            {
                return UtilityItemRepairActionResolver.TryResolve(
                    shipTarget.AccidentId,
                    out actionKind);
            }

            if (TryGetTarget(
                    target,
                    out IEventRepairTargetHandle eventTarget))
            {
                return UtilityItemRepairActionResolver.TryResolve(
                    eventTarget.EffectKind,
                    out actionKind);
            }

            actionKind = UtilityItemActionKind.None;
            return false;
        }
    }
}
