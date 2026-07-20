using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations
{
    [DisallowMultipleComponent]
    public sealed class PHSIncidentLocationAnchor : MonoBehaviour, IIncidentLocation
    {
        [Header("Location Identity")]
        [SerializeField] private string locationId;
        [SerializeField] private PHSShipIncidentZone zone;
        [SerializeField] private IncidentLocationKind kind = IncidentLocationKind.Device;
        [SerializeField] private IncidentLocationCapability capabilities =
            IncidentLocationCapability.Presentation;
        [SerializeField] private NetworkShipModuleId moduleOverride =
            NetworkShipModuleId.None;
        [SerializeField] private bool allowOutsideZoneBounds;

        [Header("Incident Compatibility")]
        [SerializeField] private NetworkRunIncidentChannel[] supportedChannels =
            Array.Empty<NetworkRunIncidentChannel>();
        [SerializeField] private NetworkRunIncidentFamily[] supportedFamilies =
            Array.Empty<NetworkRunIncidentFamily>();
        [Tooltip("Empty means every ContentId inside supported families.")]
        [SerializeField] private int[] supportedContentIds = Array.Empty<int>();

        [Header("Selection And Cooldown")]
        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 10f;

        [Header("Content Sockets")]
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private Collider hazardBounds;

        [Header("Runtime Adapter Bridge")]
        [Tooltip("Optional explicit bridge to legacy ShipRoom or PHSShipAccidentAnchor.")]
        [SerializeField] private Component runtimeTarget;

        private ulong occupantId;
        private double nextAvailableTime;

        public string LocationId => locationId;
        public IncidentLocationKind Kind => kind;
        public IncidentLocationCapability Capabilities => capabilities;
        public PHSShipIncidentZone Zone => zone;
        public NetworkShipModuleId ModuleId => moduleOverride != NetworkShipModuleId.None
            ? moduleOverride
            : zone == null
                ? NetworkShipModuleId.None
                : zone.PrimaryModule;
        public float SelectionWeight => zone == null
            ? selectionWeight
            : selectionWeight * zone.BaseRiskWeight;
        public bool IsOccupied => occupantId != 0UL;
        public ulong OccupantId => occupantId;
        public double NextAvailableTime => nextAvailableTime;
        public Transform LocationTransform => transform;
        public Transform PresentationRoot => presentationRoot;
        public Collider HazardBounds => hazardBounds;
        public Component RuntimeTarget => runtimeTarget;

        public bool IsAvailable(double currentTime)
        {
            return IsTimeValid(currentTime)
                && !IsOccupied
                && currentTime >= nextAvailableTime
                && zone != null
                && zone.CanAcquire(ulong.MaxValue, currentTime, out _);
        }

        public bool Supports(IncidentLocationQuery query)
        {
            if (!query.TryValidate(out _))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(query.RequestedLocationId)
                && !string.Equals(
                    locationId,
                    query.RequestedLocationId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (zone == null || !zone.MatchesZone(query.RequestedZoneId))
            {
                return false;
            }

            if (query.RequiredKind != IncidentLocationKind.None
                && query.RequiredKind != kind)
            {
                return false;
            }

            if ((capabilities & query.RequiredCapabilities)
                != query.RequiredCapabilities)
            {
                return false;
            }

            if (query.ModuleId != NetworkShipModuleId.None
                && query.ModuleId != ModuleId)
            {
                return false;
            }

            if (!Contains(supportedChannels, query.Channel)
                || !Contains(supportedFamilies, query.Family))
            {
                return false;
            }

            if (supportedContentIds != null
                && supportedContentIds.Length > 0
                && !Contains(supportedContentIds, query.ContentId))
            {
                return false;
            }

            return !query.RequireAvailable || IsAvailable(query.CurrentTime);
        }

        public bool TryOccupy(
            ulong newOccupantId,
            double currentTime,
            out string reason)
        {
            if (newOccupantId == 0UL)
            {
                reason = "occupant_id_invalid";
                return false;
            }

            if (!IsTimeValid(currentTime))
            {
                reason = $"current_time_invalid:{currentTime}";
                return false;
            }

            if (IsOccupied)
            {
                reason = $"location_occupied:{locationId}:{occupantId}";
                return false;
            }

            if (currentTime < nextAvailableTime)
            {
                reason = $"location_cooldown_active:{locationId}";
                return false;
            }

            if (zone == null)
            {
                reason = $"location_zone_missing:{locationId}";
                return false;
            }

            if (!zone.TryAcquire(newOccupantId, currentTime, out var zoneReason))
            {
                reason = $"location_zone_unavailable:{zoneReason}";
                return false;
            }

            occupantId = newOccupantId;
            reason = null;
            return true;
        }

        public bool TryRelease(
            ulong releasingOccupantId,
            double currentTime,
            out string reason)
        {
            if (!IsTimeValid(currentTime))
            {
                reason = $"current_time_invalid:{currentTime}";
                return false;
            }

            if (!IsOccupied)
            {
                reason = $"location_not_occupied:{locationId}";
                return false;
            }

            if (releasingOccupantId != occupantId)
            {
                reason =
                    $"location_occupant_mismatch:{locationId}:{releasingOccupantId}:{occupantId}";
                return false;
            }

            if (zone == null)
            {
                reason = "location_zone_release_failed:zone_missing";
                return false;
            }

            if (!zone.TryRelease(
                    releasingOccupantId,
                    currentTime,
                    out var zoneReason))
            {
                reason = $"location_zone_release_failed:{zoneReason}";
                return false;
            }

            occupantId = 0UL;
            nextAvailableTime = Math.Max(
                nextAvailableTime,
                currentTime + cooldownSeconds);
            reason = null;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            if (!IncidentStableId.IsValid(locationId))
            {
                reason = $"location_id_invalid:{locationId}";
                return false;
            }

            if (zone == null)
            {
                reason = "zone_missing";
                return false;
            }

            if (!allowOutsideZoneBounds && !zone.Contains(transform.position))
            {
                reason = $"location_outside_zone_bounds:{locationId}:{zone.ZoneId}";
                return false;
            }

            if (kind == IncidentLocationKind.None
                || !Enum.IsDefined(typeof(IncidentLocationKind), kind))
            {
                reason = $"location_kind_invalid:{(byte)kind}";
                return false;
            }

            if ((capabilities & ~IncidentLocationCapability.All) != 0)
            {
                reason = $"location_capabilities_invalid:{(ushort)capabilities}";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetworkShipModuleId), moduleOverride))
            {
                reason = $"module_override_invalid:{(byte)moduleOverride}";
                return false;
            }

            if (!TryValidateEnumArray(
                    supportedChannels,
                    "supported_channels",
                    out reason))
            {
                return false;
            }

            if (!TryValidateFamilies(out reason))
            {
                return false;
            }

            if (!TryValidateContentIds(out reason))
            {
                return false;
            }

            if (selectionWeight <= 0f
                || float.IsNaN(selectionWeight)
                || float.IsInfinity(selectionWeight))
            {
                reason = $"selection_weight_invalid:{selectionWeight}";
                return false;
            }

            if (cooldownSeconds < 0f
                || float.IsNaN(cooldownSeconds)
                || float.IsInfinity(cooldownSeconds))
            {
                reason = $"cooldown_seconds_invalid:{cooldownSeconds}";
                return false;
            }

            if ((capabilities & IncidentLocationCapability.Presentation) != 0
                && presentationRoot == null)
            {
                reason = "presentation_root_missing";
                return false;
            }

            if ((capabilities & IncidentLocationCapability.HazardArea) != 0
                && hazardBounds == null)
            {
                reason = "hazard_bounds_missing";
                return false;
            }

            if ((capabilities & IncidentLocationCapability.FirePropagation) != 0
                && (capabilities & IncidentLocationCapability.HazardArea) == 0)
            {
                reason = "fire_propagation_requires_hazard_area";
                return false;
            }

            reason = null;
            return true;
        }

        internal void ResetRuntimeState()
        {
            occupantId = 0UL;
            nextAvailableTime = 0d;
        }

        private bool TryValidateFamilies(out string reason)
        {
            if (supportedFamilies == null || supportedFamilies.Length == 0)
            {
                reason = "supported_families_empty";
                return false;
            }

            var unique = new HashSet<NetworkRunIncidentFamily>();
            foreach (var family in supportedFamilies)
            {
                if (family == NetworkRunIncidentFamily.None
                    || !Enum.IsDefined(typeof(NetworkRunIncidentFamily), family))
                {
                    reason = $"supported_family_invalid:{(byte)family}";
                    return false;
                }

                if (!unique.Add(family))
                {
                    reason = $"supported_family_duplicate:{family}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool TryValidateContentIds(out string reason)
        {
            if (supportedContentIds == null)
            {
                reason = null;
                return true;
            }

            var unique = new HashSet<int>();
            foreach (var contentId in supportedContentIds)
            {
                if (contentId <= 0)
                {
                    reason = $"supported_content_id_invalid:{contentId}";
                    return false;
                }

                if (!unique.Add(contentId))
                {
                    reason = $"supported_content_id_duplicate:{contentId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool TryValidateEnumArray<T>(
            T[] values,
            string fieldName,
            out string reason)
            where T : struct, Enum
        {
            if (values == null || values.Length == 0)
            {
                reason = $"{fieldName}_empty";
                return false;
            }

            var unique = new HashSet<T>();
            foreach (var value in values)
            {
                if (!Enum.IsDefined(typeof(T), value))
                {
                    reason = $"{fieldName}_invalid:{value}";
                    return false;
                }

                if (!unique.Add(value))
                {
                    reason = $"{fieldName}_duplicate:{value}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool Contains<T>(T[] values, T target)
        {
            if (values == null)
            {
                return false;
            }

            foreach (var value in values)
            {
                if (EqualityComparer<T>.Default.Equals(value, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTimeValid(double currentTime)
        {
            return !double.IsNaN(currentTime)
                && !double.IsInfinity(currentTime)
                && currentTime >= 0d;
        }
    }
}
