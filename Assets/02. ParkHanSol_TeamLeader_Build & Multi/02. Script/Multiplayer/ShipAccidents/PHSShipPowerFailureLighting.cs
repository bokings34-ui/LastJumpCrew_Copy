using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSShipPowerFailureLighting : MonoBehaviour
    {
        [SerializeField] private Light[] controlledLights = System.Array.Empty<Light>();
        [SerializeField] private GameObject emergencyLightingRoot;
        [SerializeField, Range(0f, 1f)] private float failureIntensityMultiplier = 0.05f;
        [SerializeField, Range(0f, 1f)] private float failureAmbientIntensityMultiplier = 0.12f;

        private NetworkShipSystemsState boundShipState;
        private float[] normalIntensities = System.Array.Empty<float>();
        private float normalAmbientIntensity;
        private bool failureApplied;
        private float nextBindAttemptTime;

        public bool IsBlackoutApplied => failureApplied;
        public bool IsEmergencyLightingActive => emergencyLightingRoot != null
            && emergencyLightingRoot.activeSelf;
        public float CurrentAmbientIntensityRatio => normalAmbientIntensity <= Mathf.Epsilon
            ? 1f
            : RenderSettings.ambientIntensity / normalAmbientIntensity;

        private void Awake()
        {
            if (controlledLights == null || controlledLights.Length == 0)
            {
                Debug.LogError(
                    $"PHS_POWER_FAILURE_LIGHTING_SETUP_FAILED reason=lights_missing object={name}",
                    this);
                enabled = false;
                return;
            }

            normalIntensities = new float[controlledLights.Length];
            for (var index = 0; index < controlledLights.Length; index++)
            {
                if (controlledLights[index] == null)
                {
                    Debug.LogError(
                        $"PHS_POWER_FAILURE_LIGHTING_SETUP_FAILED reason=light_reference_missing index={index} object={name}",
                        this);
                    enabled = false;
                    return;
                }

                normalIntensities[index] = controlledLights[index].intensity;
            }

            if (emergencyLightingRoot == null)
            {
                Debug.LogError(
                    $"PHS_POWER_FAILURE_LIGHTING_SETUP_FAILED reason=emergency_lighting_missing object={name}",
                    this);
                enabled = false;
                return;
            }

            normalAmbientIntensity = RenderSettings.ambientIntensity;
        }

        private void OnEnable()
        {
            TryBindShipState();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextBindAttemptTime)
            {
                return;
            }

            if (boundShipState == NetworkShipSystemsState.Instance
                && boundShipState != null
                && boundShipState.IsSpawned)
            {
                return;
            }

            nextBindAttemptTime = Time.unscaledTime + 0.25f;
            TryBindShipState();
        }

        private void OnDisable()
        {
            UnbindShipState();
            ApplyFailureState(false);
        }

        private void TryBindShipState()
        {
            var shipState = NetworkShipSystemsState.Instance;
            if (boundShipState != null && boundShipState != shipState)
            {
                UnbindShipState();
            }

            if (shipState == null || !shipState.IsSpawned)
            {
                return;
            }

            if (boundShipState == shipState)
            {
                Refresh();
                return;
            }

            boundShipState = shipState;
            boundShipState.StateChanged += Refresh;
            Refresh();
        }

        private void UnbindShipState()
        {
            if (boundShipState != null)
            {
                boundShipState.StateChanged -= Refresh;
                boundShipState = null;
            }
        }

        private void Refresh()
        {
            ApplyFailureState(boundShipState != null
                && !boundShipState.IsPowerEnabled);
        }

        private void ApplyFailureState(bool active)
        {
            if (failureApplied == active || normalIntensities.Length == 0)
            {
                return;
            }

            failureApplied = active;
            for (var index = 0; index < controlledLights.Length; index++)
            {
                controlledLights[index].intensity = active
                    ? normalIntensities[index] * failureIntensityMultiplier
                    : normalIntensities[index];
            }

            RenderSettings.ambientIntensity = active
                ? normalAmbientIntensity * failureAmbientIntensityMultiplier
                : normalAmbientIntensity;
            emergencyLightingRoot.SetActive(active);
        }
    }
}
