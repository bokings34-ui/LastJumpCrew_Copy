using UnityEngine;

namespace SM
{
    public interface IOxygenLeakZone
    {
        string ZoneId { get; }
        Vector3 RepairPosition { get; }
        bool IsAvailable { get; }

        bool TryActivate(out string reason);
        void Deactivate();
    }

    public interface IOxygenLeakZoneProvider
    {
        bool TryAcquireZone(out IOxygenLeakZone zone, out string reason);
    }
}
