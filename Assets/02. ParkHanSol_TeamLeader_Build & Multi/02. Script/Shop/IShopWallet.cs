using System;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopWallet
    {
        bool IsReady { get; }
        int Credits { get; }
        event Action<int> CreditsChanged;
        bool TryAddCredits(int amount);
        bool TrySpendCredits(int amount);
    }
}
