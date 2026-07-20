namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShipDockRepairService
    {
        bool TryPurchaseRepairServer(
            string offerId,
            string purchaseId,
            out string reason);
    }
}
