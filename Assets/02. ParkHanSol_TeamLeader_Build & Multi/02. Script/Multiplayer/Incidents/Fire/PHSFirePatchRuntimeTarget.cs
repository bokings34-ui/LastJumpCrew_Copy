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

        [Header("Patch Contract")]
        [SerializeField] private PHSFirePatch patch;

        [Header("Local Presentation")]
        [SerializeField] private Light fireLight;

        private readonly List<GameObject> presentationInstances = new();
        private PHSNetworkFireCoordinator owner;
        private string locationId = string.Empty;
        private GameObject presentationPrefab;
        private uint accidentInstanceId;
        private PHSFireIntensity intensity;
        private float baseLightIntensity;
        private float baseLightRange;
        private float flickerOffset;
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
            this.accidentInstanceId = accidentInstanceId;
            this.intensity = intensity;

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
            }
        }

        private void ApplyPresentationState()
        {
            var visibleCount = IsActive
                ? Mathf.Min(
                    (byte)intensity,
                    presentationInstances.Count)
                : 0;
            for (var index = 0;
                index < presentationInstances.Count;
                index++)
            {
                var instance = presentationInstances[index];
                if (instance != null)
                {
                    instance.SetActive(index < visibleCount);
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
            }
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
        }
    }
}
