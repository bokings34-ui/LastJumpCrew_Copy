using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations
{
    public interface IIncidentLocation
    {
        string LocationId { get; }
        IncidentLocationKind Kind { get; }
        IncidentLocationCapability Capabilities { get; }
        PHSShipIncidentZone Zone { get; }
        NetworkShipModuleId ModuleId { get; }
        float SelectionWeight { get; }
        bool IsOccupied { get; }
        ulong OccupantId { get; }
        double NextAvailableTime { get; }
        Transform LocationTransform { get; }
        Transform PresentationRoot { get; }
        Collider HazardBounds { get; }
        Component RuntimeTarget { get; }

        bool IsAvailable(double currentTime);
        bool Supports(IncidentLocationQuery query);
        bool TryOccupy(
            ulong occupantId,
            double currentTime,
            out string reason);
        bool TryRelease(
            ulong occupantId,
            double currentTime,
            out string reason);
        bool TryValidate(out string reason);
    }
}
