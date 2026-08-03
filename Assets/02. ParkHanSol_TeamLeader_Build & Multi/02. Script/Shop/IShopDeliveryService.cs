using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public interface IShopDeliveryService
    {
        int PendingCount { get; }
        bool CanQueueDeliveries(IReadOnlyList<UtilityItemDataSO> itemPrefabData);
        bool TryQueueDeliveries(IReadOnlyList<UtilityItemDataSO> itemPrefabData);
        bool TryQueueDelivery(UtilityItemDataSO itemPrefabData);
    }
}
