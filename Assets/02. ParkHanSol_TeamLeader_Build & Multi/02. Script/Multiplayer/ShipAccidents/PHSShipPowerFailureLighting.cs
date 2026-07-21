using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSShipPowerFailureLighting : MonoBehaviour
    {
        [SerializeField] private PHSNetworkShipAccidentCoordinator coordinator;
        [SerializeField] private Light[] controlledLights = System.Array.Empty<Light>();
        [SerializeField] private GameObject emergencyLightingRoot;
        [SerializeField, Range(0f, 1f)] private float failureIntensityMultiplier = 0.05f;

        private float[] normalIntensities = System.Array.Empty<float>();
        private bool failureApplied;

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
        }

        private void OnEnable()
        {
            if (coordinator == null)
            {
                Debug.LogError(
                    $"PHS_POWER_FAILURE_LIGHTING_SETUP_FAILED reason=coordinator_missing object={name}",
                    this);
                enabled = false;
                return;
            }

            coordinator.ActiveAccidentsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.ActiveAccidentsChanged -= Refresh;
            }

            ApplyFailureState(false);
        }

        private void Refresh()
        {
            var hasPowerFailure = false;
            if (coordinator != null && coordinator.IsSpawned)
            {
                for (var index = 0; index < coordinator.ActiveAccidentCount; index++)
                {
                    if (coordinator.GetActiveAccidentAt(index).AccidentId
                        == PHSShipAccidentId.PowerFailure)
                    {
                        hasPowerFailure = true;
                        break;
                    }
                }
            }

            ApplyFailureState(hasPowerFailure);
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

            if (emergencyLightingRoot != null)
            {
                emergencyLightingRoot.SetActive(active);
            }
        }
    }
}
