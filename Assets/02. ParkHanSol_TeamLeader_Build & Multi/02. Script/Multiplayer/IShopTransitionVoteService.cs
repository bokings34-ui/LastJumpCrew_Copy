using LastJumpCrew.ParkHanSol.Shop;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IShopTransitionVoteService
    {
        bool IsVoteActive { get; }
        bool IsShopExitVote { get; }
        int AgreeCount { get; }
        int RequiredAgreeCount { get; }
        int EligiblePlayerCount { get; }
        string DestinationSceneName { get; }
        void SubmitLocalVote(bool agree);
        bool TryStartVote(
            ulong initiatorClientId,
            string destinationSceneName,
            ShopSceneTransitionMode transitionMode,
            bool isShopExit,
            out string reason);
    }
}
