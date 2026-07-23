namespace LastJumpCrew.ParkHanSol.Items
{
    // Tripo 소화기 사용 기능이다. 실제 소화 판정은 여기에 추가한다.
    public sealed class TripoFireExtinguisherUsableItem :
        ProfiledRepairUsableItem
    {
        protected override string ExpectedItemId =>
            "tripo_fire_extinguisher";

        protected override bool SupportsAction(
            UtilityItemActionKind actionKind)
        {
            return actionKind == UtilityItemActionKind.FireSuppression;
        }
    }
}
