using LastJumpCrew.Common;
using UnityEngine;
using IBatteryUseTarget =
    LastJumpCrew.ParkHanSol.Interaction.IBatteryUseTarget;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class BatteryItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            return holder != null
                && holder.HasItem
                && TryResolveBatteryTarget(target, out var batteryTarget)
                && batteryTarget.CanUseBattery(holder);
        }

        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder == null || !holder.HasItem)
            {
                Debug.LogError("PHS_BATTERY_USE_FAILED reason=held_item_missing");
                return;
            }

            if (!TryResolveBatteryTarget(target, out var batteryTarget)
                || !batteryTarget.TryUseBattery(holder))
            {
                Debug.LogWarning("PHS_BATTERY_USE_FAILED reason=target_rejected");
                return;
            }

            Debug.Log("PHS_BATTERY_USE_SUCCEEDED");
        }

        private static bool TryResolveBatteryTarget(
            IInteractable target,
            out IBatteryUseTarget batteryTarget)
        {
            batteryTarget = target as IBatteryUseTarget;
            if (batteryTarget != null)
            {
                return true;
            }

            if (target is not Component targetComponent)
            {
                return false;
            }

            batteryTarget = targetComponent.GetComponentInParent<IBatteryUseTarget>();
            return batteryTarget != null;
        }
    }
}
