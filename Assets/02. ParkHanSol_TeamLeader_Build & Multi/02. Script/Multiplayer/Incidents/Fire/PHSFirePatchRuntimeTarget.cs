using System.Collections.Generic;
using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSFirePatchRuntimeTarget :
        MonoBehaviour,
        IUtilityAttackTarget
    {
        private const float FlickerSpeed = 7f;
        private const float MinimumFlicker = 0.85f;
        private const float MaximumFlicker = 1.15f;
        private const float IntensityScalePerLevel = 0.2f;
        private const float RangeScalePerLevel = 0.1f;
        private const float PresentationGrowSeconds = 0.45f;
        private const float MinimumCoverageScale = 1.1f;
        private const float CoverageScalePerPatchWidth = 0.36f;

        [Header("Patch Contract")]
        [SerializeField] private PHSFirePatch patch;

        [Header("Local Presentation")]
        [SerializeField] private Light fireLight;

        private readonly List<GameObject> presentationInstances = new();
        private readonly List<PHSTeamFirePatchPresentationAdapter>
            presentationAdapters = new();
        private readonly Dictionary<ushort, GameObject> spreadBridgeInstances =
            new();
        private readonly Dictionary<
            ushort,
            PHSTeamFirePatchPresentationAdapter> spreadBridgeAdapters =
            new();
        private PHSNetworkFireCoordinator owner;
        private string locationId = string.Empty;
        private GameObject presentationPrefab;
        private uint accidentInstanceId;
        private PHSFireIntensity intensity;
        private float baseLightIntensity;
        private float baseLightRange;
        private float flickerOffset;
        private float presentationActivatedAt;
        private bool lightStateCached;

        public PHSFirePatch Patch => patch;
        public Light FireLight => fireLight;
        public uint AccidentInstanceId => accidentInstanceId;
        public string LocationId => locationId;
        public PHSFireIntensity Intensity => intensity;
        public bool IsActive =>
            accidentInstanceId != 0U
            && intensity != PHSFireIntensity.None;

        private void Awake()
        {
            CacheLightState();
            ApplyPresentationState();
        }

        private void Update()
        {
            if (!IsActive || fireLight == null)
            {
                return;
            }

            CacheLightState();
            var level = Mathf.Max(1, (byte)intensity);
            var intensityScale =
                1f + ((level - 1) * IntensityScalePerLevel);
            var rangeScale =
                1f + ((level - 1) * RangeScalePerLevel);
            var flicker = Mathf.Lerp(
                MinimumFlicker,
                MaximumFlicker,
                Mathf.PerlinNoise(
                    flickerOffset,
                    Time.unscaledTime * FlickerSpeed));

            fireLight.intensity = baseLightIntensity
                * intensityScale
                * flicker;
            fireLight.range = baseLightRange
                * rangeScale
                * Mathf.Lerp(
                    0.95f,
                    1.05f,
                    Mathf.InverseLerp(
                        MinimumFlicker,
                        MaximumFlicker,
                        flicker));
            RefreshSpreadBridges();
        }

        internal void Bind(
            PHSNetworkFireCoordinator owner,
            string locationId,
            GameObject presentationPrefab)
        {
            var normalizedLocationId = locationId?.Trim() ?? string.Empty;
            if (this.presentationPrefab != presentationPrefab)
            {
                DestroyPresentationInstances();
                DestroySpreadBridges();
            }

            this.owner = owner;
            this.locationId = normalizedLocationId;
            this.presentationPrefab = presentationPrefab;
            CacheLightState();
            ApplyPresentationState();
        }

        public void ApplySnapshot(
            uint accidentInstanceId,
            PHSFireIntensity intensity)
        {
            var wasActive = IsActive;
            var previousAccidentInstanceId = this.accidentInstanceId;
            this.accidentInstanceId = accidentInstanceId;
            this.intensity = intensity;

            if (IsActive && (!wasActive
                || previousAccidentInstanceId != accidentInstanceId))
            {
                presentationActivatedAt = Time.unscaledTime;
            }

            if (IsActive)
            {
                EnsurePresentationInstances();
            }

            ApplyPresentationState();
        }

        public void ClearSnapshot()
        {
            accidentInstanceId = 0U;
            intensity = PHSFireIntensity.None;
            SetSpreadBridgesActive(false);
            ApplyPresentationState();
        }

        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            if (!IsActive
                || owner == null
                || patch == null
                || string.IsNullOrWhiteSpace(locationId))
            {
                return false;
            }

            return owner.TrySuppressPatchServer(
                accidentInstanceId,
                locationId,
                patch.PatchId,
                hit,
                out _);
        }

        public bool TryValidate(out string reason)
        {
            if (patch == null)
            {
                reason = "patch_missing";
                return false;
            }

            if (!patch.TryValidate(out var patchReason))
            {
                reason = $"patch_invalid:{patchReason}";
                return false;
            }

            if (fireLight == null)
            {
                reason = "fire_light_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void EnsurePresentationInstances()
        {
            if (presentationInstances.Count > 0
                || presentationPrefab == null
                || patch == null)
            {
                return;
            }

            foreach (var socket in patch.VisualSockets)
            {
                if (socket == null)
                {
                    continue;
                }

                var instance = Instantiate(
                    presentationPrefab,
                    socket,
                    false);
                instance.SetActive(false);
                presentationInstances.Add(instance);
                presentationAdapters.Add(
                    instance.GetComponent<
                        PHSTeamFirePatchPresentationAdapter>());
            }
        }

        private void ApplyPresentationState()
        {
            var coverageScale = GetCoverageScale();
            var intensityScale = GetIntensityPresentationScale();
            var growScale = IsActive
                ? Mathf.SmoothStep(
                    0.18f,
                    1f,
                    Mathf.Clamp01(
                        (Time.unscaledTime - presentationActivatedAt)
                        / PresentationGrowSeconds))
                : 0f;
            for (var index = 0;
                index < presentationInstances.Count;
                index++)
            {
                var instance = presentationInstances[index];
                if (instance != null)
                {
                    // Keep all lobes alive from Small intensity onward. Their
                    // scale, rather than a hard 1/2/3 socket toggle, makes a
                    // patch read as a growing flame area.
                    instance.SetActive(IsActive);
                    if (index < presentationAdapters.Count
                        && presentationAdapters[index] != null)
                    {
                        presentationAdapters[index].ApplyState(
                            intensity,
                            index == 0);
                    }

                    var variation = index == 1 ? 1f : 0.9f;
                    instance.transform.localScale =
                        presentationPrefab.transform.localScale
                        * coverageScale
                        * intensityScale
                        * growScale
                        * variation;
                }
            }

            if (fireLight == null)
            {
                return;
            }

            CacheLightState();
            fireLight.enabled = IsActive;
            if (!IsActive)
            {
                fireLight.intensity = baseLightIntensity;
                fireLight.range = baseLightRange;
                SetSpreadBridgesActive(false);
            }
        }

        private float GetCoverageScale()
        {
            if (patch == null || patch.HazardBounds == null)
            {
                return MinimumCoverageScale;
            }

            var size = patch.HazardBounds.bounds.size;
            return Mathf.Max(
                MinimumCoverageScale,
                Mathf.Max(size.x, size.z)
                    * CoverageScalePerPatchWidth);
        }

        private float GetIntensityPresentationScale()
        {
            return intensity switch
            {
                PHSFireIntensity.Small => 0.72f,
                PHSFireIntensity.Medium => 0.92f,
                PHSFireIntensity.Large => 1.1f,
                _ => 0f
            };
        }

        private void RefreshSpreadBridges()
        {
            if (!IsActive
                || owner == null
                || patch == null
                || presentationPrefab == null
                || string.IsNullOrWhiteSpace(locationId))
            {
                SetSpreadBridgesActive(false);
                return;
            }

            foreach (var link in patch.Neighbors)
            {
                var neighbor = link.Target;
                if (neighbor == null || patch.PatchId >= neighbor.PatchId)
                {
                    continue;
                }

                var isNeighborBurning = owner.IsPatchBurning(
                    locationId,
                    neighbor.PatchId);
                if (!spreadBridgeInstances.TryGetValue(
                        neighbor.PatchId,
                        out var bridge)
                    || bridge == null)
                {
                    if (!isNeighborBurning)
                    {
                        continue;
                    }

                    bridge = Instantiate(
                        presentationPrefab,
                        patch.PresentationRoot,
                        false);
                    bridge.SetActive(false);
                    bridge.name =
                        $"PHS_FireSpreadBridge_{patch.PatchId}_{neighbor.PatchId}";
                    spreadBridgeInstances[neighbor.PatchId] = bridge;
                    spreadBridgeAdapters[neighbor.PatchId] =
                        bridge.GetComponent<
                            PHSTeamFirePatchPresentationAdapter>();
                }

                var wasActive = bridge.activeSelf;
                bridge.SetActive(isNeighborBurning);
                if (!isNeighborBurning)
                {
                    continue;
                }

                if (!wasActive
                    && spreadBridgeAdapters.TryGetValue(
                        neighbor.PatchId,
                        out var bridgeAdapter)
                    && bridgeAdapter != null)
                {
                    bridgeAdapter.ApplyState(
                        PHSFireIntensity.Small,
                        false);
                }

                var start = patch.PresentationRoot.position;
                var end = neighbor.PresentationRoot.position;
                bridge.transform.position = Vector3.Lerp(start, end, 0.5f);
                var bridgeScale = Mathf.Max(
                    0.85f,
                    Vector3.Distance(start, end) * 0.32f);
                bridge.transform.localScale =
                    presentationPrefab.transform.localScale
                    * bridgeScale;
            }
        }

        private void SetSpreadBridgesActive(bool active)
        {
            foreach (var bridge in spreadBridgeInstances.Values)
            {
                if (bridge != null)
                {
                    bridge.SetActive(active);
                }
            }
        }

        private void DestroySpreadBridges()
        {
            foreach (var bridge in spreadBridgeInstances.Values)
            {
                if (bridge != null)
                {
                    Destroy(bridge);
                }
            }

            spreadBridgeInstances.Clear();
            spreadBridgeAdapters.Clear();
        }

        private void CacheLightState()
        {
            if (lightStateCached || fireLight == null)
            {
                return;
            }

            baseLightIntensity = fireLight.intensity;
            baseLightRange = fireLight.range;
            flickerOffset = patch != null
                ? patch.PatchId * 0.173f
                : GetEntityId().GetHashCode() * 0.001f;
            lightStateCached = true;
        }

        private void DestroyPresentationInstances()
        {
            foreach (var instance in presentationInstances)
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                    Destroy(instance);
                }
            }

            presentationInstances.Clear();
            presentationAdapters.Clear();
        }
    }
}
