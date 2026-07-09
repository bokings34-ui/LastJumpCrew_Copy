namespace LastJumpCrew.ParkHanSol.Items
{
    // 미래형 캐니스터 사용 기능이다. 실제 대상 판정은 여기에 추가한다.
    public sealed class FuturisticCanisterUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_FUTURISTIC_CANISTER_USED item={name}");
        }
    }
}
