using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations
{
    [DisallowMultipleComponent]
    public sealed class PHSShipIncidentZone : MonoBehaviour
    {
        [Header("Zone Identity")]
        [SerializeField] private string zoneId;
        [SerializeField] private string displayName;
        [SerializeField] private PHSShipIncidentZone parentZone;
        [SerializeField] private NetworkShipModuleId primaryModule = NetworkShipModuleId.None;

        [Header("Scene References")]
        [SerializeField] private Collider zoneBounds;
        [SerializeField] private PHSShipIncidentZone[] adjacentZones =
            Array.Empty<PHSShipIncidentZone>();
        [SerializeField] private Transform alarmPresentationRoot;

        [Header("Selection And Capacity")]
        [SerializeField, Min(0.01f)] private float baseRiskWeight = 1f;
        [SerializeField, Min(1)] private int maximumIndependentAccidents = 1;
        [SerializeField, Min(0f)] private float cooldownSeconds = 10f;

        private readonly Dictionary<ulong, int> activeOwnerReferenceCounts = new();
        private double nextAvailableTime;

        public string ZoneId => zoneId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? zoneId
            : displayName;
        public PHSShipIncidentZone ParentZone => parentZone;
        public NetworkShipModuleId PrimaryModule => primaryModule;
        public Collider ZoneBounds => zoneBounds;
        public IReadOnlyList<PHSShipIncidentZone> AdjacentZones =>
            adjacentZones ?? Array.Empty<PHSShipIncidentZone>();
        public Transform AlarmPresentationRoot => alarmPresentationRoot;
        public float BaseRiskWeight => baseRiskWeight;
        public int MaximumIndependentAccidents => maximumIndependentAccidents;
        public int CurrentIncidentCount => activeOwnerReferenceCounts.Count;
        public double NextAvailableTime => nextAvailableTime;

        public bool IsAvailable(double currentTime)
        {
            return IsTimeValid(currentTime)
                && currentTime >= nextAvailableTime
                && activeOwnerReferenceCounts.Count < maximumIndependentAccidents
                && (parentZone == null || parentZone.IsAvailable(currentTime));
        }

        public bool MatchesZone(string requestedZoneId)
        {
            if (string.IsNullOrWhiteSpace(requestedZoneId))
            {
                return true;
            }

            var current = this;
            var visited = new HashSet<PHSShipIncidentZone>();
            while (current != null && visited.Add(current))
            {
                if (string.Equals(
                        current.ZoneId,
                        requestedZoneId,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.ParentZone;
            }

            return false;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return zoneBounds != null && zoneBounds.bounds.Contains(worldPosition);
        }

        public bool TryValidate(out string reason)
        {
            if (!IncidentStableId.IsValid(zoneId))
            {
                reason = $"zone_id_invalid:{zoneId}";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetworkShipModuleId), primaryModule))
            {
                reason = $"primary_module_invalid:{(byte)primaryModule}";
                return false;
            }

            if (zoneBounds == null)
            {
                reason = "zone_bounds_missing";
                return false;
            }

            if (!zoneBounds.isTrigger)
            {
                reason = "zone_bounds_not_trigger";
                return false;
            }

            if (baseRiskWeight <= 0f
                || float.IsNaN(baseRiskWeight)
                || float.IsInfinity(baseRiskWeight))
            {
                reason = $"base_risk_weight_invalid:{baseRiskWeight}";
                return false;
            }

            if (maximumIndependentAccidents <= 0)
            {
                reason =
                    $"maximum_independent_accidents_invalid:{maximumIndependentAccidents}";
                return false;
            }

            if (cooldownSeconds < 0f
                || float.IsNaN(cooldownSeconds)
                || float.IsInfinity(cooldownSeconds))
            {
                reason = $"cooldown_seconds_invalid:{cooldownSeconds}";
                return false;
            }

            if (!TryValidateParentChain(out reason))
            {
                return false;
            }

            var uniqueAdjacentZones = new HashSet<PHSShipIncidentZone>();
            foreach (var adjacentZone in AdjacentZones)
            {
                if (adjacentZone == null)
                {
                    reason = "adjacent_zone_missing";
                    return false;
                }

                if (adjacentZone == this)
                {
                    reason = "adjacent_zone_self_reference";
                    return false;
                }

                if (!uniqueAdjacentZones.Add(adjacentZone))
                {
                    reason = $"adjacent_zone_duplicate:{adjacentZone.ZoneId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        internal bool CanAcquire(
            ulong occupantId,
            double currentTime,
            out string reason)
        {
            if (occupantId == 0UL)
            {
                reason = "occupant_id_invalid";
                return false;
            }

            if (!IsTimeValid(currentTime))
            {
                reason = $"current_time_invalid:{currentTime}";
                return false;
            }

            var alreadyActive = activeOwnerReferenceCounts.ContainsKey(occupantId);
            if (!alreadyActive && currentTime < nextAvailableTime)
            {
                reason = $"zone_cooldown_active:{zoneId}";
                return false;
            }

            if (!alreadyActive
                && activeOwnerReferenceCounts.Count >= maximumIndependentAccidents)
            {
                reason = $"zone_capacity_reached:{zoneId}";
                return false;
            }

            if (parentZone != null
                && !parentZone.CanAcquire(occupantId, currentTime, out var parentReason))
            {
                reason = $"parent_zone_unavailable:{parentReason}";
                return false;
            }

            reason = null;
            return true;
        }

        internal bool TryAcquire(
            ulong occupantId,
            double currentTime,
            out string reason)
        {
            if (!CanAcquire(occupantId, currentTime, out reason))
            {
                return false;
            }

            if (parentZone != null
                && !parentZone.TryAcquire(occupantId, currentTime, out var parentReason))
            {
                reason = $"parent_zone_acquire_failed:{parentReason}";
                return false;
            }

            if (activeOwnerReferenceCounts.TryGetValue(
                    occupantId,
                    out var referenceCount))
            {
                activeOwnerReferenceCounts[occupantId] = referenceCount + 1;
            }
            else
            {
                activeOwnerReferenceCounts.Add(occupantId, 1);
            }

            reason = null;
            return true;
        }

        internal bool TryRelease(
            ulong occupantId,
            double currentTime,
            out string reason)
        {
            if (!IsTimeValid(currentTime))
            {
                reason = $"current_time_invalid:{currentTime}";
                return false;
            }

            if (!activeOwnerReferenceCounts.TryGetValue(
                    occupantId,
                    out var referenceCount))
            {
                reason = $"zone_occupant_missing:{zoneId}:{occupantId}";
                return false;
            }

            if (parentZone != null
                && !parentZone.TryRelease(occupantId, currentTime, out var parentReason))
            {
                reason = $"parent_zone_release_failed:{parentReason}";
                return false;
            }

            if (referenceCount > 1)
            {
                activeOwnerReferenceCounts[occupantId] = referenceCount - 1;
            }
            else
            {
                activeOwnerReferenceCounts.Remove(occupantId);
                nextAvailableTime = Math.Max(
                    nextAvailableTime,
                    currentTime + cooldownSeconds);
            }

            reason = null;
            return true;
        }

        internal void ResetRuntimeState()
        {
            activeOwnerReferenceCounts.Clear();
            nextAvailableTime = 0d;
        }

        private bool TryValidateParentChain(out string reason)
        {
            var current = parentZone;
            var visited = new HashSet<PHSShipIncidentZone> { this };
            while (current != null)
            {
                if (!visited.Add(current))
                {
                    reason = $"parent_zone_cycle:{zoneId}";
                    return false;
                }

                current = current.ParentZone;
            }

            reason = null;
            return true;
        }

        private static bool IsTimeValid(double currentTime)
        {
            return !double.IsNaN(currentTime)
                && !double.IsInfinity(currentTime)
                && currentTime >= 0d;
        }
    }
}
