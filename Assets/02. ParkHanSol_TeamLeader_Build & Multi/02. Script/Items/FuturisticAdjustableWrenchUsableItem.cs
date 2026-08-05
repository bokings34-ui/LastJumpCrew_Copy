namespace LastJumpCrew.ParkHanSol.Items
{
    // 미래형 렌치 사용 기능이다. 실제 수리/타격 판정은 여기에 추가한다.
    public sealed class FuturisticAdjustableWrenchUsableItem :
        ProfiledRepairUsableItem
    {
        protected override string ExpectedItemId =>
            "futuristic_adjustable_wrench";

        protected override bool SupportsAction(
            UtilityItemActionKind actionKind)
        {
            return actionKind is UtilityItemActionKind.DeviceRepair
                or UtilityItemActionKind.HullBreachRepair
                or UtilityItemActionKind.SteamLeakRepair
                or UtilityItemActionKind.OxygenLeakRepair
                or UtilityItemActionKind.OxygenGeneratorRepair
                or UtilityItemActionKind.GravityGeneratorRepair;
        }
    }
}
