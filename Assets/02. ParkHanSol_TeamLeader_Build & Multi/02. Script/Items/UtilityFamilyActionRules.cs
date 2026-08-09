namespace LastJumpCrew.ParkHanSol.Items
{
    public static class UtilityFamilyActionRules
    {
        public static bool Allows(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemActionKind actionKind)
        {
            return familyKind switch
            {
                PHSUtilityFamilyActionKind.Wrench =>
                    actionKind is UtilityItemActionKind.DeviceRepair
                        or UtilityItemActionKind.HullBreachRepair
                        or UtilityItemActionKind.SteamLeakRepair
                        or UtilityItemActionKind.OxygenLeakRepair
                        or UtilityItemActionKind.OxygenGeneratorRepair
                        or UtilityItemActionKind.GravityGeneratorRepair,
                PHSUtilityFamilyActionKind.FireExtinguisher =>
                    actionKind == UtilityItemActionKind.FireSuppression,
                _ => false
            };
        }
    }
}
