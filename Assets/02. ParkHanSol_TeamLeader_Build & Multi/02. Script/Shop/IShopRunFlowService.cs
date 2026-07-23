namespace LastJumpCrew.ParkHanSol.Shop
{
    public enum ShopSceneTransitionMode
    {
        None,
        RequireShopPhase,
        CompleteShop
    }

    public interface IShopRunFlowService
    {
        bool IsReady { get; }
        bool IsShopVisitRequired { get; }
        bool CanEnterShop(out string reason);
        bool CanCompleteShop(out string reason);
        bool TryCompleteShop(out string reason);
    }
}
