using LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class PHSFireExtinguisherFamilyUsableItem :
        PHSUtilityFamilyUsableItem,
        IContinuousUsableItem
    {
        protected override PHSUtilityFamilyActionKind FamilyKind =>
            PHSUtilityFamilyActionKind.FireExtinguisher;
        protected override PHSItemUseActionKind PresentationKind =>
            PHSItemUseActionKind.FireExtinguisher;
    }
}
