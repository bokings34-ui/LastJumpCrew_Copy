using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations
{
    [DisallowMultipleComponent]
    public sealed class PHSShipIncidentLayout : MonoBehaviour
    {
        [Header("Inspector References")]
        [SerializeField] private PHSShipIncidentZone[] zones =
            Array.Empty<PHSShipIncidentZone>();
        [SerializeField] private PHSIncidentLocationAnchor[] locations =
            Array.Empty<PHSIncidentLocationAnchor>();

        [Header("Migration Fallback")]
        [Tooltip("Off by default. Prefer explicit Inspector references.")]
        [SerializeField] private bool includeChildAuthoringFallback;

        private readonly Dictionary<string, PHSShipIncidentZone> zonesById =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, PHSIncidentLocationAnchor> locationsById =
            new(StringComparer.Ordinal);
        private readonly List<PHSIncidentLocationAnchor> orderedLocations = new();
        private bool setupValid;

        public IReadOnlyList<PHSShipIncidentZone> Zones =>
            zones ?? Array.Empty<PHSShipIncidentZone>();
        public IReadOnlyList<PHSIncidentLocationAnchor> Locations =>
            locations ?? Array.Empty<PHSIncidentLocationAnchor>();
        public bool IsReady => setupValid;

        private void Awake()
        {
            setupValid = RebuildCache(out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_INCIDENT_LAYOUT_SETUP_FAILED reason={reason}",
                    this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            foreach (var location in Locations)
            {
                if (location != null)
                {
                    location.ResetRuntimeState();
                }
            }

            foreach (var zone in Zones)
            {
                if (zone != null)
                {
                    zone.ResetRuntimeState();
                }
            }
        }

        public bool TryValidate(out string reason)
        {
            setupValid = RebuildCache(out reason);
            return setupValid;
        }

        public bool TryResolve(
            string locationId,
            out IIncidentLocation location)
        {
            if (EnsureReady(out _)
                && !string.IsNullOrWhiteSpace(locationId)
                && locationsById.TryGetValue(locationId, out var anchor))
            {
                location = anchor;
                return true;
            }

            location = null;
            return false;
        }

        public bool TryResolveAnchor(
            string locationId,
            out PHSIncidentLocationAnchor location)
        {
            if (EnsureReady(out _)
                && !string.IsNullOrWhiteSpace(locationId)
                && locationsById.TryGetValue(locationId, out location))
            {
                return true;
            }

            location = null;
            return false;
        }

        public bool TryResolveZone(
            string zoneId,
            out PHSShipIncidentZone zone)
        {
            if (EnsureReady(out _)
                && !string.IsNullOrWhiteSpace(zoneId)
                && zonesById.TryGetValue(zoneId, out zone))
            {
                return true;
            }

            zone = null;
            return false;
        }

        public bool TryCopyCandidates(
            IncidentLocationQuery query,
            List<IIncidentLocation> destination,
            out string reason)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (!EnsureReady(out reason))
            {
                return false;
            }

            if (!query.TryValidate(out var queryReason))
            {
                reason = $"query_invalid:{queryReason}";
                return false;
            }

            foreach (var location in orderedLocations)
            {
                if (location.Supports(query))
                {
                    destination.Add(location);
                }
            }

            if (destination.Count == 0)
            {
                reason =
                    $"compatible_location_unavailable:{query.Family}:{query.ContentId}";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryOccupy(
            string locationId,
            ulong occupantId,
            double currentTime,
            out string reason)
        {
            if (!TryResolve(locationId, out var location))
            {
                reason = $"location_missing:{locationId}";
                return false;
            }

            return location.TryOccupy(occupantId, currentTime, out reason);
        }

        public bool TryRelease(
            string locationId,
            ulong occupantId,
            double currentTime,
            out string reason)
        {
            if (!TryResolve(locationId, out var location))
            {
                reason = $"location_missing:{locationId}";
                return false;
            }

            return location.TryRelease(occupantId, currentTime, out reason);
        }

        private bool EnsureReady(out string reason)
        {
            if (setupValid)
            {
                reason = null;
                return true;
            }

            setupValid = RebuildCache(out reason);
            return setupValid;
        }

        private bool RebuildCache(out string reason)
        {
            MergeChildAuthoringFallback();
            zonesById.Clear();
            locationsById.Clear();
            orderedLocations.Clear();

            if (zones == null || zones.Length == 0)
            {
                reason = "zones_empty";
                return false;
            }

            if (locations == null || locations.Length == 0)
            {
                reason = "locations_empty";
                return false;
            }

            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    reason = "zone_missing";
                    return false;
                }

                if (!zone.TryValidate(out var zoneReason))
                {
                    reason = $"zone_invalid:{zone.ZoneId}:{zoneReason}";
                    return false;
                }

                if (!zonesById.TryAdd(zone.ZoneId, zone))
                {
                    reason = $"zone_id_duplicate:{zone.ZoneId}";
                    return false;
                }
            }

            foreach (var zone in zones)
            {
                if (zone.ParentZone != null
                    && !zonesById.ContainsValue(zone.ParentZone))
                {
                    reason =
                        $"parent_zone_not_registered:{zone.ZoneId}:{zone.ParentZone.ZoneId}";
                    return false;
                }

                foreach (var adjacentZone in zone.AdjacentZones)
                {
                    if (!zonesById.ContainsValue(adjacentZone))
                    {
                        reason =
                            $"adjacent_zone_not_registered:{zone.ZoneId}:{adjacentZone.ZoneId}";
                        return false;
                    }
                }
            }

            foreach (var location in locations)
            {
                if (location == null)
                {
                    reason = "location_missing";
                    return false;
                }

                if (!location.TryValidate(out var locationReason))
                {
                    reason =
                        $"location_invalid:{location.LocationId}:{locationReason}";
                    return false;
                }

                if (!zonesById.ContainsValue(location.Zone))
                {
                    reason =
                        $"location_zone_not_registered:{location.LocationId}:{location.Zone.ZoneId}";
                    return false;
                }

                if (!locationsById.TryAdd(location.LocationId, location))
                {
                    reason = $"location_id_duplicate:{location.LocationId}";
                    return false;
                }

                orderedLocations.Add(location);
            }

            orderedLocations.Sort(
                (left, right) => string.CompareOrdinal(
                    left.LocationId,
                    right.LocationId));
            reason = null;
            return true;
        }

        private void MergeChildAuthoringFallback()
        {
            if (!includeChildAuthoringFallback)
            {
                return;
            }

            zones = MergeUnique(
                zones,
                GetComponentsInChildren<PHSShipIncidentZone>(true));
            locations = MergeUnique(
                locations,
                GetComponentsInChildren<PHSIncidentLocationAnchor>(true));
        }

        private static T[] MergeUnique<T>(T[] configured, T[] discovered)
            where T : UnityEngine.Object
        {
            var merged = new List<T>(configured ?? Array.Empty<T>());
            foreach (var candidate in discovered)
            {
                if (candidate != null && !merged.Contains(candidate))
                {
                    merged.Add(candidate);
                }
            }

            return merged.ToArray();
        }
    }
}
