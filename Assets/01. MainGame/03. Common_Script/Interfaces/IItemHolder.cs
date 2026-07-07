namespace LastJumpCrew.Common
{
    public interface IItemHolder
    {
        IHoldableItem CurrentItem { get; }
        bool HasItem { get; }
        bool CanHold(IHoldableItem item);
        void Hold(IHoldableItem item);
        void Drop();
    }
}
