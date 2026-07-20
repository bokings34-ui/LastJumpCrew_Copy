using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Persistent server-only incident scheduler. It reserves commands in the
    /// network ledger; scene-owned adapters execute those commands.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunFlowCoordinator))]
    [RequireComponent(typeof(NetworkRunStageClock))]
    [RequireComponent(typeof(NetworkRunRandomLedger))]
    [RequireComponent(typeof(NetworkRunIncidentLedger))]
    public sealed class PHSNetworkIncidentDirector :
        MonoBehaviour,
        IIncidentScheduleConfigurator
    {
        private const uint MaximumSemanticSlot = 0x00FFFFFFU;
        private const double UnitDoubleFromUInt64 = 1d / 9007199254740992d;

        private NetworkRunFlowCoordinator runFlow;
        private NetworkRunStageClock stageClock;
        private NetworkRunRandomLedger randomLedger;
        private NetworkRunIncidentLedger incidentLedger;
        private RunIncidentScheduleDefinition definition;
        private uint nextExternalSlot = 1U;
        private uint nextInternalSlot = 1U;
        private double nextExternalDueServerTime;
        private double nextInternalDueServerTime;
        private bool setupValid;
        private bool isConfigured;
        private bool schedulingEnabled;
        private bool scheduleCancelled;
        private string cancellationCause;
        private string readinessReason = "setup_not_validated";

        public RunIncidentScheduleDefinition Definition => definition;
        public bool IsConfigured => isConfigured;
        public bool SchedulingEnabled => schedulingEnabled;
        public bool ScheduleCancelled => scheduleCancelled;
        public bool IsReadyToSchedule => TryGetSchedulingReadiness(out _);
        public string ReadinessReason
        {
            get
            {
                TryGetSchedulingReadiness(out var reason);
                return reason;
            }
        }

        public string CancellationCause => cancellationCause;
        public uint NextExternalSlot => nextExternalSlot;
        public uint NextInternalSlot => nextInternalSlot;
        public double NextExternalDueServerTime => nextExternalDueServerTime;
        public double NextInternalDueServerTime => nextInternalDueServerTime;

        private void Awake()
        {
            runFlow = GetComponent<NetworkRunFlowCoordinator>();
            stageClock = GetComponent<NetworkRunStageClock>();
            randomLedger = GetComponent<NetworkRunRandomLedger>();
            incidentLedger = GetComponent<NetworkRunIncidentLedger>();
            setupValid = ValidateSetup(out readinessReason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_INCIDENT_DIRECTOR_SETUP_FAILED reason={readinessReason}",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!TryGetSchedulingReadiness(out _))
            {
                return;
            }

            var serverTime = runFlow.NetworkManager.ServerTime.Time;
            if (definition.MaximumActiveExternal > 0
                && serverTime >= nextExternalDueServerTime)
            {
                TickChannelServer(
                    NetworkRunIncidentChannel.External,
                    serverTime);
            }

            if (!schedulingEnabled
                || runFlow.Phase != NetworkRunPhase.Charging)
            {
                return;
            }

            if (definition.MaximumActiveInternal > 0
                && serverTime >= nextInternalDueServerTime)
            {
                TickChannelServer(
                    NetworkRunIncidentChannel.Internal,
                    serverTime);
            }
        }

        public bool TryConfigureServer(
            RunIncidentScheduleDefinition candidate,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (candidate == null)
            {
                reason = "schedule_definition_missing";
                return false;
            }

            if (!candidate.TryValidate(out var definitionReason))
            {
                reason = $"schedule_definition_invalid:{definitionReason}";
                return false;
            }

            if (isConfigured && !scheduleCancelled)
            {
                if (definition.IsEquivalentTo(candidate))
                {
                    reason = null;
                    return true;
                }

                reason =
                    $"schedule_already_configured:" +
                    $"{definition.StageSequence}";
                return false;
            }

            if (candidate.MapId != runFlow.ActiveMapId)
            {
                reason =
                    $"active_map_mismatch:" +
                    $"{candidate.MapId}!={runFlow.ActiveMapId}";
                return false;
            }

            if (candidate.MapId != stageClock.MapId
                || candidate.StageSequence != stageClock.StageSequence)
            {
                reason =
                    $"stage_clock_mismatch:" +
                    $"map={candidate.MapId}/{stageClock.MapId}:" +
                    $"sequence={candidate.StageSequence}/{stageClock.StageSequence}";
                return false;
            }

            var serverTime = runFlow.NetworkManager.ServerTime.Time;
            if (!TryRollInitialDueServerTime(
                    candidate,
                    NetworkRunIncidentChannel.External,
                    serverTime,
                    out var externalDue,
                    out reason)
                || !TryRollInitialDueServerTime(
                    candidate,
                    NetworkRunIncidentChannel.Internal,
                    serverTime,
                    out var internalDue,
                    out reason))
            {
                return false;
            }

            if (!incidentLedger.TryBeginStageServer(
                    candidate.MapId,
                    candidate.StageSequence,
                    candidate.PressureCapacity,
                    candidate.MaximumActiveExternal,
                    candidate.MaximumActiveInternal,
                    out var beginReason))
            {
                reason = $"incident_stage_begin_failed:{beginReason}";
                return false;
            }

            definition = candidate;
            nextExternalSlot = 1U;
            nextInternalSlot = 1U;
            nextExternalDueServerTime = externalDue;
            nextInternalDueServerTime = internalDue;
            isConfigured = true;
            schedulingEnabled = false;
            scheduleCancelled = false;
            cancellationCause = null;
            readinessReason = "scheduling_disabled";
            reason = null;
            Debug.Log(
                $"PHS_INCIDENT_DIRECTOR_CONFIGURED map={candidate.MapId} " +
                $"stage={candidate.StageSequence} pressure={candidate.PressureCapacity} " +
                $"externalCap={candidate.MaximumActiveExternal} " +
                $"internalCap={candidate.MaximumActiveInternal}",
                this);
            return true;
        }

        public bool TrySetSchedulingEnabledServer(
            bool shouldEnable,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (!isConfigured)
            {
                reason = "schedule_not_configured";
                return false;
            }

            if (scheduleCancelled)
            {
                reason =
                    $"schedule_cancelled:" +
                    $"{cancellationCause ?? "unknown"}";
                return false;
            }

            schedulingEnabled = shouldEnable;
            readinessReason = shouldEnable
                ? "phase_not_charging"
                : "scheduling_disabled";
            reason = null;
            Debug.Log(
                $"PHS_INCIDENT_DIRECTOR_SCHEDULING enabled={shouldEnable} " +
                $"map={definition.MapId} stage={definition.StageSequence}",
                this);
            return true;
        }

        public bool TryCancelScheduleServer(
            string cause,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(cause))
            {
                reason = "cancellation_cause_required";
                return false;
            }

            if (!isConfigured)
            {
                reason = "schedule_not_configured";
                return false;
            }

            if (scheduleCancelled)
            {
                reason = null;
                return true;
            }

            if (!incidentLedger.TryCancelStageServer(
                    definition.StageSequence,
                    cause,
                    out var cancelReason))
            {
                reason = $"incident_stage_cancel_failed:{cancelReason}";
                return false;
            }

            schedulingEnabled = false;
            scheduleCancelled = true;
            cancellationCause = cause;
            nextExternalDueServerTime = 0d;
            nextInternalDueServerTime = 0d;
            readinessReason = $"schedule_cancelled:{cause}";
            reason = null;
            Debug.Log(
                $"PHS_INCIDENT_DIRECTOR_CANCELLED map={definition.MapId} " +
                $"stage={definition.StageSequence} cause={cause}",
                this);
            return true;
        }

        public bool TryGetSchedulingReadiness(out string reason)
        {
            if (!setupValid)
            {
                reason = readinessReason ?? "setup_invalid";
                return false;
            }

            if (!isConfigured)
            {
                readinessReason = "schedule_not_configured";
                reason = readinessReason;
                return false;
            }

            if (scheduleCancelled)
            {
                readinessReason =
                    $"schedule_cancelled:" +
                    $"{cancellationCause ?? "unknown"}";
                reason = readinessReason;
                return false;
            }

            if (!schedulingEnabled)
            {
                readinessReason = "scheduling_disabled";
                reason = readinessReason;
                return false;
            }

            if (!RequireServer(out reason))
            {
                readinessReason = reason;
                return false;
            }

            var incidentSnapshot = incidentLedger.Snapshot;
            if (incidentSnapshot.State != NetworkRunIncidentStageState.Active
                || incidentSnapshot.MapId != definition.MapId
                || incidentSnapshot.StageSequence != definition.StageSequence)
            {
                readinessReason =
                    $"incident_stage_not_active:" +
                    $"state={incidentSnapshot.State}:" +
                    $"map={incidentSnapshot.MapId}:" +
                    $"sequence={incidentSnapshot.StageSequence}";
                reason = readinessReason;
                return false;
            }

            if (runFlow.Phase != NetworkRunPhase.Charging)
            {
                readinessReason = $"phase_not_charging:{runFlow.Phase}";
                reason = readinessReason;
                return false;
            }

            if (stageClock.State != NetworkRunStageClockState.Running)
            {
                readinessReason =
                    $"stage_clock_not_running:{stageClock.State}";
                reason = readinessReason;
                return false;
            }

            if (runFlow.ActiveMapId != definition.MapId
                || stageClock.MapId != definition.MapId
                || stageClock.StageSequence != definition.StageSequence)
            {
                readinessReason =
                    $"stage_identity_mismatch:" +
                    $"definition={definition.MapId}/{definition.StageSequence}:" +
                    $"flow={runFlow.ActiveMapId}:" +
                    $"clock={stageClock.MapId}/{stageClock.StageSequence}";
                reason = readinessReason;
                return false;
            }

            readinessReason = null;
            reason = null;
            return true;
        }

        private void TickChannelServer(
            NetworkRunIncidentChannel channel,
            double serverTime)
        {
            var slot = channel == NetworkRunIncidentChannel.External
                ? nextExternalSlot
                : nextInternalSlot;
            if (!TryRollSlot(
                    definition,
                    channel,
                    slot,
                    out var selectedEntry,
                    out var nextIntervalSeconds,
                    out var rollReason))
            {
                schedulingEnabled = false;
                readinessReason = $"slot_roll_failed:{rollReason}";
                Debug.LogError(
                    $"PHS_INCIDENT_DIRECTOR_FAILED reason={readinessReason} " +
                    $"channel={channel} slot={slot}",
                    this);
                return;
            }

            if (!TryIncrementSlot(channel, slot, out var slotReason))
            {
                schedulingEnabled = false;
                readinessReason = slotReason;
                Debug.LogError(
                    $"PHS_INCIDENT_DIRECTOR_FAILED reason={slotReason} " +
                    $"channel={channel} slot={slot}",
                    this);
                return;
            }

            SetNextDueServerTime(
                channel,
                serverTime + nextIntervalSeconds);

            var requestId = new FixedString64Bytes(
                $"schedule:{definition.StageSequence}:{channel}:{slot}");
            var payloadKind = channel == NetworkRunIncidentChannel.External
                ? NetworkRunIncidentPayloadKind.EventManagerEvent
                : NetworkRunIncidentPayloadKind.ShipAccident;
            var request = new NetworkRunIncidentRequest(
                requestId,
                0UL,
                definition.StageSequence,
                definition.MapId,
                channel,
                payloadKind,
                selectedEntry.IncidentFamily,
                selectedEntry.ContentId,
                NetworkRunIncidentSourceKind.Scheduled,
                selectedEntry.PressureCost,
                selectedEntry.WarpChargeMultiplier,
                default);
            if (!incidentLedger.TryReserveCommandServer(
                    in request,
                    out _,
                    out var reserveReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_DIRECTOR_RESERVE_SKIPPED " +
                    $"reason={reserveReason} request={requestId} " +
                    $"channel={channel} slot={slot}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_INCIDENT_DIRECTOR_RESERVED request={requestId} " +
                $"channel={channel} slot={slot} " +
                $"content={selectedEntry.ContentId} " +
                $"family={selectedEntry.IncidentFamily}",
                this);
        }

        private bool TryRollInitialDueServerTime(
            RunIncidentScheduleDefinition candidate,
            NetworkRunIncidentChannel channel,
            double serverTime,
            out double dueServerTime,
            out string reason)
        {
            var maximumActive = channel == NetworkRunIncidentChannel.External
                ? candidate.MaximumActiveExternal
                : candidate.MaximumActiveInternal;
            if (maximumActive == 0)
            {
                dueServerTime = 0d;
                reason = null;
                return true;
            }

            if (!TryCreateRandomScope(
                    candidate.StageSequence,
                    channel,
                    0U,
                    out var random,
                    out reason))
            {
                dueServerTime = 0d;
                return false;
            }

            var minimum = channel == NetworkRunIncidentChannel.External
                ? candidate.ExternalIntervalMinSeconds
                : candidate.InternalIntervalMinSeconds;
            var maximum = channel == NetworkRunIncidentChannel.External
                ? candidate.ExternalIntervalMaxSeconds
                : candidate.InternalIntervalMaxSeconds;
            dueServerTime = serverTime + RollRange(random, minimum, maximum);
            reason = null;
            return true;
        }

        private bool TryRollSlot(
            RunIncidentScheduleDefinition candidate,
            NetworkRunIncidentChannel channel,
            uint slot,
            out RunIncidentWeightedEntry selectedEntry,
            out double nextIntervalSeconds,
            out string reason)
        {
            selectedEntry = default;
            nextIntervalSeconds = 0d;
            if (!TryCreateRandomScope(
                    candidate.StageSequence,
                    channel,
                    slot,
                    out var random,
                    out reason))
            {
                return false;
            }

            var entries = channel == NetworkRunIncidentChannel.External
                ? candidate.ExternalEntries
                : candidate.InternalEntries;
            var totalWeight = 0d;
            for (var index = 0; index < entries.Count; index++)
            {
                totalWeight += entries[index].Weight;
            }

            if (double.IsNaN(totalWeight)
                || double.IsInfinity(totalWeight)
                || totalWeight <= 0d)
            {
                reason = $"total_weight_invalid:{channel}";
                return false;
            }

            var weightedRoll =
                ((random.NextUInt64() >> 11) * UnitDoubleFromUInt64)
                * totalWeight;
            selectedEntry = entries[entries.Count - 1];
            for (var index = 0; index < entries.Count; index++)
            {
                weightedRoll -= entries[index].Weight;
                if (weightedRoll < 0d)
                {
                    selectedEntry = entries[index];
                    break;
                }
            }

            var minimum = channel == NetworkRunIncidentChannel.External
                ? candidate.ExternalIntervalMinSeconds
                : candidate.InternalIntervalMinSeconds;
            var maximum = channel == NetworkRunIncidentChannel.External
                ? candidate.ExternalIntervalMaxSeconds
                : candidate.InternalIntervalMaxSeconds;
            nextIntervalSeconds = RollRange(random, minimum, maximum);
            reason = null;
            return true;
        }

        private bool TryCreateRandomScope(
            uint stageSequence,
            NetworkRunIncidentChannel channel,
            uint slot,
            out PHSDeterministicRandom random,
            out string reason)
        {
            random = null;
            if (slot > MaximumSemanticSlot)
            {
                reason = $"semantic_slot_exhausted:{slot}";
                return false;
            }

            var scopeKey =
                ((ulong)stageSequence << 32)
                | ((ulong)(byte)channel << 24)
                | slot;
            var stream = channel == NetworkRunIncidentChannel.External
                ? NetworkRunRandomStream.ExternalThreat
                : NetworkRunRandomStream.InternalAccident;
            if (!randomLedger.TryCreateServerScope(
                    stream,
                    scopeKey,
                    out random,
                    out var randomReason))
            {
                reason =
                    $"random_scope_failed:" +
                    $"{channel}:{slot}:{randomReason}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryIncrementSlot(
            NetworkRunIncidentChannel channel,
            uint currentSlot,
            out string reason)
        {
            if (currentSlot >= MaximumSemanticSlot)
            {
                reason = $"semantic_slot_exhausted:{channel}:{currentSlot}";
                return false;
            }

            if (channel == NetworkRunIncidentChannel.External)
            {
                nextExternalSlot = currentSlot + 1U;
            }
            else
            {
                nextInternalSlot = currentSlot + 1U;
            }

            reason = null;
            return true;
        }

        private void SetNextDueServerTime(
            NetworkRunIncidentChannel channel,
            double dueServerTime)
        {
            if (channel == NetworkRunIncidentChannel.External)
            {
                nextExternalDueServerTime = dueServerTime;
            }
            else
            {
                nextInternalDueServerTime = dueServerTime;
            }
        }

        private bool RequireServer(out string reason)
        {
            if (!setupValid)
            {
                reason = readinessReason ?? "setup_invalid";
                return false;
            }

            if (!runFlow.IsSpawned
                || !stageClock.IsSpawned
                || !randomLedger.IsSpawned
                || !incidentLedger.IsSpawned
                || !runFlow.IsServer
                || !stageClock.IsServer
                || !randomLedger.IsServer
                || !incidentLedger.IsServer
                || runFlow.OwnerClientId
                    != NetworkManager.ServerClientId)
            {
                reason = "server_authority_required";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateSetup(out string reason)
        {
            if (runFlow == null)
            {
                reason = "run_flow_missing";
                return false;
            }

            if (stageClock == null)
            {
                reason = "stage_clock_missing";
                return false;
            }

            if (randomLedger == null)
            {
                reason = "random_ledger_missing";
                return false;
            }

            if (incidentLedger == null)
            {
                reason = "incident_ledger_missing";
                return false;
            }

            if (runFlow.gameObject != gameObject
                || stageClock.gameObject != gameObject
                || randomLedger.gameObject != gameObject
                || incidentLedger.gameObject != gameObject)
            {
                reason = "root_component_mismatch";
                return false;
            }

            reason = null;
            return true;
        }

        private static double RollRange(
            PHSDeterministicRandom random,
            float minimum,
            float maximum)
        {
            if (minimum.Equals(maximum))
            {
                return minimum;
            }

            var unit =
                (random.NextUInt64() >> 11)
                * UnitDoubleFromUInt64;
            return minimum + ((maximum - minimum) * unit);
        }
    }
}
