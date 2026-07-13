using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Item
{
    //렌치 아이템의 좌클릭 사용 동작을 담당
    
    public sealed class WrenchItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            if (holder == null)
            {
                return false;
            }
            if(holder is not Component holderComponent)
            {
                return false;
            }
            return holderComponent.GetComponent<NetworkPlayerCombatController>() != null;
        }
        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder is not Component holderComponent)
            {
                Debug.LogWarning($"PHS_WRENCH_USE_FAILED" + $"reason=holder_component_missing");

                return;
            }
            var combatController = holderComponent.GetComponent<NetworkPlayerCombatController>();

            if(combatController == null)
            {
                Debug.LogWarning($"PHS_WRENCH_USE_FAILED" + $"reason = combat_controller_missing" + $"holder={holderComponent.name}");

                return;
            }

            combatController.RequestWrenchAttack();
        }
    }
}
