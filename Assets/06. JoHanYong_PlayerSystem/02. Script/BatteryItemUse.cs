using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class BatteryItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            if(holder is not Component holderComponent)
            {
                return false;   
            }
            return holderComponent.GetComponent<NetworkPlayerCombatController>() != null; 
        }
        public void Use(IItemHolder holder, IInteractable target) //배터리 들고 좌클릭 시 호출
        {

            if(holder is not Component holderComponent)
            {
                Debug.LogWarning("PHS_BATTERY_USE_FAILED" + $"reason = holder_component_missing");

                return; 
            }
            var combatController = holderComponent.GetComponent<NetworkPlayerCombatController>();

            if(combatController == null)
            {
                Debug.LogWarning($"PHS_BATTERY_USE_FAILED " + $"reason=combat_controller_missing " + $"holder={holderComponent.name}");
                return;
            }
            Debug.Log($"PHS_BATTERY_THROW_REQUESTED " + $"player={holderComponent.name}");

            combatController.RequestBatteryThrow();
        }
    }
}
