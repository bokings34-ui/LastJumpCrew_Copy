namespace LastJumpCrew.Common
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(IItemHolder itemHolder);
        void Interact(IItemHolder itemHolder);
    }
}
