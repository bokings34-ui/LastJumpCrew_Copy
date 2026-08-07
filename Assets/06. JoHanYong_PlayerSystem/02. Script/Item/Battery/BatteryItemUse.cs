using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class BatteryItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            return holder is Component holderComponent
                && holder.HasItem
                && holderComponent.GetComponent<NetworkPlayerCombatController>() != null;
        }

        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder == null || !holder.HasItem)
            {
                Debug.LogError("PHS_BATTERY_USE_FAILED reason=held_item_missing");
                return;
            }

            if (holder is not Component holderComponent)
            {
                Debug.LogError("PHS_BATTERY_ATTACK_FAILED reason=holder_component_missing");
                return;
            }

            var combatController =
                holderComponent.GetComponent<NetworkPlayerCombatController>();
            if (combatController == null)
            {
                Debug.LogError(
                    $"PHS_BATTERY_ATTACK_FAILED reason=combat_controller_missing holder={holderComponent.name}");
                return;
            }

            combatController.RequestBatteryThrow();
        }
    }
}
