namespace LastJumpCrew.ParkHanSol.Items
{
    // 렌치 사용 기능이다. 실제 수리 판정은 여기에 추가한다.
    public sealed class WrenchUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_WRENCH_USED item={name}");
        }
    }
}
