namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopItemUsePolicy
    {
        bool CanUseHeldItemServer(
            ulong playerClientId,
            string itemId,
            out string reason);
    }
}
