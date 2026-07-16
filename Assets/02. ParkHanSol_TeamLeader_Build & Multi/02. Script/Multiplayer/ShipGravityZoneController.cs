using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ShipGravityZoneController : MonoBehaviour
    {
        [Header("Inspector-wired Gravity Areas")]
        [SerializeField] private NetworkPlayerGravityArea[] shipInteriorAreas;
        [SerializeField] private GravityZone[] gravityZones;

        private NetworkShipSystemsState boundShipState;
        private float nextBindAttemptTime;

        public event Action<bool> GravityStateChanged;

        public bool IsGravityEnabled { get; private set; } = true;

        private void OnEnable()
        {
            TryBindShipState();
        }

        private void OnDisable()
        {
            UnbindShipState();
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

        public void SetGravityEnabled(bool isEnabled)
        {
            var shipState = NetworkShipSystemsState.Instance;
            if (shipState == null || !shipState.IsSpawned)
            {
                ApplyGravityState(isEnabled, "offline");
                return;
            }

            if (!shipState.IsServer)
            {
                Debug.LogError(
                    $"PHS_SHIP_GRAVITY_SET_FAILED reason=server_required controller={name}",
                    this);
                return;
            }

            if (!shipState.TrySetGravityEnabled(isEnabled, out var reason)
                && reason != "gravity_state_unchanged")
            {
                Debug.LogWarning(
                    $"PHS_SHIP_GRAVITY_SET_FAILED reason={reason} controller={name}",
                    this);
            }
        }

        public void TurnGravityOn()
        {
            SetGravityEnabled(true);
        }

        public void TurnGravityOff()
        {
            SetGravityEnabled(false);
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
                ApplyGravityState(boundShipState.IsGravityEnabled, "rebind_refresh");
                return;
            }

            UnbindShipState();
            boundShipState = shipState;
            boundShipState.StateChanged += HandleShipStateChanged;
            ApplyGravityState(boundShipState.IsGravityEnabled, "initial_snapshot");
            Debug.Log(
                $"PHS_SHIP_GRAVITY_BOUND revision={boundShipState.Revision} controller={name}",
                this);
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
            if (boundShipState != null)
            {
                ApplyGravityState(boundShipState.IsGravityEnabled, "replicated_snapshot");
            }
        }

        private void ApplyGravityState(bool isEnabled, string reason)
        {
            var hasLegacyAreas = shipInteriorAreas != null && shipInteriorAreas.Length > 0;
            var hasGravityZones = gravityZones != null && gravityZones.Length > 0;
            if (!hasLegacyAreas && !hasGravityZones)
            {
                Debug.LogError(
                    $"PHS_SHIP_GRAVITY_APPLY_FAILED reason=areas_missing controller={name}",
                    this);
                return;
            }

            if (hasLegacyAreas)
            {
                foreach (var area in shipInteriorAreas)
                {
                    if (area == null)
                    {
                        Debug.LogError(
                            $"PHS_SHIP_GRAVITY_APPLY_FAILED reason=area_missing controller={name}",
                            this);
                        continue;
                    }

                    area.SetShipGravityEnabled(isEnabled);
                }
            }

            if (hasGravityZones)
            {
                foreach (var zone in gravityZones)
                {
                    if (zone == null)
                    {
                        Debug.LogError(
                            $"PHS_SHIP_GRAVITY_APPLY_FAILED reason=zone_missing controller={name}",
                            this);
                        continue;
                    }

                    zone.SetShipGravityEnabled(isEnabled);
                }
            }

            var changed = IsGravityEnabled != isEnabled;
            IsGravityEnabled = isEnabled;
            if (changed)
            {
                GravityStateChanged?.Invoke(isEnabled);
            }

            Debug.Log(
                $"PHS_SHIP_GRAVITY_APPLIED enabled={isEnabled} reason={reason} controller={name}",
                this);
        }
    }
}
