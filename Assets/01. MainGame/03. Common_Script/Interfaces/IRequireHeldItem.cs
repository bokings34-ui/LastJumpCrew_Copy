namespace LastJumpCrew.Common
{
    public interface IRequireHeldItem
    {
        string RequiredItemId { get; }
        bool IsRequirementMet(IItemHolder itemHolder);
    }
}
