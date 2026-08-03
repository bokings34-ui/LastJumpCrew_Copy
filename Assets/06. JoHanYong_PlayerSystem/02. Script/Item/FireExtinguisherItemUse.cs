using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class FireExtinguisherItemUse : MonoBehaviour, IUsableItem, IContinuousUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target) //현재 소화기를  사용할 수 있는 검사
        {
            Debug.Log($"PHS_EXTINGUISHER_USE " + $"frame={Time.frameCount} " + $"time={Time.time:F2}");
            if (holder == null)
            {
                return false;
            }
            if(holder is not Component holderComponent)
            {
                Debug.LogWarning("PHS_EXTINGUISHER_USE_FAILED " + "reason=holder_component_missing");
                return false;
            }
            return holderComponent.GetComponent<NetworkPlayerCombatController>() != null;
        }
        public void Use(IItemHolder holder, IInteractable target) //좌클릭 누르는 동안 호출
        {
            Debug.Log("소화기 발사 !!");
            if(holder is not Component holderComponent)
            {
                Debug.LogWarning("PHS_EXTINGUISHER_USE_FAILED " + "reason=holder_component_missing");
                return; 
            }
            var combatController = holderComponent.GetComponent<NetworkPlayerCombatController>();
            Debug.Log("소화기 중 !!");

            if (combatController == null)
            {
                Debug.LogWarning($"PHS_EXTINGUISHER_USE_FAILED " + $"reason=combat_controller_missing " + $"holder={holderComponent.name}");

                return;
            }
            combatController.RequestExtinguisherSpray();
        }
    }


}
