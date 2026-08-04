namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class AutoRepairKitUsableItem :
        ProfiledRepairUsableItem
    {
        protected override string ExpectedItemId => "auto_repair_kit";

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
