using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopDeliveryService
    {
        int PendingCount { get; }
        bool CanQueueDeliveries(IReadOnlyList<UtilityItemPrefabData> itemPrefabData);
        bool TryQueueDeliveries(IReadOnlyList<UtilityItemPrefabData> itemPrefabData);
        bool TryQueueDelivery(UtilityItemPrefabData itemPrefabData);
    }
}
