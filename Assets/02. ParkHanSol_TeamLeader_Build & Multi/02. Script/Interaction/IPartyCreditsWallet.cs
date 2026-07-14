namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface IPartyCreditsWallet
    {
        int Credits { get; }
        void AddCredits(int value);
        bool TrySpendCredits(int value);
    }
}
