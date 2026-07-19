using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using Unity.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Scene-owned server adapter that turns persistent incident commands into
    /// concrete EventManager or ship-accident runtime instances.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PHSMapIncidentCommandConsumer : MonoBehaviour
    {
        private const double UnitDoubleFromUInt64 =
            1d / 9007199254740992d;

        [Header("Inspector References")]
        [SerializeField] private NetworkEventCoordinator eventCoordinator;
        [SerializeField] private PHSNetworkShipAccidentCoordinator accidentCoordinator;
        [SerializeField] private PHSShipIncidentLayout incidentLayout;
        [Tooltip("Migration only. Disable after the authored Incident Layout is wired.")]
        [SerializeField] private bool allowLegacyLocationFallback = true;
        [SerializeField] private ShipRoom[] rooms = Array.Empty<ShipRoom>();

        private readonly List<NetworkRunIncidentCommand> pendingCommands = new();
        private readonly List<string> compatibleAnchorIds = new();
        private readonly List<IIncidentLocation> locationCandidates = new();
        private readonly Dictionary<ulong, ulong> eventCommandIds = new();
        private readonly Dictionary<uint, ulong> accidentCommandIds = new();
        private readonly Dictionary<ulong, EventId> eventContentIds = new();
        private readonly Dictionary<uint, PHSShipAccidentId> accidentContentIds =
            new();
        private readonly Dictionary<ulong, string> eventLocationIds = new();
        private readonly Dictionary<uint, string> accidentLocationIds = new();
        private readonly Dictionary<ulong, EventCompletion>
            pendingEventCompletions = new();
        private readonly Dictionary<uint, AccidentCompletion>
            pendingAccidentCompletions = new();

        private ShipRoom[] configuredRooms = Array.Empty<ShipRoom>();
        private NetworkRunSessionRoot runSessionRoot;
        private NetworkRunIncidentLedger incidentLedger;
        private NetworkRunRandomLedger randomLedger;
        private ulong spawningEventCommandId;
        private ulong spawningAccidentCommandId;
        private string eventTerminationCause;
        private string accidentTerminationCause;

        private readonly struct EventCompletion
        {
            public EventCompletion(
                EventId eventId,
                bool succeeded,
                string cancellationCause = null)
            {
                EventId = eventId;
                Succeeded = succeeded;
                CancellationCause = cancellationCause;
            }

            public EventId EventId { get; }
            public bool Succeeded { get; }
            public string CancellationCause { get; }
            public bool IsCancellation =>
                !string.IsNullOrEmpty(CancellationCause);
        }

        private readonly struct AccidentCompletion
        {
            public AccidentCompletion(
                PHSShipAccidentId accidentId,
                bool succeeded,
                string cancellationCause = null)
            {
                AccidentId = accidentId;
                Succeeded = succeeded;
                CancellationCause = cancellationCause;
            }

            public PHSShipAccidentId AccidentId { get; }
            public bool Succeeded { get; }
            public string CancellationCause { get; }
            public bool IsCancellation =>
                !string.IsNullOrEmpty(CancellationCause);
        }

        public NetworkEventCoordinator EventCoordinator => eventCoordinator;
        public PHSNetworkShipAccidentCoordinator AccidentCoordinator => accidentCoordinator;
        public PHSShipIncidentLayout IncidentLayout => incidentLayout;
        public bool AllowLegacyLocationFallback => allowLegacyLocationFallback;
        public int ConfiguredRoomCount => configuredRooms.Length;
        public bool IsConfigured { get; private set; }

        public ShipRoom GetConfiguredRoomAt(int index)
        {
            if (index < 0 || index >= configuredRooms.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return configuredRooms[index];
        }

        public bool TryTerminateAllServer(string cause, out string reason)
        {
            if (!IsConfigured)
            {
                reason = "consumer_not_configured";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cause))
            {
                reason = "termination_cause_required";
                return false;
            }

            if (!CanFitFixedString64(cause))
            {
                reason = "termination_cause_invalid";
                return false;
            }

            bool eventTerminated;
            var previousEventTerminationCause = eventTerminationCause;
            eventTerminationCause = cause;
            try
            {
                eventTerminated = eventCoordinator.TryTerminateAllServer();
            }
            finally
            {
                eventTerminationCause = previousEventTerminationCause;
            }

            bool accidentsTerminated;
            string accidentReason;
            var previousAccidentTerminationCause =
                accidentTerminationCause;
            accidentTerminationCause = cause;
            try
            {
                accidentsTerminated =
                    accidentCoordinator.TryTerminateAllServer(
                        cause,
                        out accidentReason);
            }
            finally
            {
                accidentTerminationCause =
                    previousAccidentTerminationCause;
            }

            string eventReconcileReason = null;
            string accidentReconcileReason = null;
            var eventReconciled = !eventTerminated
                || TryReconcileTerminatedEventCommands(
                    cause,
                    out eventReconcileReason);
            var accidentReconciled = !accidentsTerminated
                || TryReconcileTerminatedAccidentCommands(
                    cause,
                    out accidentReconcileReason);
            if (!eventTerminated
                || !accidentsTerminated
                || !eventReconciled
                || !accidentReconciled)
            {
                reason =
                    $"runtime_termination_failed:" +
                    $"event={eventTerminated}:" +
                    $"accident={accidentsTerminated}:" +
                    $"event_reconciled={eventReconciled}:" +
                    $"accident_reconciled={accidentReconciled}:" +
                    $"detail={accidentReason ?? "none"}:" +
                    $"event_detail={eventReconcileReason ?? "none"}:" +
                    $"accident_detail={accidentReconcileReason ?? "none"}";
                return false;
            }

            pendingEventCompletions.Clear();
            pendingAccidentCompletions.Clear();
            eventContentIds.Clear();
            accidentContentIds.Clear();
            spawningEventCommandId = 0UL;
            spawningAccidentCommandId = 0UL;
            reason = null;
            return true;
        }

        private void Awake()
        {
            IsConfigured = TryValidateAndSortSetup();
            enabled = IsConfigured;
        }

        private void OnEnable()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleRootAvailable;
            NetworkRunSessionRoot.InstanceAvailable += HandleRootAvailable;
            if (eventCoordinator != null)
            {
                eventCoordinator.ServerEventFinished -= HandleEventFinished;
                eventCoordinator.ServerEventFinished += HandleEventFinished;
            }

            if (accidentCoordinator != null)
            {
                accidentCoordinator.ServerAccidentFinished -= HandleAccidentFinished;
                accidentCoordinator.ServerAccidentFinished += HandleAccidentFinished;
            }

            TryBindRoot(NetworkRunSessionRoot.Instance);
        }

        private void OnDisable()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleRootAvailable;
            if (eventCoordinator != null)
            {
                eventCoordinator.ServerEventFinished -= HandleEventFinished;
            }

            if (accidentCoordinator != null)
            {
                accidentCoordinator.ServerAccidentFinished -= HandleAccidentFinished;
            }
        }

        private void Update()
        {
            if (!IsConfigured)
            {
                return;
            }

            if (runSessionRoot == null
                || runSessionRoot != NetworkRunSessionRoot.Instance)
            {
                TryBindRoot(NetworkRunSessionRoot.Instance);
            }

            if (!CanProcessServerCommands())
            {
                return;
            }

            RetryPendingRuntimeCompletions();
            CopyCurrentPendingCommands();
            foreach (var command in pendingCommands)
            {
                ProcessPendingCommand(command);
            }
        }

        private void HandleRootAvailable(NetworkRunSessionRoot root)
        {
            TryBindRoot(root);
        }

        private void TryBindRoot(NetworkRunSessionRoot root)
        {
            if (root == null
                || root.Incidents == null
                || root.Rng == null
                || root.StageClock == null)
            {
                return;
            }

            runSessionRoot = root;
            incidentLedger = root.Incidents;
            randomLedger = root.Rng;
        }

        private bool CanProcessServerCommands()
        {
            return runSessionRoot != null
                && runSessionRoot.IsSpawned
                && runSessionRoot.IsServer
                && incidentLedger != null
                && incidentLedger.IsSpawned
                && randomLedger != null
                && randomLedger.IsSpawned
                && runSessionRoot.StageClock != null
                && runSessionRoot.StageClock.IsSpawned
                && runSessionRoot.StageClock.IsServer
                && runSessionRoot.StageClock.MapId > 0
                && runSessionRoot.StageClock.StageSequence != 0U;
        }

        private void CopyCurrentPendingCommands()
        {
            pendingCommands.Clear();
            var mapId = runSessionRoot.StageClock.MapId;
            var stageSequence = runSessionRoot.StageClock.StageSequence;

            for (var index = 0; index < incidentLedger.CommandCount; index++)
            {
                var command = incidentLedger.GetCommandAt(index);
                if (command.State == NetworkRunIncidentCommandState.Pending
                    && command.MapId == mapId
                    && command.StageSequence == stageSequence)
                {
                    pendingCommands.Add(command);
                }
            }

            pendingCommands.Sort(
                (left, right) => left.CommandId.CompareTo(right.CommandId));
        }

        private void ProcessPendingCommand(NetworkRunIncidentCommand command)
        {
            switch (command.Channel)
            {
                case NetworkRunIncidentChannel.External:
                    ProcessExternalCommand(command);
                    return;
                case NetworkRunIncidentChannel.Internal:
                    ProcessInternalCommand(command);
                    return;
                default:
                    CancelPendingCommand(command.CommandId, "channel_invalid");
                    return;
            }
        }

        private void ProcessExternalCommand(NetworkRunIncidentCommand command)
        {
            if (!IncidentRequestContentContract.TryValidate(
                    command.Channel,
                    command.PayloadKind,
                    command.IncidentFamily,
                    command.ContentId,
                    out var contractReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=content_contract_invalid detail={contractReason}",
                    this);
                CancelPendingCommand(
                    command.CommandId,
                    "content_contract_invalid");
                return;
            }

            if (!eventCoordinator.IsSpawned || !eventCoordinator.IsServer)
            {
                return;
            }

            if (!TryResolveExternalRoom(
                    command,
                    out var room,
                    out var resolvedTargetId,
                    out var managedLocationId,
                    out var roomReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=target_unavailable detail={roomReason}",
                    this);
                CancelPendingCommand(command.CommandId, "target_unavailable");
                return;
            }

            var executorId = eventCoordinator.NetworkObjectId;
            if (!incidentLedger.TryClaimCommandServer(
                    command.CommandId,
                    executorId,
                    out var claimed,
                    out _))
            {
                return;
            }

            if (!TryOccupyManagedLocation(
                    managedLocationId,
                    claimed.CommandId,
                    out var occupyReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=location_occupy_failed detail={occupyReason}",
                    this);
                CancelClaimedCommand(
                    claimed.CommandId,
                    "location_occupy_failed");
                return;
            }

            var eventId = (EventId)claimed.ContentId;
            var runtimeInstanceId = 0UL;
            spawningEventCommandId = claimed.CommandId;
            try
            {
                if (!eventCoordinator.TrySpawnEventServer(
                        eventId,
                        room,
                        out runtimeInstanceId))
                {
                    pendingEventCompletions.Remove(runtimeInstanceId);
                    ReleaseManagedLocation(
                        managedLocationId,
                        claimed.CommandId);
                    CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                    return;
                }
            }
            catch (Exception exception)
            {
                pendingEventCompletions.Remove(runtimeInstanceId);
                ReleaseManagedLocation(
                    managedLocationId,
                    claimed.CommandId);
                Debug.LogException(exception, this);
                CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                return;
            }
            finally
            {
                spawningEventCommandId = 0UL;
            }

            if (eventCommandIds.ContainsKey(runtimeInstanceId))
            {
                pendingEventCompletions.Remove(runtimeInstanceId);
                ReleaseManagedLocation(
                    managedLocationId,
                    claimed.CommandId);
                CancelClaimedCommand(claimed.CommandId, "runtime_id_collision");
                return;
            }

            eventCommandIds.Add(runtimeInstanceId, claimed.CommandId);
            eventContentIds[runtimeInstanceId] = eventId;
            if (!string.IsNullOrEmpty(managedLocationId))
            {
                eventLocationIds.Add(runtimeInstanceId, managedLocationId);
            }

            if (!incidentLedger.TryActivateCommandServer(
                    claimed.CommandId,
                    executorId,
                    runtimeInstanceId,
                    resolvedTargetId,
                    out var activateReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=activate_failed detail={activateReason}",
                    this);
                CancelClaimedCommand(claimed.CommandId, "activate_failed");
                if (pendingEventCompletions.TryGetValue(
                        runtimeInstanceId,
                        out var failedActivationCompletion))
                {
                    ProcessEventCompletion(
                        runtimeInstanceId,
                        failedActivationCompletion);
                }

                return;
            }

            Debug.Log(
                $"PHS_INCIDENT_EXTERNAL_ACTIVATED command={claimed.CommandId} " +
                $"runtime={runtimeInstanceId} event={eventId} room={room.RoomId}",
                this);

            if (pendingEventCompletions.TryGetValue(
                    runtimeInstanceId,
                    out var pendingCompletion))
            {
                ProcessEventCompletion(
                    runtimeInstanceId,
                    pendingCompletion);
            }
        }

        private void ProcessInternalCommand(NetworkRunIncidentCommand command)
        {
            if (!IncidentRequestContentContract.TryValidate(
                    command.Channel,
                    command.PayloadKind,
                    command.IncidentFamily,
                    command.ContentId,
                    out var contractReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=content_contract_invalid detail={contractReason}",
                    this);
                CancelPendingCommand(
                    command.CommandId,
                    "content_contract_invalid");
                return;
            }

            if (!accidentCoordinator.IsSpawned || !accidentCoordinator.IsServer)
            {
                return;
            }

            var accidentId = (PHSShipAccidentId)(ushort)command.ContentId;
            if (!accidentCoordinator.TryCopyAvailableCompatibleAnchorIdsServer(
                    accidentId,
                    compatibleAnchorIds,
                    out var anchorReason))
            {
                if (!string.IsNullOrEmpty(anchorReason)
                    && anchorReason.StartsWith(
                        "definition_missing:",
                        StringComparison.Ordinal))
                {
                    CancelPendingCommand(command.CommandId, "definition_missing");
                }

                return;
            }

            compatibleAnchorIds.Sort(StringComparer.Ordinal);
            if (!TryResolveInternalAnchorId(
                    command,
                    out var anchorId,
                    out var resolvedTargetId,
                    out var managedLocationId,
                    out var anchorResolveReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=target_unavailable detail={anchorResolveReason}",
                    this);
                CancelPendingCommand(command.CommandId, "target_unavailable");
                return;
            }

            if (!CanFitFixedString64(anchorId))
            {
                CancelPendingCommand(command.CommandId, "target_id_invalid");
                return;
            }

            var executorId = accidentCoordinator.NetworkObjectId;
            if (!incidentLedger.TryClaimCommandServer(
                    command.CommandId,
                    executorId,
                    out var claimed,
                    out _))
            {
                return;
            }

            if (!TryOccupyManagedLocation(
                    managedLocationId,
                    claimed.CommandId,
                    out var occupyReason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=location_occupy_failed detail={occupyReason}",
                    this);
                CancelClaimedCommand(
                    claimed.CommandId,
                    "location_occupy_failed");
                return;
            }

            var runtimeInstanceId = 0U;
            spawningAccidentCommandId = claimed.CommandId;
            try
            {
                if (!accidentCoordinator.TrySpawnAccidentServer(
                        accidentId,
                        anchorId,
                        out runtimeInstanceId,
                        out var spawnReason))
                {
                    pendingAccidentCompletions.Remove(runtimeInstanceId);
                    ReleaseManagedLocation(
                        managedLocationId,
                        claimed.CommandId);
                    Debug.LogWarning(
                        $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                        $"reason=spawn_failed detail={spawnReason}",
                        this);
                    CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                    return;
                }
            }
            catch (Exception exception)
            {
                pendingAccidentCompletions.Remove(runtimeInstanceId);
                ReleaseManagedLocation(
                    managedLocationId,
                    claimed.CommandId);
                Debug.LogException(exception, this);
                CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                return;
            }
            finally
            {
                spawningAccidentCommandId = 0UL;
            }

            if (accidentCommandIds.ContainsKey(runtimeInstanceId))
            {
                pendingAccidentCompletions.Remove(runtimeInstanceId);
                ReleaseManagedLocation(
                    managedLocationId,
                    claimed.CommandId);
                CancelClaimedCommand(claimed.CommandId, "runtime_id_collision");
                return;
            }

            accidentCommandIds.Add(runtimeInstanceId, claimed.CommandId);
            accidentContentIds[runtimeInstanceId] = accidentId;
            if (!string.IsNullOrEmpty(managedLocationId))
            {
                accidentLocationIds.Add(runtimeInstanceId, managedLocationId);
            }

            if (!incidentLedger.TryActivateCommandServer(
                    claimed.CommandId,
                    executorId,
                    runtimeInstanceId,
                    resolvedTargetId,
                    out var activateReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=activate_failed detail={activateReason}",
                    this);
                CancelClaimedCommand(claimed.CommandId, "activate_failed");
                if (pendingAccidentCompletions.TryGetValue(
                        runtimeInstanceId,
                        out var failedActivationCompletion))
                {
                    ProcessAccidentCompletion(
                        runtimeInstanceId,
                        failedActivationCompletion);
                }

                return;
            }

            Debug.Log(
                $"PHS_INCIDENT_INTERNAL_ACTIVATED command={claimed.CommandId} " +
                $"runtime={runtimeInstanceId} accident={accidentId} anchor={anchorId}",
                this);

            if (pendingAccidentCompletions.TryGetValue(
                    runtimeInstanceId,
                    out var pendingCompletion))
            {
                ProcessAccidentCompletion(
                    runtimeInstanceId,
                    pendingCompletion);
            }
        }

        private void HandleEventFinished(
            ulong runtimeInstanceId,
            EventId eventId,
            bool succeeded)
        {
            var completion = new EventCompletion(
                eventId,
                succeeded,
                eventTerminationCause);
            if (eventCommandIds.ContainsKey(runtimeInstanceId)
                || spawningEventCommandId != 0UL)
            {
                pendingEventCompletions[runtimeInstanceId] = completion;
            }

            if (eventCommandIds.ContainsKey(runtimeInstanceId))
            {
                if (completion.IsCancellation)
                {
                    CancelEventCommand(
                        runtimeInstanceId,
                        eventId,
                        completion.CancellationCause);
                }
                else
                {
                    CompleteEventCommand(
                        runtimeInstanceId,
                        eventId,
                        succeeded);
                }
            }
        }

        private void HandleAccidentFinished(
            uint runtimeInstanceId,
            PHSShipAccidentId accidentId,
            bool succeeded)
        {
            var completion = new AccidentCompletion(
                accidentId,
                succeeded,
                accidentTerminationCause);
            if (accidentCommandIds.ContainsKey(runtimeInstanceId)
                || spawningAccidentCommandId != 0UL)
            {
                pendingAccidentCompletions[runtimeInstanceId] = completion;
            }

            if (accidentCommandIds.ContainsKey(runtimeInstanceId))
            {
                if (completion.IsCancellation)
                {
                    CancelAccidentCommand(
                        runtimeInstanceId,
                        accidentId,
                        completion.CancellationCause);
                }
                else
                {
                    CompleteAccidentCommand(
                        runtimeInstanceId,
                        accidentId,
                        succeeded);
                }
            }
        }

        private void CompleteEventCommand(
            ulong runtimeInstanceId,
            EventId eventId,
            bool succeeded)
        {
            ProcessEventCompletion(
                runtimeInstanceId,
                new EventCompletion(eventId, succeeded));
        }

        private void CancelEventCommand(
            ulong runtimeInstanceId,
            EventId eventId,
            string cause)
        {
            ProcessEventCompletion(
                runtimeInstanceId,
                new EventCompletion(eventId, false, cause));
        }

        private void ProcessEventCompletion(
            ulong runtimeInstanceId,
            EventCompletion completion)
        {
            pendingEventCompletions[runtimeInstanceId] = completion;
            if (!eventCommandIds.TryGetValue(
                    runtimeInstanceId,
                    out var commandId)
                || incidentLedger == null)
            {
                return;
            }

            if (completion.IsCancellation)
            {
                if (!incidentLedger.TryCancelCommandServer(
                        commandId,
                        completion.CancellationCause,
                        out var cancelReason))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_CANCEL_FAILED command={commandId} " +
                        $"runtime={runtimeInstanceId} " +
                        $"cause={completion.CancellationCause} " +
                        $"reason={cancelReason}",
                        this);
                    return;
                }
            }
            else
            {
                if (incidentLedger.TryGetCommand(commandId, out var command)
                    && command.IsTerminal)
                {
                    TryFinalizeEventRuntimeTracking(
                        runtimeInstanceId,
                        commandId);
                    return;
                }

                var executorId = eventCoordinator.NetworkObjectId;
                if (!incidentLedger.TryCompleteCommandServer(
                        commandId,
                        executorId,
                        completion.Succeeded,
                        $"event_finished:{completion.EventId}",
                        out var completeReason))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_COMPLETE_FAILED command={commandId} " +
                        $"runtime={runtimeInstanceId} " +
                        $"reason={completeReason}",
                        this);
                    return;
                }
            }

            TryFinalizeEventRuntimeTracking(runtimeInstanceId, commandId);
        }

        private void CompleteAccidentCommand(
            uint runtimeInstanceId,
            PHSShipAccidentId accidentId,
            bool succeeded)
        {
            ProcessAccidentCompletion(
                runtimeInstanceId,
                new AccidentCompletion(accidentId, succeeded));
        }

        private void CancelAccidentCommand(
            uint runtimeInstanceId,
            PHSShipAccidentId accidentId,
            string cause)
        {
            ProcessAccidentCompletion(
                runtimeInstanceId,
                new AccidentCompletion(accidentId, false, cause));
        }

        private void ProcessAccidentCompletion(
            uint runtimeInstanceId,
            AccidentCompletion completion)
        {
            pendingAccidentCompletions[runtimeInstanceId] = completion;
            if (!accidentCommandIds.TryGetValue(
                    runtimeInstanceId,
                    out var commandId)
                || incidentLedger == null)
            {
                return;
            }

            if (completion.IsCancellation)
            {
                if (!incidentLedger.TryCancelCommandServer(
                        commandId,
                        completion.CancellationCause,
                        out var cancelReason))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_CANCEL_FAILED command={commandId} " +
                        $"runtime={runtimeInstanceId} " +
                        $"cause={completion.CancellationCause} " +
                        $"reason={cancelReason}",
                        this);
                    return;
                }
            }
            else
            {
                if (incidentLedger.TryGetCommand(commandId, out var command)
                    && command.IsTerminal)
                {
                    TryFinalizeAccidentRuntimeTracking(
                        runtimeInstanceId,
                        commandId);
                    return;
                }

                var executorId = accidentCoordinator.NetworkObjectId;
                if (!incidentLedger.TryCompleteCommandServer(
                        commandId,
                        executorId,
                        completion.Succeeded,
                        $"accident_finished:{completion.AccidentId}",
                        out var completeReason))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_COMPLETE_FAILED command={commandId} " +
                        $"runtime={runtimeInstanceId} " +
                        $"reason={completeReason}",
                        this);
                    return;
                }
            }

            TryFinalizeAccidentRuntimeTracking(runtimeInstanceId, commandId);
        }

        private void RetryPendingRuntimeCompletions()
        {
            if (pendingEventCompletions.Count > 0)
            {
                var pendingEvents =
                    new List<KeyValuePair<ulong, EventCompletion>>(
                        pendingEventCompletions);
                foreach (var pair in pendingEvents)
                {
                    if (eventCommandIds.ContainsKey(pair.Key))
                    {
                        ProcessEventCompletion(pair.Key, pair.Value);
                    }
                }
            }

            if (pendingAccidentCompletions.Count > 0)
            {
                var pendingAccidents =
                    new List<KeyValuePair<uint, AccidentCompletion>>(
                        pendingAccidentCompletions);
                foreach (var pair in pendingAccidents)
                {
                    if (accidentCommandIds.ContainsKey(pair.Key))
                    {
                        ProcessAccidentCompletion(pair.Key, pair.Value);
                    }
                }
            }
        }

        private bool TryFinalizeEventRuntimeTracking(
            ulong runtimeInstanceId,
            ulong commandId)
        {
            if (eventLocationIds.TryGetValue(
                    runtimeInstanceId,
                    out var locationId)
                && !ReleaseManagedLocation(locationId, commandId))
            {
                return false;
            }

            eventLocationIds.Remove(runtimeInstanceId);
            eventCommandIds.Remove(runtimeInstanceId);
            eventContentIds.Remove(runtimeInstanceId);
            pendingEventCompletions.Remove(runtimeInstanceId);
            return true;
        }

        private bool TryFinalizeAccidentRuntimeTracking(
            uint runtimeInstanceId,
            ulong commandId)
        {
            if (accidentLocationIds.TryGetValue(
                    runtimeInstanceId,
                    out var locationId)
                && !ReleaseManagedLocation(locationId, commandId))
            {
                return false;
            }

            accidentLocationIds.Remove(runtimeInstanceId);
            accidentCommandIds.Remove(runtimeInstanceId);
            accidentContentIds.Remove(runtimeInstanceId);
            pendingAccidentCompletions.Remove(runtimeInstanceId);
            return true;
        }

        private bool TryReconcileTerminatedEventCommands(
            string cause,
            out string reason)
        {
            var trackedCommands =
                new List<KeyValuePair<ulong, ulong>>(eventCommandIds);
            foreach (var pair in trackedCommands)
            {
                var eventId = default(EventId);
                if (pendingEventCompletions.TryGetValue(
                        pair.Key,
                        out var completion))
                {
                    eventId = completion.EventId;
                }
                else if (eventContentIds.TryGetValue(
                    pair.Key,
                    out var trackedEventId))
                {
                    eventId = trackedEventId;
                }
                else if (incidentLedger != null
                    && incidentLedger.TryGetCommand(
                        pair.Value,
                        out var command))
                {
                    eventId = (EventId)command.ContentId;
                }

                CancelEventCommand(pair.Key, eventId, cause);
                if (eventCommandIds.ContainsKey(pair.Key))
                {
                    reason =
                        $"event_runtime_tracking_unresolved:{pair.Key}:{pair.Value}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool TryReconcileTerminatedAccidentCommands(
            string cause,
            out string reason)
        {
            var trackedCommands =
                new List<KeyValuePair<uint, ulong>>(accidentCommandIds);
            foreach (var pair in trackedCommands)
            {
                var accidentId = default(PHSShipAccidentId);
                if (pendingAccidentCompletions.TryGetValue(
                        pair.Key,
                        out var completion))
                {
                    accidentId = completion.AccidentId;
                }
                else if (accidentContentIds.TryGetValue(
                    pair.Key,
                    out var trackedAccidentId))
                {
                    accidentId = trackedAccidentId;
                }
                else if (incidentLedger != null
                    && incidentLedger.TryGetCommand(
                        pair.Value,
                        out var command))
                {
                    accidentId =
                        (PHSShipAccidentId)(ushort)command.ContentId;
                }

                CancelAccidentCommand(pair.Key, accidentId, cause);
                if (accidentCommandIds.ContainsKey(pair.Key))
                {
                    reason =
                        $"accident_runtime_tracking_unresolved:{pair.Key}:{pair.Value}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool TryResolveExternalRoom(
            in NetworkRunIncidentCommand command,
            out ShipRoom room,
            out string resolvedTargetId,
            out string managedLocationId,
            out string reason)
        {
            var requestedTargetId = command.TargetId.ToString();
            if (!string.IsNullOrEmpty(requestedTargetId))
            {
                if (incidentLayout != null
                    && incidentLayout.TryResolve(
                        requestedTargetId,
                        out var requestedLocation)
                    && SupportsCommand(requestedLocation, command)
                    && requestedLocation.RuntimeTarget is ShipRoom layoutRoom)
                {
                    room = layoutRoom;
                    resolvedTargetId = requestedLocation.LocationId;
                    managedLocationId = requestedLocation.LocationId;
                    reason = null;
                    return true;
                }

                if (incidentLayout != null && !allowLegacyLocationFallback)
                {
                    room = null;
                    resolvedTargetId = null;
                    managedLocationId = null;
                    reason =
                        $"layout_location_incompatible:{requestedTargetId}";
                    return false;
                }

                foreach (var configuredRoom in configuredRooms)
                {
                    if (string.Equals(
                            configuredRoom.RoomId,
                            requestedTargetId,
                            StringComparison.Ordinal))
                    {
                        room = configuredRoom;
                        resolvedTargetId = configuredRoom.RoomId;
                        managedLocationId = null;
                        reason = null;
                        return true;
                    }
                }

                room = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = $"configured_room_missing:{requestedTargetId}";
                return false;
            }

            if (!randomLedger.TryCreateServerScope(
                    NetworkRunRandomStream.ExternalThreat,
                    command.CommandId,
                    out var random,
                    out var randomReason))
            {
                room = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = $"random_scope_failed:{randomReason}";
                return false;
            }

            if (TrySelectLayoutLocation(
                    command,
                    random,
                    candidate => candidate.RuntimeTarget is ShipRoom,
                    out var selectedLocation))
            {
                room = (ShipRoom)selectedLocation.RuntimeTarget;
                resolvedTargetId = selectedLocation.LocationId;
                managedLocationId = selectedLocation.LocationId;
                reason = null;
                return true;
            }

            if (incidentLayout != null && !allowLegacyLocationFallback)
            {
                room = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = "compatible_layout_location_unavailable";
                return false;
            }

            room = configuredRooms[random.NextInt(configuredRooms.Length)];
            resolvedTargetId = room.RoomId;
            managedLocationId = null;
            reason = null;
            return true;
        }

        private bool TryResolveInternalAnchorId(
            in NetworkRunIncidentCommand command,
            out string anchorId,
            out string resolvedTargetId,
            out string managedLocationId,
            out string reason)
        {
            var requestedTargetId = command.TargetId.ToString();
            if (!string.IsNullOrEmpty(requestedTargetId))
            {
                if (incidentLayout != null
                    && incidentLayout.TryResolve(
                        requestedTargetId,
                        out var requestedLocation)
                    && SupportsCommand(requestedLocation, command)
                    && requestedLocation.RuntimeTarget
                        is PHSShipAccidentAnchor layoutAnchor
                    && IsCompatibleAnchorId(layoutAnchor.AnchorId))
                {
                    anchorId = layoutAnchor.AnchorId;
                    resolvedTargetId = requestedLocation.LocationId;
                    managedLocationId = requestedLocation.LocationId;
                    reason = null;
                    return true;
                }

                if (incidentLayout != null && !allowLegacyLocationFallback)
                {
                    anchorId = null;
                    resolvedTargetId = null;
                    managedLocationId = null;
                    reason =
                        $"layout_location_incompatible:{requestedTargetId}";
                    return false;
                }

                if (compatibleAnchorIds.BinarySearch(
                        requestedTargetId,
                        StringComparer.Ordinal) >= 0)
                {
                    anchorId = requestedTargetId;
                    resolvedTargetId = requestedTargetId;
                    managedLocationId = null;
                    reason = null;
                    return true;
                }

                anchorId = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = $"compatible_anchor_missing:{requestedTargetId}";
                return false;
            }

            if (!randomLedger.TryCreateServerScope(
                    NetworkRunRandomStream.InternalAccidentAnchor,
                    command.CommandId,
                    out var random,
                    out var randomReason))
            {
                anchorId = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = $"random_scope_failed:{randomReason}";
                return false;
            }

            if (TrySelectLayoutLocation(
                    command,
                    random,
                    candidate =>
                        candidate.RuntimeTarget is PHSShipAccidentAnchor target
                        && IsCompatibleAnchorId(target.AnchorId),
                    out var selectedLocation))
            {
                anchorId =
                    ((PHSShipAccidentAnchor)selectedLocation.RuntimeTarget)
                    .AnchorId;
                resolvedTargetId = selectedLocation.LocationId;
                managedLocationId = selectedLocation.LocationId;
                reason = null;
                return true;
            }

            if (incidentLayout != null && !allowLegacyLocationFallback)
            {
                anchorId = null;
                resolvedTargetId = null;
                managedLocationId = null;
                reason = "compatible_layout_location_unavailable";
                return false;
            }

            anchorId =
                compatibleAnchorIds[random.NextInt(compatibleAnchorIds.Count)];
            resolvedTargetId = anchorId;
            managedLocationId = null;
            reason = null;
            return true;
        }

        private bool TrySelectLayoutLocation(
            in NetworkRunIncidentCommand command,
            PHSDeterministicRandom random,
            Predicate<IIncidentLocation> runtimeTargetPredicate,
            out IIncidentLocation selectedLocation)
        {
            selectedLocation = null;
            if (incidentLayout == null
                || random == null
                || runtimeTargetPredicate == null
                || runSessionRoot == null
                || runSessionRoot.NetworkManager == null)
            {
                return false;
            }

            var query = CreateLocationQuery(command, null);
            if (!incidentLayout.TryCopyCandidates(
                    query,
                    locationCandidates,
                    out _))
            {
                return false;
            }

            var totalWeight = 0d;
            foreach (var candidate in locationCandidates)
            {
                if (candidate != null && runtimeTargetPredicate(candidate))
                {
                    totalWeight += candidate.SelectionWeight;
                }
            }

            if (double.IsNaN(totalWeight)
                || double.IsInfinity(totalWeight)
                || totalWeight <= 0d)
            {
                return false;
            }

            var weightedRoll =
                ((random.NextUInt64() >> 11) * UnitDoubleFromUInt64)
                * totalWeight;
            IIncidentLocation lastCandidate = null;
            foreach (var candidate in locationCandidates)
            {
                if (candidate == null || !runtimeTargetPredicate(candidate))
                {
                    continue;
                }

                lastCandidate = candidate;
                weightedRoll -= candidate.SelectionWeight;
                if (weightedRoll < 0d)
                {
                    selectedLocation = candidate;
                    return true;
                }
            }

            selectedLocation = lastCandidate;
            return selectedLocation != null;
        }

        private bool SupportsCommand(
            IIncidentLocation location,
            in NetworkRunIncidentCommand command)
        {
            return location != null
                && runSessionRoot != null
                && runSessionRoot.NetworkManager != null
                && location.Supports(
                    CreateLocationQuery(command, location.LocationId));
        }

        private IncidentLocationQuery CreateLocationQuery(
            in NetworkRunIncidentCommand command,
            string requestedLocationId)
        {
            var requiredCapabilities =
                command.IncidentFamily == NetworkRunIncidentFamily.Fire
                    ? IncidentLocationCapability.HazardArea
                        | IncidentLocationCapability.FirePropagation
                    : IncidentLocationCapability.None;
            return new IncidentLocationQuery(
                command.Channel,
                command.IncidentFamily,
                command.ContentId,
                NetworkShipModuleId.None,
                IncidentLocationKind.None,
                requiredCapabilities,
                null,
                requestedLocationId,
                runSessionRoot.NetworkManager.ServerTime.Time,
                true);
        }

        private bool TryOccupyManagedLocation(
            string locationId,
            ulong commandId,
            out string reason)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                reason = null;
                return true;
            }

            if (incidentLayout == null
                || runSessionRoot == null
                || runSessionRoot.NetworkManager == null)
            {
                reason = "incident_layout_not_ready";
                return false;
            }

            return incidentLayout.TryOccupy(
                locationId,
                commandId,
                runSessionRoot.NetworkManager.ServerTime.Time,
                out reason);
        }

        private bool ReleaseManagedLocation(
            string locationId,
            ulong commandId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return true;
            }

            if (incidentLayout == null
                || runSessionRoot == null
                || runSessionRoot.NetworkManager == null)
            {
                return false;
            }

            if (!incidentLayout.TryResolve(
                    locationId,
                    out var location))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_LOCATION_RELEASE_FAILED " +
                    $"location={locationId} command={commandId} " +
                    $"reason=location_missing",
                    this);
                return false;
            }

            if (!location.IsOccupied)
            {
                return true;
            }

            if (location.OccupantId != commandId)
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_LOCATION_RELEASE_FAILED " +
                    $"location={locationId} command={commandId} " +
                    $"reason=occupant_mismatch:{location.OccupantId}",
                    this);
                return false;
            }

            if (!incidentLayout.TryRelease(
                    locationId,
                    commandId,
                    runSessionRoot.NetworkManager.ServerTime.Time,
                    out var reason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_LOCATION_RELEASE_FAILED " +
                    $"location={locationId} command={commandId} " +
                    $"reason={reason}",
                    this);
                return false;
            }

            return true;
        }

        private bool IsCompatibleAnchorId(string anchorId)
        {
            return !string.IsNullOrWhiteSpace(anchorId)
                && compatibleAnchorIds.BinarySearch(
                    anchorId,
                    StringComparer.Ordinal) >= 0;
        }

        private void CancelPendingCommand(ulong commandId, string cause)
        {
            if (incidentLedger == null)
            {
                return;
            }

            if (!incidentLedger.TryCancelCommandServer(
                    commandId,
                    cause,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CANCEL_FAILED command={commandId} " +
                    $"cause={cause} reason={reason}",
                    this);
            }
        }

        private void CancelClaimedCommand(ulong commandId, string cause)
        {
            CancelPendingCommand(commandId, cause);
        }

        private bool TryValidateAndSortSetup()
        {
            if (eventCoordinator == null)
            {
                Debug.LogError(
                    "PHS_INCIDENT_CONSUMER_SETUP_FAILED reason=event_coordinator_missing",
                    this);
                return false;
            }

            if (accidentCoordinator == null)
            {
                Debug.LogError(
                    "PHS_INCIDENT_CONSUMER_SETUP_FAILED reason=accident_coordinator_missing",
                    this);
                return false;
            }

            if (incidentLayout != null
                && !incidentLayout.TryValidate(out var layoutReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUMER_SETUP_FAILED " +
                    $"reason=incident_layout_invalid detail={layoutReason}",
                    this);
                return false;
            }

            if (rooms == null || rooms.Length == 0)
            {
                Debug.LogError(
                    "PHS_INCIDENT_CONSUMER_SETUP_FAILED reason=rooms_empty",
                    this);
                return false;
            }

            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            configuredRooms = (ShipRoom[])rooms.Clone();
            foreach (var room in configuredRooms)
            {
                if (room == null)
                {
                    Debug.LogError(
                        "PHS_INCIDENT_CONSUMER_SETUP_FAILED reason=room_missing",
                        this);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(room.RoomId))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_CONSUMER_SETUP_FAILED " +
                        $"reason=room_id_missing room={room.name}",
                        this);
                    return false;
                }

                if (!CanFitFixedString64(room.RoomId))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_CONSUMER_SETUP_FAILED " +
                        $"reason=room_id_too_long room={room.name}",
                        this);
                    return false;
                }

                if (!roomIds.Add(room.RoomId))
                {
                    Debug.LogError(
                        $"PHS_INCIDENT_CONSUMER_SETUP_FAILED " +
                        $"reason=room_id_duplicate roomId={room.RoomId}",
                        this);
                    return false;
                }
            }

            Array.Sort(
                configuredRooms,
                (left, right) =>
                    StringComparer.Ordinal.Compare(left.RoomId, right.RoomId));
            return true;
        }

        private static bool CanFitFixedString64(string value)
        {
            var fixedValue = default(FixedString64Bytes);
            return !string.IsNullOrWhiteSpace(value)
                && fixedValue.CopyFrom(value) == CopyError.None;
        }
    }
}
