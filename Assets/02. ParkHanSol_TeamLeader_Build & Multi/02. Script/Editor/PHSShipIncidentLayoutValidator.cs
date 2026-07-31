#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHSShipIncidentLayoutValidator
    {
        [MenuItem("Tools/PHS/Validate Ship Incident Layout")]
        public static void Validate()
        {
            var errors = new List<string>();
            var rooms = UnityEngine.Object.FindObjectsByType<ShipRoom>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (rooms.Length == 0)
            {
                errors.Add("ship_rooms_missing");
            }

            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                ValidateRoom(room, roomIds, errors);
            }

            var engineTargets = UnityEngine.Object.FindObjectsByType<
                    PHSEngineBreakRepairTarget>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(target => target.gameObject.scene
                    == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                .ToArray();
            foreach (var target in engineTargets)
            {
                if (!target.TryValidate(out var targetReason))
                {
                    errors.Add($"engine_target_invalid:{GetPath(target.transform)}:{targetReason}");
                }
            }

            foreach (var reactor in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None)
                     .Where(transform => transform.name == "Reactor_ColdFusion_01"))
            {
                if (reactor.GetComponent<PHSEngineBreakRepairTarget>() == null)
                {
                    errors.Add($"reactor_repair_target_missing:{GetPath(reactor)}");
                }
            }

            foreach (var zone in UnityEngine.Object.FindObjectsByType<
                         PHSOxygenDeprivationZone>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (zone.GetComponentInParent<ShipRoom>(true) == null)
                {
                    errors.Add($"oxygen_zone_outside_ship_room:{zone.name}");
                }
            }

            if (engineTargets.Length == 0)
            {
                errors.Add("engine_repair_targets_missing");
            }

            ValidateScheduler(errors);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"PHS_SHIP_INCIDENT_LAYOUT_INVALID reason={error}");
                }

                Debug.LogError(
                    $"PHS_SHIP_INCIDENT_LAYOUT_FAILED errors={errors.Count} rooms={rooms.Length}");
                return;
            }

            Debug.Log(
                $"PHS_SHIP_INCIDENT_LAYOUT_OK rooms={rooms.Length} engineTargets={engineTargets.Length}");
        }

        private static void ValidateRoom(
            ShipRoom room,
            HashSet<string> roomIds,
            List<string> errors)
        {
            var roomId = room.RoomId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                errors.Add($"room_id_missing:{room.name}");
                return;
            }

            if (!roomIds.Add(roomId))
            {
                errors.Add($"room_id_duplicate:{roomId}");
            }

            var oxygenProviders = room.GetComponents<PHSOxygenLeakZoneProvider>();
            if (oxygenProviders.Length != 1)
            {
                errors.Add($"oxygen_provider_count:{roomId}:{oxygenProviders.Length}");
            }
            else if (!oxygenProviders[0].TryValidate(out var oxygenReason))
            {
                errors.Add($"oxygen_provider_invalid:{roomId}:{oxygenReason}");
            }

            var oxygenZones = room.GetComponentsInChildren<
                PHSOxygenDeprivationZone>(true);
            if (oxygenZones.Length < 2)
            {
                errors.Add($"oxygen_pipe_variety_insufficient:{roomId}:{oxygenZones.Length}");
            }

            var powerControllers = room.GetComponents<
                PHSPowerFailureRoomController>();
            if (powerControllers.Length != 1)
            {
                errors.Add($"power_controller_count:{roomId}:{powerControllers.Length}");
            }
            else
            {
                var powerController = powerControllers[0];
                if (!powerController.TryValidate(out var powerReason))
                {
                    errors.Add($"power_controller_invalid:{roomId}:{powerReason}");
                }

                var sockets = UnityEngine.Object.FindObjectsByType<
                        BatteryInsertPowerStationSocket>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Where(socket => socket.gameObject.scene == room.gameObject.scene);
                var matchingSockets = sockets.Count(
                    socket => socket.RoomPowerController == powerController);
                if (matchingSockets != 1)
                {
                    errors.Add(
                        $"room_battery_socket_count:{roomId}:{matchingSockets}");
                }
            }

        }

        private static void ValidateScheduler(List<string> errors)
        {
            var eventManager = UnityEngine.Object.FindFirstObjectByType<
                EventManager>(FindObjectsInactive.Include);
            if (eventManager == null)
            {
                errors.Add("event_manager_missing");
            }
            else
            {
                var managerView = new SerializedObject(eventManager);
                var registry = managerView.FindProperty("registry")
                    ?.objectReferenceValue as EventRegistrySO;
                foreach (var requiredEvent in new[]
                         {
                             EventId.OxygenLeak,
                             EventId.PowerOff,
                             EventId.EngineBreak
                         })
                {
                    if (registry == null || registry.GetData(requiredEvent) == null)
                    {
                        errors.Add($"event_registry_data_missing:{requiredEvent}");
                    }
                }
            }

            var scheduler = UnityEngine.Object.FindFirstObjectByType<
                PHSNetworkEventScheduler>(FindObjectsInactive.Include);
            if (scheduler == null)
            {
                errors.Add("network_event_scheduler_missing");
                return;
            }

            var serializedScheduler = new SerializedObject(scheduler);
            var weightedEvents = serializedScheduler.FindProperty("weightedEvents");
            var ids = new HashSet<EventId>();
            for (var index = 0; index < weightedEvents.arraySize; index++)
            {
                var entry = weightedEvents.GetArrayElementAtIndex(index);
                ids.Add((EventId)entry.FindPropertyRelative("eventId").intValue);
            }

            foreach (var requiredEvent in new[]
                     {
                         EventId.OxygenLeak,
                         EventId.PowerOff,
                         EventId.EngineBreak
                     })
            {
                if (!ids.Contains(requiredEvent))
                {
                    errors.Add($"scheduler_event_missing:{requiredEvent}");
                }
            }
        }

        private static string GetPath(Transform target)
        {
            var parts = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
            {
                parts.Push(current.name);
            }

            return string.Join("/", parts);
        }
    }
}
#endif
