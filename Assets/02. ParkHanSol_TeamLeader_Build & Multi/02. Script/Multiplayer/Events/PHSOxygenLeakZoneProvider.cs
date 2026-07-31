using System.Collections.Generic;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSOxygenLeakZoneProvider :
        MonoBehaviour,
        IOxygenLeakZoneProvider
    {
        [SerializeField]
        private PHSOxygenDeprivationZone[] zones =
            System.Array.Empty<PHSOxygenDeprivationZone>();

        private readonly List<PHSOxygenDeprivationZone> availableZones =
            new();
        private PHSOxygenDeprivationZone lastSelectedZone;

        public bool TryAcquireZone(
            out IOxygenLeakZone zone,
            out string reason)
        {
            zone = null;
            if (!TryValidate(out reason))
            {
                return false;
            }

            availableZones.Clear();
            foreach (var candidate in zones)
            {
                if (candidate.IsAvailable
                    && (zones.Length == 1 || candidate != lastSelectedZone))
                {
                    availableZones.Add(candidate);
                }
            }

            if (availableZones.Count == 0
                && lastSelectedZone != null
                && lastSelectedZone.IsAvailable)
            {
                availableZones.Add(lastSelectedZone);
            }

            if (availableZones.Count == 0)
            {
                reason = "zone_unavailable";
                return false;
            }

            var selected = availableZones[
                Random.Range(0, availableZones.Count)];
            if (!selected.TryActivate(out reason))
            {
                return false;
            }

            lastSelectedZone = selected;
            zone = selected;
            reason = null;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            if (zones == null || zones.Length == 0)
            {
                reason = "zones_missing";
                return false;
            }

            var zoneIds = new HashSet<string>();
            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    reason = "zone_reference_missing";
                    return false;
                }

                if (!zone.TryValidate(out var zoneReason))
                {
                    reason = $"zone_invalid:{zoneReason}";
                    return false;
                }

                if (!zoneIds.Add(zone.ZoneId))
                {
                    reason = $"zone_id_duplicate:{zone.ZoneId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
