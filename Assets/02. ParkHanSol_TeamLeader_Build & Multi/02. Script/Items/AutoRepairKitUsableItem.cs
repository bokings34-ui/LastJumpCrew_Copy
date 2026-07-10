namespace LastJumpCrew.ParkHanSol.Items
{
    // 자동 수리 키트 사용 기능이다. 실제 수리 판정은 여기에 추가한다.
    public sealed class AutoRepairKitUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_AUTO_REPAIR_KIT_USED item={name}");
        }
    }
}
