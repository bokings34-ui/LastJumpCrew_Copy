using LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Items
{
    public interface IOfflineUtilityActionTarget :
        IInteractable,
        IUtilityAttackTarget
    {
        UtilityItemActionKind ActionKind { get; }
        bool IsResolved { get; }
    }
}
