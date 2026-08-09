namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface ITravelConsoleFlow
    {
        string ActionPrompt { get; }

        bool CanSelectSide(TravelConsoleSide side);
        void RequestSelectSide(IItemHolder itemHolder, TravelConsoleSide side);
        bool CanExecute(IItemHolder itemHolder);
        void Execute(IItemHolder itemHolder);
    }
}
