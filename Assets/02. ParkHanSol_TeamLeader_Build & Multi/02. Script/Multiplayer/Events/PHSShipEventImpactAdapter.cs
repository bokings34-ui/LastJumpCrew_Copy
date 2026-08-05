using System.Collections.Generic;
using System.Linq;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSShipEventImpactAdapter : MonoBehaviour, IShipEventImpactSink
    {
        [Header("Inspector References")]
        [SerializeField] private NetworkShipSystemsState shipSystemsState;

        private readonly HashSet<ulong> appliedEventInstanceIds = new();

        public bool TryApplyTerminalImpact(
            ulong eventInstanceId,
            EventId eventId,
            bool success,
            out string reason)
        {
            if (eventInstanceId == 0UL)
            {
                reason = "event_instance_invalid";
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} event={eventId}",
                    this);
                return false;
            }

            if (!IsSupportedTerminalEvent(eventId))
            {
                reason = "terminal_event_unsupported";
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            if (appliedEventInstanceIds.Contains(eventInstanceId))
            {
                reason = "event_instance_already_applied";
                Debug.LogWarning(
                    $"PHS_SHIP_EVENT_IMPACT_REJECTED reason={reason} instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            if (shipSystemsState == null
                || !shipSystemsState.IsSpawned
                || !shipSystemsState.IsServer)
            {
                reason = "server_ship_systems_required";
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} " +
                    $"instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            var consequenceEventId = default(EventId);
            var consequenceInstanceId = 0UL;
            if (!success
                && !TrySpawnFailureConsequence(
                    eventInstanceId,
                    eventId,
                    out consequenceEventId,
                    out consequenceInstanceId,
                    out reason))
            {
                Debug.LogError(
                    $"PHS_SHIP_EVENT_CONSEQUENCE_FAILED reason={reason} " +
                    $"instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            appliedEventInstanceIds.Add(eventInstanceId);
            reason = null;
            Debug.Log(
                $"PHS_SHIP_EVENT_IMPACT_DELEGATED instance={eventInstanceId} " +
                $"event={eventId} success={success} " +
                $"target={(success ? "none" : consequenceEventId.ToString())} " +
                $"consequenceInstance={(success ? 0UL : consequenceInstanceId)}",
                this);
            return true;
        }

        public static bool TryGetFailureConsequence(
            EventId eventId,
            out EventId consequenceEventId)
        {
            switch (eventId)
            {
                case EventId.EmpAttack:
                    consequenceEventId = EventId.Fire;
                    return true;
                case EventId.MeteorAttack:
                    consequenceEventId = EventId.OxygenLeak;
                    return true;
                case EventId.EnemyScout:
                    consequenceEventId = EventId.EnemySpawn;
                    return true;
                default:
                    consequenceEventId = default;
                    return false;
            }
        }

        private static bool TrySpawnFailureConsequence(
            ulong sourceEventInstanceId,
            EventId sourceEventId,
            out EventId consequenceEventId,
            out ulong consequenceInstanceId,
            out string reason)
        {
            consequenceInstanceId = 0UL;
            if (!TryGetFailureConsequence(sourceEventId, out consequenceEventId))
            {
                reason = "consequence_not_mapped";
                return false;
            }

            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsAuthoritative)
            {
                reason = "event_coordinator_server_required";
                return false;
            }

            if (!coordinator.TryGetSnapshot(sourceEventInstanceId, out var snapshot))
            {
                reason = "source_snapshot_missing";
                return false;
            }

            var sourceRoomId = snapshot.RoomId.ToString();
            var matchingRooms = FindObjectsByType<ShipRoom>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(room => room != null && room.RoomId == sourceRoomId)
                .ToArray();
            if (matchingRooms.Length != 1)
            {
                reason = $"source_room_count:{matchingRooms.Length}:room={sourceRoomId}";
                return false;
            }

            if (!coordinator.TrySpawnEventServer(
                    consequenceEventId,
                    matchingRooms[0],
                    out consequenceInstanceId))
            {
                reason = $"consequence_spawn_rejected:{consequenceEventId}";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsSupportedTerminalEvent(EventId eventId)
        {
            return eventId == EventId.EmpAttack
                || eventId == EventId.MeteorAttack
                || eventId == EventId.EnemyScout;
        }
    }
}
