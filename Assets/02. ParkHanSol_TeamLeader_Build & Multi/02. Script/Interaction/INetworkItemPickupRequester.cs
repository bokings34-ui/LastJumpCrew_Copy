using LastJumpCrew.ParkHanSol.Items;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public interface INetworkItemPickupRequester
    {
        bool CanRequestNetworkPickup(UtilityItemObject itemObject);
        void RequestNetworkPickup(UtilityItemObject itemObject);
    }
}
