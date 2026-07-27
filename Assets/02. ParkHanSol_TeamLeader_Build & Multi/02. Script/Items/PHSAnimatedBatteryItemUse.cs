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
                && holder.HasItem
                && holderComponent.GetComponent<PHSNetworkItemUseActionController>() != null;
        }

        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder is not Component holderComponent)
            {
                Debug.LogError("PHS_BATTERY_USE_FAILED reason=holder_component_missing", this);
                return;
            }

            var action = holderComponent.GetComponent<PHSNetworkItemUseActionController>();
            if (action == null)
            {
                Debug.LogError(
                    $"PHS_BATTERY_USE_FAILED reason=action_contract_missing holder={holderComponent.name}",
                    this);
                return;
            }

            action.TryBeginImpactAction(
                PHSItemUseActionKind.Battery,
                () =>
                {
                    holder.Drop();
                    Debug.Log("PHS_BATTERY_PLACED_FROM_USE", this);
                });
        }
    }
}
