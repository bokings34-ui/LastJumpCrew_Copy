using System.Collections.Generic;
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

            appliedEventInstanceIds.Add(eventInstanceId);
            reason = null;
            Debug.Log(
                $"PHS_SHIP_EVENT_IMPACT_DELEGATED instance={eventInstanceId} " +
                $"event={eventId} success={success} " +
                $"target={(success ? "none" : "incident_consequence_selector")}",
                this);
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
