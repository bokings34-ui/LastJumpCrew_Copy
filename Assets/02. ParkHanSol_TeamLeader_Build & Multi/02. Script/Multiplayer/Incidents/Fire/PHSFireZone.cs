using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSFireZone : MonoBehaviour, IFireSpreadSurface
    {
        private const float BoundsContainmentEpsilon = 0.01f;

        [Header("Incident References")]
        [SerializeField] private PHSShipIncidentZone incidentZone;
        [SerializeField] private PHSShipAccidentAnchor fireAccidentAnchor;
        [SerializeField] private PHSFirePatch[] patches =
            Array.Empty<PHSFirePatch>();

        [Header("Spread Limits")]
        [SerializeField] private byte maximumBurningPatches = 8;
        [SerializeField] private ushort initialHeat = 70;
        [SerializeField] private ushort maximumHeat =
            PHSFireIntensityUtility.MaximumHeat;
        [SerializeField] private ushort minimumHeatGrowthPerTick = 8;
        [SerializeField] private ushort maximumHeatGrowthPerTick = 18;
        [SerializeField, Min(0.01f)] private float spreadTickSeconds = 2.5f;
        [SerializeField] private byte spreadAttemptsPerTick = 2;
        [SerializeField] private byte maximumNewIgnitionsPerTick = 1;
        [SerializeField, Range(0f, 1f)] private float baseSpreadChance = 0.45f;

        [Header("Damage Collection")]
        [SerializeField, Min(0.05f)] private float damageTickSeconds = 1f;
        [SerializeField, Min(1)] private int baseDamagePerTick = 2;
        [SerializeField] private LayerMask damageableLayers = ~0;

        [Header("Suppression And Presentation")]
        [SerializeField, Min(0.1f)]
        private float containmentGraceSeconds = 2.5f;
        [SerializeField] private GameObject patchPresentationPrefab;

        private readonly Dictionary<ushort, PHSFirePatch> patchesById = new();
        private readonly List<PHSFirePatch> orderedPatches = new();
        private bool setupValid;

        public PHSShipIncidentZone IncidentZone => incidentZone;
        public PHSShipAccidentAnchor FireAccidentAnchor => fireAccidentAnchor;
        public IReadOnlyList<PHSFirePatch> Patches =>
            patches ?? Array.Empty<PHSFirePatch>();
        public byte MaximumBurningPatches => maximumBurningPatches;
        public ushort InitialHeat => initialHeat;
        public ushort MaximumHeat => maximumHeat;
        public ushort MinimumHeatGrowthPerTick =>
            minimumHeatGrowthPerTick;
        public ushort MaximumHeatGrowthPerTick =>
            maximumHeatGrowthPerTick;
        public float SpreadTickSeconds => spreadTickSeconds;
        public byte SpreadAttemptsPerTick => spreadAttemptsPerTick;
        public byte MaximumNewIgnitionsPerTick => maximumNewIgnitionsPerTick;
        public float BaseSpreadChance => baseSpreadChance;
        public float DamageTickSeconds => damageTickSeconds;
        public int BaseDamagePerTick => baseDamagePerTick;
        public float ContainmentGraceSeconds =>
            containmentGraceSeconds;
        public LayerMask DamageableLayers => damageableLayers;
        public GameObject PatchPresentationPrefab => patchPresentationPrefab;
        public bool IsReady => setupValid;

        private void OnValidate()
        {
            setupValid = false;
        }

        public bool TryResolvePatch(
            ushort patchId,
            out PHSFirePatch patch)
        {
            if (EnsureReady(out _)
                && patchId != 0
                && patchesById.TryGetValue(patchId, out patch))
            {
                return true;
            }

            patch = null;
            return false;
        }

        public bool TryCopyOrderedPatches(
            List<PHSFirePatch> destination,
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

            destination.AddRange(orderedPatches);
            reason = null;
            return true;
        }

        public bool TryCopySpreadCandidates(
            ushort sourcePatchId,
            PHSFireIntensity sourceIntensity,
            List<PHSFirePatchLink> destination,
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

            if (!patchesById.TryGetValue(sourcePatchId, out var source))
            {
                reason = $"source_patch_missing:{sourcePatchId}";
                return false;
            }

            foreach (var link in source.Neighbors)
            {
                if (link.IsEligible(sourceIntensity))
                {
                    destination.Add(link);
                }
            }

            destination.Sort(
                (left, right) => left.Target.PatchId.CompareTo(
                    right.Target.PatchId));
            reason = null;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            setupValid = RebuildCache(out reason);
            return setupValid;
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
            patchesById.Clear();
            orderedPatches.Clear();

            if (incidentZone == null)
            {
                reason = "incident_zone_missing";
                return false;
            }

            if (!incidentZone.TryValidate(out var zoneReason))
            {
                reason = $"incident_zone_invalid:{zoneReason}";
                return false;
            }

            if (fireAccidentAnchor == null)
            {
                reason = "fire_accident_anchor_missing";
                return false;
            }

            if (patches == null || patches.Length == 0)
            {
                reason = "patches_empty";
                return false;
            }

            if (patches.Length > ushort.MaxValue)
            {
                reason = $"patch_count_exceeds_id_range:{patches.Length}";
                return false;
            }

            if (maximumBurningPatches == 0
                || maximumBurningPatches > patches.Length)
            {
                reason =
                    $"maximum_burning_patches_invalid:{maximumBurningPatches}:{patches.Length}";
                return false;
            }

            if (initialHeat == 0
                || maximumHeat == 0
                || initialHeat > maximumHeat
                || maximumHeat
                    > PHSFireIntensityUtility.MaximumHeat)
            {
                reason =
                    $"fire_heat_invalid:{initialHeat}:{maximumHeat}";
                return false;
            }

            if (minimumHeatGrowthPerTick == 0
                || maximumHeatGrowthPerTick == 0
                || minimumHeatGrowthPerTick
                    > maximumHeatGrowthPerTick
                || maximumHeatGrowthPerTick > maximumHeat)
            {
                reason =
                    $"fire_heat_growth_invalid:" +
                    $"{minimumHeatGrowthPerTick}:" +
                    $"{maximumHeatGrowthPerTick}:" +
                    $"{maximumHeat}";
                return false;
            }

            if (spreadTickSeconds <= 0f
                || float.IsNaN(spreadTickSeconds)
                || float.IsInfinity(spreadTickSeconds))
            {
                reason = $"spread_tick_seconds_invalid:{spreadTickSeconds}";
                return false;
            }

            if (spreadAttemptsPerTick == 0)
            {
                reason = "spread_attempts_per_tick_invalid:0";
                return false;
            }

            if (maximumNewIgnitionsPerTick == 0
                || maximumNewIgnitionsPerTick > spreadAttemptsPerTick
                || maximumNewIgnitionsPerTick > maximumBurningPatches)
            {
                reason =
                    $"maximum_new_ignitions_per_tick_invalid:{maximumNewIgnitionsPerTick}";
                return false;
            }

            if (baseSpreadChance < 0f
                || baseSpreadChance > 1f
                || float.IsNaN(baseSpreadChance)
                || float.IsInfinity(baseSpreadChance))
            {
                reason = $"base_spread_chance_invalid:{baseSpreadChance}";
                return false;
            }

            if (damageTickSeconds <= 0f
                || float.IsNaN(damageTickSeconds)
                || float.IsInfinity(damageTickSeconds))
            {
                reason = $"damage_tick_seconds_invalid:{damageTickSeconds}";
                return false;
            }

            if (baseDamagePerTick <= 0)
            {
                reason = $"base_damage_per_tick_invalid:{baseDamagePerTick}";
                return false;
            }

            if (damageableLayers.value == 0)
            {
                reason = "damageable_layers_empty";
                return false;
            }

            if (containmentGraceSeconds <= 0f
                || float.IsNaN(containmentGraceSeconds)
                || float.IsInfinity(containmentGraceSeconds))
            {
                reason =
                    $"containment_grace_seconds_invalid:" +
                    $"{containmentGraceSeconds}";
                return false;
            }

            if (patchPresentationPrefab == null)
            {
                reason = "patch_presentation_prefab_missing";
                return false;
            }

            foreach (var patch in patches)
            {
                if (patch == null)
                {
                    reason = "patch_missing";
                    return false;
                }

                if (!patch.TryValidate(out var patchReason))
                {
                    reason = $"patch_invalid:{patch.PatchId}:{patchReason}";
                    return false;
                }

                var runtimeTargets =
                    patch.GetComponents<PHSFirePatchRuntimeTarget>();
                if (runtimeTargets.Length != 1)
                {
                    reason =
                        $"patch_runtime_target_invalid:{patch.PatchId}:" +
                        $"count={runtimeTargets.Length}";
                    return false;
                }

                var runtimeTarget = runtimeTargets[0];
                if (runtimeTarget.Patch != patch)
                {
                    reason =
                        $"patch_runtime_target_invalid:{patch.PatchId}:" +
                        "patch_mismatch";
                    return false;
                }

                if (runtimeTarget.FireLight == null
                    || (runtimeTarget.FireLight.transform
                            != patch.PresentationRoot
                        && !runtimeTarget.FireLight.transform.IsChildOf(
                            patch.PresentationRoot)))
                {
                    reason =
                        $"patch_runtime_target_invalid:{patch.PatchId}:" +
                        "light_outside_presentation_root";
                    return false;
                }

                if (!runtimeTarget.TryValidate(out var targetReason))
                {
                    reason =
                        $"patch_runtime_target_invalid:{patch.PatchId}:" +
                        $"{targetReason}";
                    return false;
                }

                if (!ContainsWorldBounds(
                        incidentZone.ZoneBounds.bounds,
                        patch.HazardBounds.bounds,
                        BoundsContainmentEpsilon))
                {
                    reason =
                        $"patch_outside_incident_zone:{patch.PatchId}:{incidentZone.ZoneId}";
                    return false;
                }

                if (!patchesById.TryAdd(patch.PatchId, patch))
                {
                    reason = $"patch_id_duplicate:{patch.PatchId}";
                    return false;
                }

                orderedPatches.Add(patch);
            }

            var registeredPatches = new HashSet<PHSFirePatch>(patches);
            foreach (var patch in orderedPatches)
            {
                foreach (var link in patch.Neighbors)
                {
                    if (!registeredPatches.Contains(link.Target))
                    {
                        reason =
                            $"cross_zone_link_not_supported:{patch.PatchId}:{link.Target.PatchId}";
                        return false;
                    }

                    if (!link.OneWay
                        && !HasLinkTo(link.Target, patch))
                    {
                        reason =
                            $"reciprocal_link_missing:{patch.PatchId}:{link.Target.PatchId}";
                        return false;
                    }
                }
            }

            orderedPatches.Sort(
                (left, right) => left.PatchId.CompareTo(right.PatchId));
            reason = null;
            return true;
        }

        private static bool HasLinkTo(
            PHSFirePatch source,
            PHSFirePatch target)
        {
            foreach (var candidate in source.Neighbors)
            {
                if (candidate != null && candidate.Target == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWorldBounds(
            Bounds container,
            Bounds candidate,
            float epsilon)
        {
            var allowedMinimum =
                container.min - (Vector3.one * epsilon);
            var allowedMaximum =
                container.max + (Vector3.one * epsilon);
            var candidateMinimum = candidate.min;
            var candidateMaximum = candidate.max;
            for (var cornerIndex = 0;
                 cornerIndex < 8;
                 cornerIndex++)
            {
                var corner = new Vector3(
                    (cornerIndex & 1) == 0
                        ? candidateMinimum.x
                        : candidateMaximum.x,
                    (cornerIndex & 2) == 0
                        ? candidateMinimum.y
                        : candidateMaximum.y,
                    (cornerIndex & 4) == 0
                        ? candidateMinimum.z
                        : candidateMaximum.z);
                if (corner.x < allowedMinimum.x
                    || corner.x > allowedMaximum.x
                    || corner.y < allowedMinimum.y
                    || corner.y > allowedMaximum.y
                    || corner.z < allowedMinimum.z
                    || corner.z > allowedMaximum.z)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
