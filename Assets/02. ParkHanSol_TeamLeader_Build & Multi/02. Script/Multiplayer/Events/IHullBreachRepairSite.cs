using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public interface IHullBreachRepairSite
    {
        string SiteId { get; }
        Vector3 RepairPosition { get; }
        bool IsAvailable { get; }

        bool TryActivate(out string reason);
        void Deactivate();
    }

    public interface IHullBreachRepairSiteProvider
    {
        bool TryAcquireSite(out IHullBreachRepairSite site, out string reason);
    }
}
