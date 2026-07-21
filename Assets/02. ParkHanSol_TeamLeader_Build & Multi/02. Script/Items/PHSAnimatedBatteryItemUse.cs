using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class PHSAnimatedBatteryItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            return holder is Component holderComponent
                && holderComponent.GetComponent<NetworkPlayerCombatController>() != null
                && holderComponent.GetComponent<PHSNetworkItemUseActionController>() != null;
        }

        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder is not Component holderComponent)
            {
                Debug.LogError("PHS_BATTERY_USE_FAILED reason=holder_component_missing", this);
                return;
            }

            var combat = holderComponent.GetComponent<NetworkPlayerCombatController>();
            var action = holderComponent.GetComponent<PHSNetworkItemUseActionController>();
            if (combat == null || action == null)
            {
                Debug.LogError(
                    $"PHS_BATTERY_USE_FAILED reason=action_contract_missing holder={holderComponent.name}",
                    this);
                return;
            }

            if (TryGetPowerFailureAnchor(target, out var powerFailureAnchor))
            {
                action.TryBeginImpactAction(
                    PHSItemUseActionKind.Battery,
                    () => TryRepairPowerFailure(
                        holder,
                        holderComponent,
                        powerFailureAnchor));
                return;
            }

            action.TryBeginImpactAction(
                PHSItemUseActionKind.Battery,
                combat.RequestBatteryThrow);
        }

        private void TryRepairPowerFailure(
            IItemHolder holder,
            Component holderComponent,
            PHSShipAccidentAnchor anchor)
        {
            var feedback =
                holderComponent.GetComponent<PHSNetworkItemUseFeedbackController>();
            if (feedback == null)
            {
                Debug.LogError(
                    $"PHS_BATTERY_REPAIR_FAILED reason=feedback_controller_missing holder={holderComponent.name}",
                    this);
                return;
            }

            if (!anchor.RequestRepair(holder))
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_REPAIR_FAILED reason=request_rejected anchor={anchor.name}",
                    this);
                return;
            }

            var origin = holderComponent.transform.position;
            var targetPosition = anchor.RepairPosition;
            var direction = targetPosition - origin;
            feedback.RequestOwnerFeedback(
                PHSItemUseFeedbackShape.Cast,
                origin,
                direction,
                0.12f,
                Mathf.Max(0.1f, direction.magnitude),
                new[] { targetPosition });

            Debug.Log(
                $"PHS_BATTERY_REPAIR_SENT holder={holderComponent.name} anchor={anchor.name}",
                this);
        }

        private static bool TryGetPowerFailureAnchor(
            IInteractable target,
            out PHSShipAccidentAnchor anchor)
        {
            anchor = target as PHSShipAccidentAnchor;
            if (anchor == null && target is Component targetComponent)
            {
                anchor = targetComponent.GetComponentInParent<PHSShipAccidentAnchor>();
            }

            return anchor != null
                && anchor.AccidentId == PHSShipAccidentId.PowerFailure;
        }
    }
}
