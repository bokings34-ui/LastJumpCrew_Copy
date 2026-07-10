namespace LastJumpCrew.ParkHanSol.Items
{
    // 폼 실란트 건 사용 기능이다. 실제 분사/봉합 판정은 여기에 추가한다.
    public sealed class FoamSealantGunUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_FOAM_SEALANT_GUN_USED item={name}");
        }
    }
}
