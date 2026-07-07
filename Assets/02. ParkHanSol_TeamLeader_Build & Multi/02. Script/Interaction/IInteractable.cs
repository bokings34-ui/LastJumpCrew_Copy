namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(IItemHolder itemHolder);
        void Interact(IItemHolder itemHolder);
    }
}
