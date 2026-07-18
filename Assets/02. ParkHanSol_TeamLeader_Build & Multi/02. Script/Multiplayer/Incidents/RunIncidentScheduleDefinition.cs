using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public readonly struct RunIncidentWeightedEntry :
        IEquatable<RunIncidentWeightedEntry>
    {
        public RunIncidentWeightedEntry(
            int contentId,
            NetworkRunIncidentFamily incidentFamily,
            float weight,
            ushort pressureCost,
            float warpChargeMultiplier)
        {
            ContentId = contentId;
            IncidentFamily = incidentFamily;
            Weight = weight;
            PressureCost = pressureCost;
            WarpChargeMultiplier = warpChargeMultiplier;
        }

        public int ContentId { get; }
        public NetworkRunIncidentFamily IncidentFamily { get; }
        public float Weight { get; }
        public ushort PressureCost { get; }
        public float WarpChargeMultiplier { get; }

        public bool Equals(RunIncidentWeightedEntry other)
        {
            return ContentId == other.ContentId
                && IncidentFamily == other.IncidentFamily
                && Weight.Equals(other.Weight)
                && PressureCost == other.PressureCost
                && WarpChargeMultiplier.Equals(other.WarpChargeMultiplier);
        }

        public override bool Equals(object obj)
        {
            return obj is RunIncidentWeightedEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ContentId,
                IncidentFamily,
                Weight,
                PressureCost,
                WarpChargeMultiplier);
        }
    }

    /// <summary>
    /// Immutable, stage-specific input for the persistent incident director.
    /// Scene-owned map content is copied into this definition before configuration.
    /// </summary>
    public sealed class RunIncidentScheduleDefinition
    {
        private readonly RunIncidentWeightedEntry[] externalEntries;
        private readonly RunIncidentWeightedEntry[] internalEntries;
        private readonly ReadOnlyCollection<RunIncidentWeightedEntry> readOnlyExternalEntries;
        private readonly ReadOnlyCollection<RunIncidentWeightedEntry> readOnlyInternalEntries;

        public RunIncidentScheduleDefinition(
            int mapId,
            uint stageSequence,
            ushort pressureCapacity,
            byte maximumActiveExternal,
            byte maximumActiveInternal,
            float externalIntervalMinSeconds,
            float externalIntervalMaxSeconds,
            float internalIntervalMinSeconds,
            float internalIntervalMaxSeconds,
            RunIncidentWeightedEntry[] externalEntries,
            RunIncidentWeightedEntry[] internalEntries)
        {
            MapId = mapId;
            StageSequence = stageSequence;
            PressureCapacity = pressureCapacity;
            MaximumActiveExternal = maximumActiveExternal;
            MaximumActiveInternal = maximumActiveInternal;
            ExternalIntervalMinSeconds = externalIntervalMinSeconds;
            ExternalIntervalMaxSeconds = externalIntervalMaxSeconds;
            InternalIntervalMinSeconds = internalIntervalMinSeconds;
            InternalIntervalMaxSeconds = internalIntervalMaxSeconds;
            this.externalEntries = externalEntries == null
                ? Array.Empty<RunIncidentWeightedEntry>()
                : (RunIncidentWeightedEntry[])externalEntries.Clone();
            this.internalEntries = internalEntries == null
                ? Array.Empty<RunIncidentWeightedEntry>()
                : (RunIncidentWeightedEntry[])internalEntries.Clone();
            readOnlyExternalEntries = Array.AsReadOnly(this.externalEntries);
            readOnlyInternalEntries = Array.AsReadOnly(this.internalEntries);
        }

        public int MapId { get; }
        public uint StageSequence { get; }
        public ushort PressureCapacity { get; }
        public byte MaximumActiveExternal { get; }
        public byte MaximumActiveInternal { get; }
        public float ExternalIntervalMinSeconds { get; }
        public float ExternalIntervalMaxSeconds { get; }
        public float InternalIntervalMinSeconds { get; }
        public float InternalIntervalMaxSeconds { get; }
        public IReadOnlyList<RunIncidentWeightedEntry> ExternalEntries =>
            readOnlyExternalEntries;
        public IReadOnlyList<RunIncidentWeightedEntry> InternalEntries =>
            readOnlyInternalEntries;

        public bool TryValidate(out string reason)
        {
            if (MapId <= 0)
            {
                reason = "positive_map_id_required";
                return false;
            }

            if (StageSequence == 0U)
            {
                reason = "nonzero_stage_sequence_required";
                return false;
            }

            if (PressureCapacity == 0)
            {
                reason = "positive_pressure_capacity_required";
                return false;
            }

            if (!TryValidateChannel(
                    NetworkRunIncidentChannel.External,
                    MaximumActiveExternal,
                    ExternalIntervalMinSeconds,
                    ExternalIntervalMaxSeconds,
                    externalEntries,
                    PressureCapacity,
                    out reason))
            {
                return false;
            }

            return TryValidateChannel(
                NetworkRunIncidentChannel.Internal,
                MaximumActiveInternal,
                InternalIntervalMinSeconds,
                InternalIntervalMaxSeconds,
                internalEntries,
                PressureCapacity,
                out reason);
        }

        public bool IsEquivalentTo(RunIncidentScheduleDefinition other)
        {
            if (other == null
                || MapId != other.MapId
                || StageSequence != other.StageSequence
                || PressureCapacity != other.PressureCapacity
                || MaximumActiveExternal != other.MaximumActiveExternal
                || MaximumActiveInternal != other.MaximumActiveInternal
                || !ExternalIntervalMinSeconds.Equals(other.ExternalIntervalMinSeconds)
                || !ExternalIntervalMaxSeconds.Equals(other.ExternalIntervalMaxSeconds)
                || !InternalIntervalMinSeconds.Equals(other.InternalIntervalMinSeconds)
                || !InternalIntervalMaxSeconds.Equals(other.InternalIntervalMaxSeconds)
                || externalEntries.Length != other.externalEntries.Length
                || internalEntries.Length != other.internalEntries.Length)
            {
                return false;
            }

            return EntriesEqual(externalEntries, other.externalEntries)
                && EntriesEqual(internalEntries, other.internalEntries);
        }

        private static bool TryValidateChannel(
            NetworkRunIncidentChannel channel,
            byte maximumActive,
            float intervalMinSeconds,
            float intervalMaxSeconds,
            RunIncidentWeightedEntry[] entries,
            ushort pressureCapacity,
            out string reason)
        {
            var channelName = channel.ToString().ToLowerInvariant();
            if (maximumActive == 0)
            {
                if (entries.Length != 0)
                {
                    reason = $"{channelName}_entries_require_positive_active_cap";
                    return false;
                }

                reason = null;
                return true;
            }

            if (entries.Length == 0)
            {
                reason = $"{channelName}_entries_empty";
                return false;
            }

            if (!IsPositiveFinite(intervalMinSeconds))
            {
                reason = $"{channelName}_interval_min_invalid";
                return false;
            }

            if (!IsPositiveFinite(intervalMaxSeconds)
                || intervalMaxSeconds < intervalMinSeconds)
            {
                reason = $"{channelName}_interval_max_invalid";
                return false;
            }

            var contentIds = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry.ContentId <= 0)
                {
                    reason = $"{channelName}_content_id_invalid:{entry.ContentId}";
                    return false;
                }

                if (!contentIds.Add(entry.ContentId))
                {
                    reason = $"{channelName}_content_id_duplicate:{entry.ContentId}";
                    return false;
                }

                if (entry.IncidentFamily == NetworkRunIncidentFamily.None
                    || !Enum.IsDefined(
                        typeof(NetworkRunIncidentFamily),
                        entry.IncidentFamily))
                {
                    reason =
                        $"{channelName}_family_invalid:" +
                        $"{(byte)entry.IncidentFamily}";
                    return false;
                }

                if (!IsPositiveFinite(entry.Weight))
                {
                    reason =
                        $"{channelName}_weight_invalid:" +
                        $"{entry.ContentId}";
                    return false;
                }

                if (entry.PressureCost == 0
                    || entry.PressureCost > pressureCapacity)
                {
                    reason =
                        $"{channelName}_pressure_cost_invalid:" +
                        $"{entry.ContentId}:{entry.PressureCost}";
                    return false;
                }

                if (float.IsNaN(entry.WarpChargeMultiplier)
                    || float.IsInfinity(entry.WarpChargeMultiplier)
                    || entry.WarpChargeMultiplier < 0f
                    || entry.WarpChargeMultiplier > 1f)
                {
                    reason =
                        $"{channelName}_warp_charge_multiplier_invalid:" +
                        $"{entry.ContentId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool EntriesEqual(
            RunIncidentWeightedEntry[] left,
            RunIncidentWeightedEntry[] right)
        {
            for (var index = 0; index < left.Length; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f
                && !float.IsNaN(value)
                && !float.IsInfinity(value);
        }
    }
}
