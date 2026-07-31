using System;
using System.Collections.Generic;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSPowerFailureRoomController :
        MonoBehaviour,
        IPowerFailureRoom
    {
        [Header("Room")]
        [SerializeField] private ShipRoom room;

        [Header("Presentation")]
        [SerializeField] private Light[] controlledLights = Array.Empty<Light>();
        [SerializeField] private GameObject emergencyLightingRoot;
        [SerializeField] private GameObject installedBatteryVisual;
        [SerializeField, Range(0f, 1f)] private float failureIntensityMultiplier = 0.05f;

        private readonly List<NetworkEventLifecycleSnapshot> snapshots = new();
        private float[] normalIntensities = Array.Empty<float>();
        private NetworkEventCoordinator coordinator;
        private ulong activeEventInstanceId;
        private bool failureApplied;
        private float nextBindAttemptTime;

        public string PowerRoomId => room != null
            ? room.RoomId?.Trim() ?? string.Empty
            : string.Empty;
        public bool IsPowerFailureActive => failureApplied;
        public bool IsBatteryInstalled => !failureApplied;
        public event Action StateChanged;

        private void Awake()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_ROOM_POWER_SETUP_FAILED reason={reason} target={name}",
                    this);
                enabled = false;
                return;
            }

            normalIntensities = new float[controlledLights.Length];
            for (var index = 0; index < controlledLights.Length; index++)
            {
                normalIntensities[index] = controlledLights[index].intensity;
            }

            ApplyPresentation(false);
        }

        private void OnEnable()
        {
            TryBindCoordinator();
        }

        private void Update()
        {
            if (coordinator != null || Time.unscaledTime < nextBindAttemptTime)
            {
                return;
            }

            nextBindAttemptTime = Time.unscaledTime + 0.25f;
            TryBindCoordinator();
        }

        private void OnDisable()
        {
            UnbindCoordinator();
            activeEventInstanceId = 0UL;
            ApplyPresentation(false);
        }

        public bool TrySetPowerFailure(
            bool active,
            ulong eventInstanceId,
            out string reason)
        {
            if (!enabled)
            {
                reason = "room_power_controller_disabled";
                return false;
            }

            if (eventInstanceId == 0UL)
            {
                reason = "event_instance_id_missing";
                return false;
            }

            if (!active
                && activeEventInstanceId != 0UL
                && activeEventInstanceId != eventInstanceId)
            {
                reason = $"event_instance_mismatch:{activeEventInstanceId}";
                return false;
            }

            activeEventInstanceId = active ? eventInstanceId : 0UL;
            ApplyPresentation(active);
            reason = null;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                reason = "ship_room_missing";
                return false;
            }

            if (controlledLights == null || controlledLights.Length == 0)
            {
                reason = "controlled_lights_missing";
                return false;
            }

            foreach (var controlledLight in controlledLights)
            {
                if (controlledLight == null)
                {
                    reason = "controlled_light_reference_missing";
                    return false;
                }
            }

            if (emergencyLightingRoot == null)
            {
                reason = "emergency_lighting_root_missing";
                return false;
            }

            if (installedBatteryVisual == null)
            {
                reason = "installed_battery_visual_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void TryBindCoordinator()
        {
            var candidate = NetworkEventCoordinator.Instance;
            if (candidate == null || !candidate.IsSpawned)
            {
                return;
            }

            if (coordinator == candidate)
            {
                RefreshFromSnapshots();
                return;
            }

            UnbindCoordinator();
            coordinator = candidate;
            coordinator.LifecycleSnapshotsChanged += RefreshFromSnapshots;
            RefreshFromSnapshots();
        }

        private void UnbindCoordinator()
        {
            if (coordinator != null)
            {
                coordinator.LifecycleSnapshotsChanged -= RefreshFromSnapshots;
                coordinator = null;
            }
        }

        private void RefreshFromSnapshots()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.CopySnapshotsTo(snapshots);
            var matchingInstanceId = 0UL;
            foreach (var snapshot in snapshots)
            {
                if (!snapshot.IsTerminal
                    && snapshot.EventId == EventId.PowerOff
                    && snapshot.RoomId.ToString() == PowerRoomId)
                {
                    matchingInstanceId = snapshot.InstanceId;
                    break;
                }
            }

            activeEventInstanceId = matchingInstanceId;
            ApplyPresentation(matchingInstanceId != 0UL);
        }

        private void ApplyPresentation(bool active)
        {
            var changed = failureApplied != active;
            failureApplied = active;
            for (var index = 0; index < controlledLights.Length; index++)
            {
                if (controlledLights[index] != null
                    && index < normalIntensities.Length)
                {
                    controlledLights[index].intensity = active
                        ? normalIntensities[index] * failureIntensityMultiplier
                        : normalIntensities[index];
                }
            }

            if (emergencyLightingRoot != null)
            {
                emergencyLightingRoot.SetActive(active);
            }

            if (installedBatteryVisual != null)
            {
                installedBatteryVisual.SetActive(!active);
            }
            if (changed)
            {
                StateChanged?.Invoke();
                Debug.Log(
                    $"PHS_ROOM_POWER_STATE room={PowerRoomId} failure={active} event={activeEventInstanceId}",
                    this);
            }
        }
    }
}
