using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
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

            action.TryBeginImpactAction(
                PHSItemUseActionKind.Battery,
                combat.RequestBatteryThrow);
        }
    }
}
