using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace SM
{
    [DisallowMultipleComponent]
    public sealed class TeamPowerOffNetworkVisual : MonoBehaviour
    {
        [SerializeField] private Light[] controlledLights = System.Array.Empty<Light>();
        [SerializeField] private GameObject emergencyLightingRoot;
        [SerializeField] private Light[] emergencyLights = System.Array.Empty<Light>();
        [SerializeField, Range(0f, 1f)] private float blackoutLightMultiplier = 0.05f;
        [SerializeField, Range(0f, 1f)] private float blackoutAmbientMultiplier = 0.12f;

        private NetworkShipSystemsState shipSystems;
        private float[] normalIntensities = System.Array.Empty<float>();
        private float normalAmbientIntensity;
        private float nextBindAttemptTime;
        private bool blackoutApplied;

        public bool IsBlackoutApplied => blackoutApplied;
        public bool IsEmergencyLightingActive
        {
            get
            {
                if (emergencyLightingRoot != null && emergencyLightingRoot.activeSelf)
                {
                    return true;
                }

                for (var index = 0; index < emergencyLights.Length; index++)
                {
                    var emergencyLight = emergencyLights[index];
                    if (emergencyLight != null && emergencyLight.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public float CurrentAmbientIntensityRatio => normalAmbientIntensity <= Mathf.Epsilon
            ? 1f
            : RenderSettings.ambientIntensity / normalAmbientIntensity;

        private void Awake()
        {
            if (controlledLights == null || controlledLights.Length == 0)
            {
                Debug.LogError($"TEAM_POWER_OFF_VISUAL_SETUP_FAILED object={name}", this);
                enabled = false;
                return;
            }

            normalIntensities = new float[controlledLights.Length];
            for (var index = 0; index < controlledLights.Length; index++)
            {
                if (controlledLights[index] == null)
                {
                    Debug.LogError($"TEAM_POWER_OFF_VISUAL_SETUP_FAILED reason=light_missing index={index}", this);
                    enabled = false;
                    return;
                }

                normalIntensities[index] = controlledLights[index].intensity;
            }

            normalAmbientIntensity = RenderSettings.ambientIntensity;
        }

        private void OnEnable()
        {
            TryBindShipSystems();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextBindAttemptTime)
            {
                nextBindAttemptTime = Time.unscaledTime + 0.25f;
                TryBindShipSystems();
            }
        }

        private void OnDisable()
        {
            UnbindShipSystems();
            ApplyPowerState(false);
        }

        private void TryBindShipSystems()
        {
            var candidate = NetworkShipSystemsState.Instance;
            if (shipSystems != null && shipSystems != candidate)
            {
                UnbindShipSystems();
            }

            if (candidate == null || !candidate.IsSpawned)
            {
                return;
            }

            if (shipSystems == candidate)
            {
                ApplyPowerState(!shipSystems.IsPowerEnabled);
                return;
            }

            shipSystems = candidate;
            shipSystems.StateChanged += HandleShipStateChanged;
            ApplyPowerState(!shipSystems.IsPowerEnabled);
        }

        private void UnbindShipSystems()
        {
            if (shipSystems != null)
            {
                shipSystems.StateChanged -= HandleShipStateChanged;
                shipSystems = null;
            }
        }

        private void HandleShipStateChanged()
        {
            ApplyPowerState(shipSystems != null && !shipSystems.IsPowerEnabled);
        }

        private void ApplyPowerState(bool blackout)
        {
            if (blackoutApplied == blackout || normalIntensities.Length == 0)
            {
                return;
            }

            blackoutApplied = blackout;
            for (var index = 0; index < controlledLights.Length; index++)
            {
                var controlledLight = controlledLights[index];
                if (controlledLight == null)
                {
                    continue;
                }

                controlledLight.intensity = blackout
                    ? normalIntensities[index] * blackoutLightMultiplier
                    : normalIntensities[index];
            }

            RenderSettings.ambientIntensity = blackout
                ? normalAmbientIntensity * blackoutAmbientMultiplier
                : normalAmbientIntensity;
            if (emergencyLightingRoot != null)
            {
                emergencyLightingRoot.SetActive(blackout);
            }

            for (var index = 0; index < emergencyLights.Length; index++)
            {
                var emergencyLight = emergencyLights[index];
                if (emergencyLight != null)
                {
                    emergencyLight.gameObject.SetActive(blackout);
                }
            }
        }
    }
}
