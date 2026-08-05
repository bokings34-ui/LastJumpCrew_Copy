namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class AutoRepairKitUsableItem :
        PHSUtilityFamilyUsableItem
    {
        protected override PHSUtilityFamilyActionKind FamilyKind =>
            PHSUtilityFamilyActionKind.Wrench;
        protected override PHSItemUseActionKind PresentationKind =>
            PHSItemUseActionKind.Wrench;
    }
}
