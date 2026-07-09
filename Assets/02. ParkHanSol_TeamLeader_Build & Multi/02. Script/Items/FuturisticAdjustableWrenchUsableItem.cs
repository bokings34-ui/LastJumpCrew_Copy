namespace LastJumpCrew.ParkHanSol.Items
{
    // 미래형 렌치 사용 기능이다. 실제 수리/타격 판정은 여기에 추가한다.
    public sealed class FuturisticAdjustableWrenchUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_FUTURISTIC_ADJUSTABLE_WRENCH_USED item={name}");
        }
    }
}
