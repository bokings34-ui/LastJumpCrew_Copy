using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopDeliveryService
    {
        int PendingCount { get; }
        bool TryQueueDelivery(UtilityItemPrefabData itemPrefabData);
    }
}
