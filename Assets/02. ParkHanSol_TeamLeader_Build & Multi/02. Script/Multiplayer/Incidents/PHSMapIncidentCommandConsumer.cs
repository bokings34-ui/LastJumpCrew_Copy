using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
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
        [Header("Inspector References")]
        [SerializeField] private NetworkEventCoordinator eventCoordinator;
        [SerializeField] private PHSNetworkShipAccidentCoordinator accidentCoordinator;
        [SerializeField] private ShipRoom[] rooms = Array.Empty<ShipRoom>();

        private readonly List<NetworkRunIncidentCommand> pendingCommands = new();
        private readonly List<string> compatibleAnchorIds = new();
        private readonly Dictionary<ulong, ulong> eventCommandIds = new();
        private readonly Dictionary<uint, ulong> accidentCommandIds = new();

        private ShipRoom[] configuredRooms = Array.Empty<ShipRoom>();
        private NetworkRunSessionRoot runSessionRoot;
        private NetworkRunIncidentLedger incidentLedger;
        private NetworkRunRandomLedger randomLedger;

        public NetworkEventCoordinator EventCoordinator => eventCoordinator;
        public PHSNetworkShipAccidentCoordinator AccidentCoordinator => accidentCoordinator;
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

            var eventTerminated = eventCoordinator.TryTerminateAllServer();
            var accidentsTerminated =
                accidentCoordinator.TryTerminateAllServer(
                    cause,
                    out var accidentReason);
            eventCommandIds.Clear();
            accidentCommandIds.Clear();
            if (!eventTerminated || !accidentsTerminated)
            {
                reason =
                    $"runtime_termination_failed:" +
                    $"event={eventTerminated}:" +
                    $"accident={accidentsTerminated}:" +
                    $"detail={accidentReason ?? "none"}";
                return false;
            }

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
            if (command.PayloadKind != NetworkRunIncidentPayloadKind.EventManagerEvent)
            {
                CancelPendingCommand(command.CommandId, "payload_kind_invalid");
                return;
            }

            if (!Enum.IsDefined(typeof(EventId), command.ContentId))
            {
                CancelPendingCommand(command.CommandId, "content_id_invalid");
                return;
            }

            if (!eventCoordinator.IsSpawned || !eventCoordinator.IsServer)
            {
                return;
            }

            if (!randomLedger.TryCreateServerScope(
                    NetworkRunRandomStream.ExternalThreat,
                    command.CommandId,
                    out var random,
                    out var randomReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=random_scope_failed detail={randomReason}",
                    this);
                CancelPendingCommand(command.CommandId, "random_scope_failed");
                return;
            }

            var room = configuredRooms[random.NextInt(configuredRooms.Length)];
            var executorId = eventCoordinator.NetworkObjectId;
            if (!incidentLedger.TryClaimCommandServer(
                    command.CommandId,
                    executorId,
                    out var claimed,
                    out _))
            {
                return;
            }

            var eventId = (EventId)claimed.ContentId;
            ulong runtimeInstanceId;
            try
            {
                if (!eventCoordinator.TrySpawnEventServer(
                        eventId,
                        room,
                        out runtimeInstanceId))
                {
                    CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                return;
            }

            if (eventCommandIds.ContainsKey(runtimeInstanceId))
            {
                CancelClaimedCommand(claimed.CommandId, "runtime_id_collision");
                return;
            }

            if (!incidentLedger.TryActivateCommandServer(
                    claimed.CommandId,
                    executorId,
                    runtimeInstanceId,
                    room.RoomId,
                    out var activateReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=activate_failed detail={activateReason}",
                    this);
                CancelClaimedCommand(claimed.CommandId, "activate_failed");
                return;
            }

            eventCommandIds.Add(runtimeInstanceId, claimed.CommandId);
            Debug.Log(
                $"PHS_INCIDENT_EXTERNAL_ACTIVATED command={claimed.CommandId} " +
                $"runtime={runtimeInstanceId} event={eventId} room={room.RoomId}",
                this);
        }

        private void ProcessInternalCommand(NetworkRunIncidentCommand command)
        {
            if (command.PayloadKind != NetworkRunIncidentPayloadKind.ShipAccident)
            {
                CancelPendingCommand(command.CommandId, "payload_kind_invalid");
                return;
            }

            if (command.ContentId <= (int)PHSShipAccidentId.None
                || command.ContentId > ushort.MaxValue
                || !Enum.IsDefined(
                    typeof(PHSShipAccidentId),
                    (ushort)command.ContentId))
            {
                CancelPendingCommand(command.CommandId, "content_id_invalid");
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
            if (!randomLedger.TryCreateServerScope(
                    NetworkRunRandomStream.InternalAccidentAnchor,
                    command.CommandId,
                    out var random,
                    out var randomReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={command.CommandId} " +
                    $"reason=random_scope_failed detail={randomReason}",
                    this);
                CancelPendingCommand(command.CommandId, "random_scope_failed");
                return;
            }

            var anchorId = compatibleAnchorIds[random.NextInt(compatibleAnchorIds.Count)];
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

            uint runtimeInstanceId;
            try
            {
                if (!accidentCoordinator.TrySpawnAccidentServer(
                        accidentId,
                        anchorId,
                        out runtimeInstanceId,
                        out var spawnReason))
                {
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
                Debug.LogException(exception, this);
                CancelClaimedCommand(claimed.CommandId, "spawn_failed");
                return;
            }

            if (accidentCommandIds.ContainsKey(runtimeInstanceId))
            {
                CancelClaimedCommand(claimed.CommandId, "runtime_id_collision");
                return;
            }

            if (!incidentLedger.TryActivateCommandServer(
                    claimed.CommandId,
                    executorId,
                    runtimeInstanceId,
                    anchorId,
                    out var activateReason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSUME_FAILED command={claimed.CommandId} " +
                    $"reason=activate_failed detail={activateReason}",
                    this);
                CancelClaimedCommand(claimed.CommandId, "activate_failed");
                return;
            }

            accidentCommandIds.Add(runtimeInstanceId, claimed.CommandId);
            Debug.Log(
                $"PHS_INCIDENT_INTERNAL_ACTIVATED command={claimed.CommandId} " +
                $"runtime={runtimeInstanceId} accident={accidentId} anchor={anchorId}",
                this);
        }

        private void HandleEventFinished(
            ulong runtimeInstanceId,
            EventId eventId,
            bool succeeded)
        {
            if (!eventCommandIds.Remove(runtimeInstanceId, out var commandId)
                || incidentLedger == null)
            {
                return;
            }

            var executorId = eventCoordinator.NetworkObjectId;
            if (!incidentLedger.TryCompleteCommandServer(
                    commandId,
                    executorId,
                    succeeded,
                    $"event_finished:{eventId}",
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_COMPLETE_FAILED command={commandId} " +
                    $"runtime={runtimeInstanceId} reason={reason}",
                    this);
            }
        }

        private void HandleAccidentFinished(
            uint runtimeInstanceId,
            PHSShipAccidentId accidentId,
            bool succeeded)
        {
            if (!accidentCommandIds.Remove(runtimeInstanceId, out var commandId)
                || incidentLedger == null)
            {
                return;
            }

            var executorId = accidentCoordinator.NetworkObjectId;
            if (!incidentLedger.TryCompleteCommandServer(
                    commandId,
                    executorId,
                    succeeded,
                    $"accident_finished:{accidentId}",
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_INCIDENT_COMPLETE_FAILED command={commandId} " +
                    $"runtime={runtimeInstanceId} reason={reason}",
                    this);
            }
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
