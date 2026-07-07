namespace LastJumpCrew.Common
{
    public interface IUsableItem
    {
        bool CanUse(IItemHolder user, IInteractable target);
        void Use(IItemHolder user, IInteractable target);
    }
}
