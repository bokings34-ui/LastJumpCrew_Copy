namespace LastJumpCrew.ParkHanSol.Items
{
    // 소화기 사용 기능이다. 실제 소화 판정은 여기에 추가한다.
    public sealed class FireExtinguisherUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(LastJumpCrew.Common.IItemHolder user, LastJumpCrew.Common.IInteractable target)
        {
            UnityEngine.Debug.Log($"PHS_FIRE_EXTINGUISHER_USED item={name}");
        }
    }
}
