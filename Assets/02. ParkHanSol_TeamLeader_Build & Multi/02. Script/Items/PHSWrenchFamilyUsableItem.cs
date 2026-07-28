namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class PHSWrenchFamilyUsableItem :
        PHSUtilityFamilyUsableItem
    {
        protected override PHSUtilityFamilyActionKind FamilyKind =>
            PHSUtilityFamilyActionKind.Wrench;
        protected override PHSItemUseActionKind PresentationKind =>
            PHSItemUseActionKind.Wrench;
    }
}
