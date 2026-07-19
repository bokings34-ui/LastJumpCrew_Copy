using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations
{
    public enum IncidentLocationKind : byte
    {
        None = 0,
        Room = 1,
        Device = 2,
        Pipe = 3,
        HullSurface = 4,
        FireSurface = 5,
        EnemyIngress = 6,
        Terminal = 7,
        GlobalShip = 8
    }

    [Flags]
    public enum IncidentLocationCapability : ushort
    {
        None = 0,
        Presentation = 1 << 0,
        Interaction = 1 << 1,
        HazardArea = 1 << 2,
        FirePropagation = 1 << 3,
        EnemySpawn = 1 << 4,
        ExteriorImpact = 1 << 5,
        RequestSource = 1 << 6,
        Alarm = 1 << 7,
        All = Presentation
            | Interaction
            | HazardArea
            | FirePropagation
            | EnemySpawn
            | ExteriorImpact
            | RequestSource
            | Alarm
    }

    public readonly struct IncidentLocationQuery
    {
        public IncidentLocationQuery(
            NetworkRunIncidentChannel channel,
            NetworkRunIncidentFamily family,
            int contentId,
            NetworkShipModuleId moduleId,
            IncidentLocationKind requiredKind,
            IncidentLocationCapability requiredCapabilities,
            string requestedZoneId,
            string requestedLocationId,
            double currentTime,
            bool requireAvailable = true)
        {
            Channel = channel;
            Family = family;
            ContentId = contentId;
            ModuleId = moduleId;
            RequiredKind = requiredKind;
            RequiredCapabilities = requiredCapabilities;
            RequestedZoneId = requestedZoneId;
            RequestedLocationId = requestedLocationId;
            CurrentTime = currentTime;
            RequireAvailable = requireAvailable;
        }

        public NetworkRunIncidentChannel Channel { get; }
        public NetworkRunIncidentFamily Family { get; }
        public int ContentId { get; }
        public NetworkShipModuleId ModuleId { get; }
        public IncidentLocationKind RequiredKind { get; }
        public IncidentLocationCapability RequiredCapabilities { get; }
        public string RequestedZoneId { get; }
        public string RequestedLocationId { get; }
        public double CurrentTime { get; }
        public bool RequireAvailable { get; }

        public bool TryValidate(out string reason)
        {
            if (!Enum.IsDefined(typeof(NetworkRunIncidentChannel), Channel))
            {
                reason = $"channel_invalid:{(byte)Channel}";
                return false;
            }

            if (Family == NetworkRunIncidentFamily.None
                || !Enum.IsDefined(typeof(NetworkRunIncidentFamily), Family))
            {
                reason = $"family_invalid:{(byte)Family}";
                return false;
            }

            if (ContentId <= 0)
            {
                reason = $"content_id_invalid:{ContentId}";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetworkShipModuleId), ModuleId))
            {
                reason = $"module_id_invalid:{(byte)ModuleId}";
                return false;
            }

            if (!Enum.IsDefined(typeof(IncidentLocationKind), RequiredKind))
            {
                reason = $"location_kind_invalid:{(byte)RequiredKind}";
                return false;
            }

            if ((RequiredCapabilities & ~IncidentLocationCapability.All) != 0)
            {
                reason = $"location_capabilities_invalid:{(ushort)RequiredCapabilities}";
                return false;
            }

            if (!string.IsNullOrEmpty(RequestedZoneId)
                && !IncidentStableId.IsValid(RequestedZoneId))
            {
                reason = $"requested_zone_id_invalid:{RequestedZoneId}";
                return false;
            }

            if (!string.IsNullOrEmpty(RequestedLocationId)
                && !IncidentStableId.IsValid(RequestedLocationId))
            {
                reason =
                    $"requested_location_id_invalid:{RequestedLocationId}";
                return false;
            }

            if (double.IsNaN(CurrentTime)
                || double.IsInfinity(CurrentTime)
                || CurrentTime < 0d)
            {
                reason = $"current_time_invalid:{CurrentTime}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
