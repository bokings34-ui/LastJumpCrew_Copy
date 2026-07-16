using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PHSShipPowerVisualController : MonoBehaviour
    {
        [Header("Inspector References")]
        [SerializeField] private Light shipInteriorKeyLight;

        [Header("Blackout Visual")]
        [SerializeField, Range(0.1f, 1f)] private float blackoutLightMultiplier = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float blackoutAmbientMultiplier = 0.6f;

        private NetworkShipSystemsState boundShipState;
        private float normalKeyLightIntensity;
        private float normalAmbientIntensity;
        private float nextBindAttemptTime;

        private void Awake()
        {
            if (shipInteriorKeyLight == null)
            {
                Debug.LogError($"PHS_POWER_VISUAL_SETUP_FAILED reason=interior_key_light_missing controller={name}", this);
                enabled = false;
                return;
            }

            normalKeyLightIntensity = shipInteriorKeyLight.intensity;
            normalAmbientIntensity = RenderSettings.ambientIntensity;
        }

        private void OnEnable()
        {
            TryBindShipState();
        }

        private void OnDisable()
        {
            UnbindShipState();
            ApplyPowerVisual(true);
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
                ApplyPowerVisual(boundShipState.IsPowerEnabled);
                return;
            }

            boundShipState = shipState;
            boundShipState.StateChanged += HandleShipStateChanged;
            ApplyPowerVisual(boundShipState.IsPowerEnabled);
        }

        private void UnbindShipState()
        {
            if (boundShipState == null)
            {
                return;
            }

            boundShipState.StateChanged -= HandleShipStateChanged;
            boundShipState = null;
        }

        private void HandleShipStateChanged()
        {
            ApplyPowerVisual(boundShipState != null && boundShipState.IsPowerEnabled);
        }

        private void ApplyPowerVisual(bool powerEnabled)
        {
            if (shipInteriorKeyLight == null)
            {
                return;
            }

            shipInteriorKeyLight.intensity = powerEnabled
                ? normalKeyLightIntensity
                : normalKeyLightIntensity * blackoutLightMultiplier;
            RenderSettings.ambientIntensity = powerEnabled
                ? normalAmbientIntensity
                : normalAmbientIntensity * blackoutAmbientMultiplier;
        }
    }
}
