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

        [Header("Terminal Failure Impact")]
        [SerializeField, Min(1)] private int meteorAttackHullDamage = 10;
        [SerializeField, Min(1)] private int enemyScoutEngineDamage = 10;
        [SerializeField] private bool enemyScoutCausesEngineFault = true;

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

            if (shipSystemsState == null)
            {
                reason = "ship_systems_reference_missing";
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            if (!shipSystemsState.IsSpawned || !shipSystemsState.IsServer)
            {
                reason = "server_authority_required";
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            if (success)
            {
                appliedEventInstanceIds.Add(eventInstanceId);
                reason = null;
                Debug.Log(
                    $"PHS_SHIP_EVENT_IMPACT_SKIPPED reason=terminal_success instance={eventInstanceId} event={eventId}",
                    this);
                return true;
            }

            if (!TryApplyFailureImpact(eventId, out reason))
            {
                Debug.LogError(
                    $"PHS_SHIP_EVENT_IMPACT_FAILED reason={reason} instance={eventInstanceId} event={eventId}",
                    this);
                return false;
            }

            appliedEventInstanceIds.Add(eventInstanceId);
            Debug.Log(
                $"PHS_SHIP_EVENT_IMPACT_APPLIED instance={eventInstanceId} event={eventId} success=false shipRevision={shipSystemsState.Revision}",
                this);
            return true;
        }

        private bool TryApplyFailureImpact(EventId eventId, out string reason)
        {
            switch (eventId)
            {
                case EventId.EmpAttack:
                    return shipSystemsState.TryPowerOff(out reason);
                case EventId.MeteorAttack:
                    return shipSystemsState.TryApplyShipDamage(
                        meteorAttackHullDamage,
                        "terminal_meteor_attack_failed",
                        out reason);
                case EventId.EnemyScout:
                    return shipSystemsState.TryApplyModuleDamage(
                        NetworkShipModuleId.Engine,
                        enemyScoutEngineDamage,
                        enemyScoutCausesEngineFault,
                        "terminal_enemy_scout_failed",
                        out reason);
                default:
                    reason = "terminal_event_unsupported";
                    return false;
            }
        }

        private static bool IsSupportedTerminalEvent(EventId eventId)
        {
            return eventId == EventId.EmpAttack
                || eventId == EventId.MeteorAttack
                || eventId == EventId.EnemyScout;
        }
    }
}
