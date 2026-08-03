using UnityEngine;
using LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Items
{
    public static class RepairResolver
    {
        public static bool TryRepair(UtilityItemDataSO itemData, GameObject target, GameObject repairer)
        {
            if (itemData == null)//아이템 데이터없으면 수리 불가능
            {
                Debug.LogError("PHS_REPAIR_FAILED " + "reason=item_data_missing");
                return false;
            }
            if (target == null)// 대상이 없으면 수리를 불가능
            {
                Debug.LogError("PHS_REPAIR_FAILED " + "reason=target_missing");
                return false;
            }
            if(repairer == null)
            {
                Debug.LogError($"PHS_REPAIR_FAILED " + $"reason=repairer_missing " + $"itemId={itemData.ItemId} " + $"target={target.name}");
            }
            float repairAmount = itemData.RepairAmount; //SO 에 설정된 수리량을 가져옵니다.
            if (repairAmount <= 0) //내구도 0은 수리아이템으로 사용 X
            {
                Debug.Log($"PHS_REPAIR_FAILED " + $"reason=repair_amount_zero " + $"itemId={itemData.ItemId}");
                return false;
            }
            IRepairable repairable = target.GetComponentInParent<IRepairable>();

            if (repairable == null)
            {
                Debug.Log($"PHS_REPAIR_FAILED " + $"reason=repairable_not_found " + $"target={target.name}");
                return false;
            }
            if (!repairable.CanRepair)
            {
                Debug.Log($"PHS_REPAIR_FAILED " + $"reason=target_cannot_repair " + $"target={target.name}");
                return false;
            }
            bool repaired = repairable.ApplyRepair(repairAmount, repairer); //수리 대상에게 수리량을 전달

            if (!repaired)
            {
                Debug.Log($"PHS_REPAIR_FAILED " + $"reason=apply_repair_rejected " + $"target={target.name}");
                return false;
            }
            Debug.Log($"PHS_REPAIR_SUCCEEDED " + $"itemId={itemData.ItemId} " + $"target={target.name} " + $"amount={repairAmount} " + $"current={repairable.CurrentIntegrity} " + $"maximum={repairable.MaxIntegrity}");

            return true;
        }
    }
}