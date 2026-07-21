using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Persistent server-owned incident pressure budget and command ledger.
    /// Content directors reserve work here; scene consumers claim and execute it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRunIncidentLedger :
        NetworkBehaviour,
        IRunIncidentLedger
    {
        private const int MinimumCommandHistory = 32;

        [Header("Incident Budget Defaults")]
        [SerializeField, Min(1)] private int defaultPressureCapacity = 3;
        [SerializeField, Range(0, byte.MaxValue)]
        private int defaultMaximumExternalCommands = 1;
        [SerializeField, Range(0, byte.MaxValue)]
        private int defaultMaximumInternalCommands = 2;
        [SerializeField, Min(MinimumCommandHistory)]
        private int maximumCommandHistory = 128;

        private readonly NetworkVariable<NetworkRunIncidentSnapshot>
            synchronizedSnapshot = new(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkRunIncidentCommand>
            synchronizedCommands = new(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly Dictionary<string, NetworkRunIncidentRequest>
            acceptedRequests = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ulong> requestCommandIds = new(
            StringComparer.Ordinal);
        private readonly Dictionary<ulong, NetworkRunIncidentCommand>
            currentStageCommandRecords = new();
        private readonly Queue<NetworkListEvent<NetworkRunIncidentCommand>>
            pendingCommandEvents = new();

        private byte stageMaximumExternalCommands;
        private byte stageMaximumInternalCommands;
        private FixedString64Bytes lastStageCancelCause;
        private bool isSubscribed;

        public NetworkRunIncidentSnapshot Snapshot =>
            synchronizedSnapshot.Value;
        public int CommandCount => synchronizedCommands.Count;

        public event Action<
            NetworkRunIncidentSnapshot,
            NetworkRunIncidentSnapshot> SnapshotChanged;
        public event Action<
            NetworkListEvent<NetworkRunIncidentCommand>> CommandChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_RUN_INCIDENT_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            Subscribe();
            if (IsServer)
            {
                InitializeServerState();
            }

            Debug.Log(
                $"PHS_RUN_INCIDENT_READY server={IsServer} state={Snapshot.State} " +
                $"stage={Snapshot.StageSequence} revision={Snapshot.Revision} " +
                $"commands={CommandCount}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
            acceptedRequests.Clear();
            requestCommandIds.Clear();
            currentStageCommandRecords.Clear();
            pendingCommandEvents.Clear();
            base.OnNetworkDespawn();
        }

        public NetworkRunIncidentCommand GetCommandAt(int index)
        {
            if (index < 0 || index >= synchronizedCommands.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return synchronizedCommands[index];
        }

        public bool TryGetCommand(
            ulong commandId,
            out NetworkRunIncidentCommand command)
        {
            if (TryFindCommandIndex(commandId, out var index))
            {
                command = synchronizedCommands[index];
                return true;
            }

            command = default;
            return false;
        }

        public bool TryBeginStageServer(
            int mapId,
            uint stageSequence,
            ushort pressureCapacity,
            byte maximumExternalCommands,
            byte maximumInternalCommands,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (mapId <= 0)
            {
                reason = "positive_map_id_required";
                return false;
            }

            if (stageSequence == 0U)
            {
                reason = "nonzero_stage_sequence_required";
                return false;
            }

            if (pressureCapacity == 0)
            {
                reason = "positive_pressure_capacity_required";
                return false;
            }

            if (maximumExternalCommands == 0
                && maximumInternalCommands == 0)
            {
                reason = "incident_channel_capacity_required";
                return false;
            }

            var current = Snapshot;
            if (current.State == NetworkRunIncidentStageState.Active)
            {
                if (current.MapId == mapId
                    && current.StageSequence == stageSequence
                    && current.PressureCapacity == pressureCapacity
                    && stageMaximumExternalCommands
                        == maximumExternalCommands
                    && stageMaximumInternalCommands
                        == maximumInternalCommands)
                {
                    reason = null;
                    return true;
                }

                reason =
                    $"stage_already_active:{current.StageSequence}:{current.MapId}";
                return false;
            }

            if (current.State == NetworkRunIncidentStageState.Cancelled
                && current.StageSequence == stageSequence)
            {
                reason = "stage_sequence_already_closed";
                return false;
            }

            if (HasNonTerminalCommand())
            {
                reason = "unfinished_commands_present";
                return false;
            }

            stageMaximumExternalCommands = maximumExternalCommands;
            stageMaximumInternalCommands = maximumInternalCommands;
            acceptedRequests.Clear();
            requestCommandIds.Clear();
            currentStageCommandRecords.Clear();
            lastStageCancelCause = default;
            synchronizedSnapshot.Value = new NetworkRunIncidentSnapshot(
                mapId,
                stageSequence,
                NetworkRunIncidentStageState.Active,
                pressureCapacity,
                0,
                0,
                0,
                0,
                0U,
                0U,
                current.NextCommandId == 0UL
                    ? 1UL
                    : current.NextCommandId,
                1f,
                IncrementNonZero(current.Revision));
            reason = null;
            Debug.Log(
                $"PHS_RUN_INCIDENT_STAGE_BEGAN map={mapId} stage={stageSequence} " +
                $"pressure={pressureCapacity} externalCap={maximumExternalCommands} " +
                $"internalCap={maximumInternalCommands} revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryReserveCommandServer(
            in NetworkRunIncidentRequest request,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!RequireServer(out reason)
                || !TryValidateRequest(
                    request,
                    out var requestKey,
                    out reason))
            {
                return false;
            }

            if (acceptedRequests.TryGetValue(
                    requestKey,
                    out var acceptedRequest))
            {
                if (!acceptedRequest.Equals(request))
                {
                    reason = "request_id_conflict";
                    return false;
                }

                if (!requestCommandIds.TryGetValue(
                        requestKey,
                        out var existingCommandId)
                    || !currentStageCommandRecords.TryGetValue(
                        existingCommandId,
                        out command))
                {
                    reason = "request_idempotency_record_missing";
                    return false;
                }

                reason = null;
                return true;
            }

            var current = Snapshot;
            if (current.State != NetworkRunIncidentStageState.Active)
            {
                reason = $"stage_not_active:{current.State}";
                return false;
            }

            if (request.StageSequence != current.StageSequence)
            {
                reason =
                    $"request_stage_mismatch:{request.StageSequence}!=" +
                    $"{current.StageSequence}";
                return false;
            }

            if (request.MapId != current.MapId)
            {
                reason =
                    $"request_map_mismatch:{request.MapId}!={current.MapId}";
                return false;
            }

            if (request.ParentCommandId != 0UL
                && !currentStageCommandRecords.ContainsKey(
                    request.ParentCommandId))
            {
                reason = "parent_command_missing";
                return false;
            }

            var usedPressure =
                (uint)current.ReservedPressure + current.ActivePressure;
            if (request.PressureCost > current.PressureCapacity
                || usedPressure + request.PressureCost
                    > current.PressureCapacity)
            {
                reason = "incident_pressure_capacity_exceeded";
                return false;
            }

            if (request.Channel == NetworkRunIncidentChannel.External
                && current.ActiveExternalCount
                    >= stageMaximumExternalCommands)
            {
                reason = "external_command_cap_reached";
                return false;
            }

            if (request.Channel == NetworkRunIncidentChannel.Internal
                && current.ActiveInternalCount
                    >= stageMaximumInternalCommands)
            {
                reason = "internal_command_cap_reached";
                return false;
            }

            var commandId = current.NextCommandId == 0UL
                ? 1UL
                : current.NextCommandId;
            if (TryFindCommandIndex(commandId, out _)
                || currentStageCommandRecords.ContainsKey(commandId))
            {
                reason = "command_id_exhausted";
                return false;
            }

            if (!TryResolveHistoryWriteSlot(
                    out var historyReplacementIndex,
                    out reason))
            {
                return false;
            }

            var commitRevision = IncrementNonZero(current.Revision);
            command = new NetworkRunIncidentCommand(
                commandId,
                request.RequestId,
                request.ParentCommandId,
                request.StageSequence,
                request.MapId,
                request.Channel,
                request.PayloadKind,
                request.IncidentFamily,
                request.ContentId,
                request.SourceKind,
                request.PressureCost,
                request.WarpChargeMultiplier,
                NetworkRunIncidentCommandState.Pending,
                0UL,
                0UL,
                request.TargetId,
                default,
                default,
                commitRevision,
                commitRevision,
                NetworkManager.ServerTime.Time);
            var nextSnapshot = new NetworkRunIncidentSnapshot(
                current.MapId,
                current.StageSequence,
                current.State,
                current.PressureCapacity,
                (ushort)(current.ReservedPressure + request.PressureCost),
                current.ActivePressure,
                request.Channel == NetworkRunIncidentChannel.External
                    ? (byte)(current.ActiveExternalCount + 1)
                    : current.ActiveExternalCount,
                request.Channel == NetworkRunIncidentChannel.Internal
                    ? (byte)(current.ActiveInternalCount + 1)
                    : current.ActiveInternalCount,
                IncrementNonZero(current.StageIssuedCount),
                current.StageResolvedCount,
                IncrementNonZero(commandId),
                current.ActiveWarpChargeMultiplier,
                commitRevision);

            try
            {
                if (historyReplacementIndex >= 0)
                {
                    // One revisioned Value event atomically prunes terminal history
                    // and publishes the new pending command.
                    synchronizedCommands[historyReplacementIndex] = command;
                }
                else
                {
                    synchronizedCommands.Add(command);
                }
            }
            catch (Exception exception)
            {
                pendingCommandEvents.Clear();
                Debug.LogError(
                    $"PHS_RUN_INCIDENT_INVARIANT_FAILED operation=" +
                    $"{(historyReplacementIndex >= 0 ? "reserve_replace" : "reserve_append")} " +
                    $"exception={exception.GetType().Name}",
                    this);
                command = default;
                reason = "command_append_failed";
                return false;
            }

            acceptedRequests.Add(requestKey, request);
            requestCommandIds.Add(requestKey, commandId);
            currentStageCommandRecords.Add(commandId, command);
            synchronizedSnapshot.Value = nextSnapshot;
            reason = null;
            Debug.Log(
                $"PHS_RUN_INCIDENT_RESERVED command={commandId} request={request.RequestId} " +
                $"channel={request.Channel} pressure={request.PressureCost} " +
                $"revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryClaimCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (executorNetworkObjectId == 0UL)
            {
                reason = "executor_network_object_id_required";
                return false;
            }

            if (!TryFindCommandIndex(commandId, out var index))
            {
                reason = "command_missing";
                return false;
            }

            var currentCommand = synchronizedCommands[index];
            if (currentCommand.State != NetworkRunIncidentCommandState.Pending)
            {
                if (currentCommand.State
                        == NetworkRunIncidentCommandState.Claimed
                    && currentCommand.ExecutorNetworkObjectId
                        == executorNetworkObjectId)
                {
                    command = currentCommand;
                    reason = null;
                    return true;
                }

                reason = currentCommand.State
                    == NetworkRunIncidentCommandState.Claimed
                        ? $"command_claim_conflict:" +
                          $"{currentCommand.ExecutorNetworkObjectId}"
                        : $"command_not_pending:{currentCommand.State}";
                return false;
            }

            var stateRevision = IncrementNonZero(Snapshot.Revision);
            command = currentCommand.WithState(
                NetworkRunIncidentCommandState.Claimed,
                executorNetworkObjectId,
                0UL,
                currentCommand.TargetId,
                default,
                default,
                stateRevision,
                NetworkManager.ServerTime.Time);
            if (!TryCommitCommandState(
                    index,
                    command,
                    false,
                    out reason))
            {
                command = default;
                return false;
            }

            Debug.Log(
                $"PHS_RUN_INCIDENT_CLAIMED command={commandId} " +
                $"executor={executorNetworkObjectId} revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryActivateCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            ulong runtimeInstanceId,
            string targetId,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (executorNetworkObjectId == 0UL)
            {
                reason = "executor_network_object_id_required";
                return false;
            }

            if (runtimeInstanceId == 0UL)
            {
                reason = "runtime_instance_id_required";
                return false;
            }

            if (!TryFindCommandIndex(commandId, out var index))
            {
                reason = "command_missing";
                return false;
            }

            var currentCommand = synchronizedCommands[index];
            if (!TryCreateActivationTarget(
                    targetId,
                    currentCommand.TargetId,
                    out var fixedTargetId,
                    out reason))
            {
                return false;
            }

            if (currentCommand.State
                != NetworkRunIncidentCommandState.Claimed)
            {
                if (currentCommand.State
                        == NetworkRunIncidentCommandState.Active
                    && currentCommand.ExecutorNetworkObjectId
                        == executorNetworkObjectId
                    && currentCommand.RuntimeInstanceId == runtimeInstanceId
                    && currentCommand.TargetId.Equals(fixedTargetId))
                {
                    reason = null;
                    return true;
                }

                reason = currentCommand.State
                    == NetworkRunIncidentCommandState.Active
                        ? "command_activation_conflict"
                        : $"command_not_claimed:{currentCommand.State}";
                return false;
            }

            if (currentCommand.ExecutorNetworkObjectId
                != executorNetworkObjectId)
            {
                reason = "command_executor_mismatch";
                return false;
            }

            var stateRevision = IncrementNonZero(Snapshot.Revision);
            var nextCommand = currentCommand.WithState(
                NetworkRunIncidentCommandState.Active,
                executorNetworkObjectId,
                runtimeInstanceId,
                fixedTargetId,
                default,
                default,
                stateRevision,
                NetworkManager.ServerTime.Time);
            if (!TryCommitCommandState(
                    index,
                    nextCommand,
                    false,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_RUN_INCIDENT_ACTIVATED command={commandId} " +
                $"runtime={runtimeInstanceId} target={fixedTargetId} " +
                $"revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryCompleteCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            bool succeeded,
            string outcomeId,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (executorNetworkObjectId == 0UL)
            {
                reason = "executor_network_object_id_required";
                return false;
            }

            if (!TryCreateRequiredFixedString64(
                    outcomeId,
                    "outcome_id",
                    out var fixedOutcomeId,
                    out reason))
            {
                return false;
            }

            var terminalState = succeeded
                ? NetworkRunIncidentCommandState.Resolved
                : NetworkRunIncidentCommandState.Failed;
            if (!TryFindCommandIndex(commandId, out var index))
            {
                if (currentStageCommandRecords.TryGetValue(
                        commandId,
                        out var archivedCommand)
                    && archivedCommand.State == terminalState
                    && archivedCommand.ExecutorNetworkObjectId
                        == executorNetworkObjectId
                    && archivedCommand.OutcomeId.Equals(fixedOutcomeId))
                {
                    reason = null;
                    return true;
                }

                reason = "command_missing";
                return false;
            }

            var currentCommand = synchronizedCommands[index];
            if (currentCommand.IsTerminal)
            {
                if (currentCommand.State == terminalState
                    && currentCommand.ExecutorNetworkObjectId
                        == executorNetworkObjectId
                    && currentCommand.OutcomeId.Equals(fixedOutcomeId))
                {
                    reason = null;
                    return true;
                }

                reason =
                    $"command_completion_conflict:{currentCommand.State}";
                return false;
            }

            if (currentCommand.State
                != NetworkRunIncidentCommandState.Active)
            {
                reason = $"command_not_active:{currentCommand.State}";
                return false;
            }

            if (currentCommand.ExecutorNetworkObjectId
                != executorNetworkObjectId)
            {
                reason = "command_executor_mismatch";
                return false;
            }

            var stateRevision = IncrementNonZero(Snapshot.Revision);
            var nextCommand = currentCommand.WithState(
                terminalState,
                executorNetworkObjectId,
                currentCommand.RuntimeInstanceId,
                currentCommand.TargetId,
                fixedOutcomeId,
                default,
                stateRevision,
                NetworkManager.ServerTime.Time);
            if (!TryCommitCommandState(
                    index,
                    nextCommand,
                    true,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_RUN_INCIDENT_COMPLETED command={commandId} " +
                $"state={terminalState} outcome={fixedOutcomeId} " +
                $"revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryCancelCommandServer(
            ulong commandId,
            string cause,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryCreateRequiredFixedString64(
                    cause,
                    "cancel_reason",
                    out var fixedCause,
                    out reason))
            {
                return false;
            }

            if (!TryFindCommandIndex(commandId, out var index))
            {
                if (currentStageCommandRecords.TryGetValue(
                        commandId,
                        out var archivedCommand)
                    && archivedCommand.State
                        == NetworkRunIncidentCommandState.Cancelled
                    && archivedCommand.CancelReason.Equals(fixedCause))
                {
                    reason = null;
                    return true;
                }

                reason = "command_missing";
                return false;
            }

            var currentCommand = synchronizedCommands[index];
            if (currentCommand.State
                == NetworkRunIncidentCommandState.Cancelled)
            {
                if (currentCommand.CancelReason.Equals(fixedCause))
                {
                    reason = null;
                    return true;
                }

                reason = "command_cancel_conflict";
                return false;
            }

            if (currentCommand.IsTerminal)
            {
                reason = $"command_already_terminal:{currentCommand.State}";
                return false;
            }

            var stateRevision = IncrementNonZero(Snapshot.Revision);
            var nextCommand = currentCommand.WithState(
                NetworkRunIncidentCommandState.Cancelled,
                currentCommand.ExecutorNetworkObjectId,
                currentCommand.RuntimeInstanceId,
                currentCommand.TargetId,
                default,
                fixedCause,
                stateRevision,
                NetworkManager.ServerTime.Time);
            if (!TryCommitCommandState(
                    index,
                    nextCommand,
                    true,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_RUN_INCIDENT_CANCELLED command={commandId} " +
                $"cause={fixedCause} revision={Snapshot.Revision}",
                this);
            return true;
        }

        public bool TryCancelStageServer(
            uint stageSequence,
            string cause,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryCreateRequiredFixedString64(
                    cause,
                    "cancel_reason",
                    out var fixedCause,
                    out reason))
            {
                return false;
            }

            var current = Snapshot;
            if (stageSequence != current.StageSequence)
            {
                reason =
                    $"stage_sequence_mismatch:{stageSequence}!=" +
                    $"{current.StageSequence}";
                return false;
            }

            if (current.State == NetworkRunIncidentStageState.Cancelled)
            {
                if (lastStageCancelCause.Equals(fixedCause))
                {
                    reason = null;
                    return true;
                }

                reason = "stage_cancel_conflict";
                return false;
            }

            if (current.State != NetworkRunIncidentStageState.Active)
            {
                reason = $"stage_not_active:{current.State}";
                return false;
            }

            var stateRevision = IncrementNonZero(current.Revision);
            var changedAtServerTime = NetworkManager.ServerTime.Time;
            var indices = new List<int>();
            var previousCommands = new List<NetworkRunIncidentCommand>();
            var cancelledCommands = new List<NetworkRunIncidentCommand>();
            for (var index = 0; index < synchronizedCommands.Count; index++)
            {
                var candidate = synchronizedCommands[index];
                if (candidate.StageSequence != stageSequence
                    || candidate.IsTerminal)
                {
                    continue;
                }

                indices.Add(index);
                previousCommands.Add(candidate);
                cancelledCommands.Add(candidate.WithState(
                    NetworkRunIncidentCommandState.Cancelled,
                    candidate.ExecutorNetworkObjectId,
                    candidate.RuntimeInstanceId,
                    candidate.TargetId,
                    default,
                    fixedCause,
                    stateRevision,
                    changedAtServerTime));
            }

            var appliedCount = 0;
            try
            {
                for (var item = 0; item < indices.Count; item++)
                {
                    synchronizedCommands[indices[item]] =
                        cancelledCommands[item];
                    appliedCount++;
                }
            }
            catch (Exception exception)
            {
                for (var item = appliedCount - 1; item >= 0; item--)
                {
                    synchronizedCommands[indices[item]] =
                        previousCommands[item];
                }

                pendingCommandEvents.Clear();
                Debug.LogError(
                    $"PHS_RUN_INCIDENT_INVARIANT_FAILED operation=stage_cancel " +
                    $"exception={exception.GetType().Name}",
                    this);
                reason = "stage_cancel_apply_failed";
                return false;
            }

            for (var item = 0; item < cancelledCommands.Count; item++)
            {
                UpdateCommandRecord(cancelledCommands[item]);
            }

            synchronizedSnapshot.Value = new NetworkRunIncidentSnapshot(
                current.MapId,
                current.StageSequence,
                NetworkRunIncidentStageState.Cancelled,
                current.PressureCapacity,
                0,
                0,
                0,
                0,
                current.StageIssuedCount,
                AddWithoutZeroWrap(
                    current.StageResolvedCount,
                    (uint)cancelledCommands.Count),
                current.NextCommandId,
                1f,
                stateRevision);
            lastStageCancelCause = fixedCause;
            reason = null;
            Debug.Log(
                $"PHS_RUN_INCIDENT_STAGE_CANCELLED stage={stageSequence} " +
                $"cause={fixedCause} commands={cancelledCommands.Count} " +
                $"revision={Snapshot.Revision}",
                this);
            return true;
        }

        private void InitializeServerState()
        {
            defaultPressureCapacity = Mathf.Clamp(
                defaultPressureCapacity,
                1,
                ushort.MaxValue);
            defaultMaximumExternalCommands = Mathf.Clamp(
                defaultMaximumExternalCommands,
                0,
                byte.MaxValue);
            defaultMaximumInternalCommands = Mathf.Clamp(
                defaultMaximumInternalCommands,
                0,
                byte.MaxValue);
            if (defaultMaximumExternalCommands == 0
                && defaultMaximumInternalCommands == 0)
            {
                defaultMaximumExternalCommands = 1;
                defaultMaximumInternalCommands = 2;
            }

            maximumCommandHistory = Mathf.Max(
                MinimumCommandHistory,
                maximumCommandHistory);
            stageMaximumExternalCommands =
                (byte)defaultMaximumExternalCommands;
            stageMaximumInternalCommands =
                (byte)defaultMaximumInternalCommands;
            acceptedRequests.Clear();
            requestCommandIds.Clear();
            currentStageCommandRecords.Clear();
            pendingCommandEvents.Clear();
            synchronizedCommands.Clear();
            pendingCommandEvents.Clear();
            lastStageCancelCause = default;
            synchronizedSnapshot.Value = new NetworkRunIncidentSnapshot(
                0,
                0U,
                NetworkRunIncidentStageState.Inactive,
                (ushort)defaultPressureCapacity,
                0,
                0,
                0,
                0,
                0U,
                0U,
                1UL,
                1f,
                1U);
        }

        private bool TryCommitCommandState(
            int index,
            NetworkRunIncidentCommand nextCommand,
            bool addsResolvedCount,
            out string reason)
        {
            if (!TryCalculateCurrentStageWorkload(
                    index,
                    nextCommand,
                    out var reservedPressure,
                    out var activePressure,
                    out var externalCount,
                    out var internalCount,
                    out var activeWarpChargeMultiplier,
                    out reason))
            {
                return false;
            }

            try
            {
                synchronizedCommands[index] = nextCommand;
            }
            catch (Exception exception)
            {
                pendingCommandEvents.Clear();
                Debug.LogError(
                    $"PHS_RUN_INCIDENT_INVARIANT_FAILED operation=state_update " +
                    $"command={nextCommand.CommandId} " +
                    $"exception={exception.GetType().Name}",
                    this);
                reason = "command_state_apply_failed";
                return false;
            }

            UpdateCommandRecord(nextCommand);
            var current = Snapshot;
            synchronizedSnapshot.Value = new NetworkRunIncidentSnapshot(
                current.MapId,
                current.StageSequence,
                current.State,
                current.PressureCapacity,
                reservedPressure,
                activePressure,
                externalCount,
                internalCount,
                current.StageIssuedCount,
                addsResolvedCount
                    ? IncrementNonZero(current.StageResolvedCount)
                    : current.StageResolvedCount,
                current.NextCommandId,
                activeWarpChargeMultiplier,
                nextCommand.StateRevision);
            reason = null;
            return true;
        }

        private bool TryCalculateCurrentStageWorkload(
            int overrideIndex,
            NetworkRunIncidentCommand overrideCommand,
            out ushort reservedPressure,
            out ushort activePressure,
            out byte externalCount,
            out byte internalCount,
            out float activeWarpChargeMultiplier,
            out string reason)
        {
            uint reserved = 0U;
            uint active = 0U;
            var external = 0;
            var internalCountValue = 0;
            double multiplier = 1d;
            var current = Snapshot;

            for (var index = 0; index < synchronizedCommands.Count; index++)
            {
                var command = index == overrideIndex
                    ? overrideCommand
                    : synchronizedCommands[index];
                if (command.StageSequence != current.StageSequence
                    || command.IsTerminal)
                {
                    continue;
                }

                if (command.HoldsReservedPressure)
                {
                    reserved += command.PressureCost;
                }
                else if (command.HoldsActivePressure)
                {
                    active += command.PressureCost;
                    multiplier *= command.WarpChargeMultiplier;
                }

                if (command.Channel == NetworkRunIncidentChannel.External)
                {
                    external++;
                }
                else if (command.Channel
                    == NetworkRunIncidentChannel.Internal)
                {
                    internalCountValue++;
                }
            }

            if (reserved > ushort.MaxValue
                || active > ushort.MaxValue
                || reserved + active > current.PressureCapacity)
            {
                reservedPressure = 0;
                activePressure = 0;
                externalCount = 0;
                internalCount = 0;
                activeWarpChargeMultiplier = 1f;
                reason = "incident_pressure_invariant_failed";
                return false;
            }

            if (external > stageMaximumExternalCommands
                || internalCountValue > stageMaximumInternalCommands
                || external > byte.MaxValue
                || internalCountValue > byte.MaxValue)
            {
                reservedPressure = 0;
                activePressure = 0;
                externalCount = 0;
                internalCount = 0;
                activeWarpChargeMultiplier = 1f;
                reason = "incident_channel_cap_invariant_failed";
                return false;
            }

            if (double.IsNaN(multiplier)
                || double.IsInfinity(multiplier)
                || multiplier < 0d
                || multiplier > float.MaxValue)
            {
                reservedPressure = 0;
                activePressure = 0;
                externalCount = 0;
                internalCount = 0;
                activeWarpChargeMultiplier = 1f;
                reason = "warp_charge_multiplier_overflow";
                return false;
            }

            reservedPressure = (ushort)reserved;
            activePressure = (ushort)active;
            externalCount = (byte)external;
            internalCount = (byte)internalCountValue;
            activeWarpChargeMultiplier = (float)multiplier;
            reason = null;
            return true;
        }

        private bool TryValidateRequest(
            in NetworkRunIncidentRequest request,
            out string requestKey,
            out string reason)
        {
            requestKey = request.RequestId.ToString();
            if (string.IsNullOrWhiteSpace(requestKey))
            {
                reason = "request_id_required";
                return false;
            }

            if (!string.Equals(
                    requestKey,
                    requestKey.Trim(),
                    StringComparison.Ordinal))
            {
                reason = "request_id_not_normalized";
                return false;
            }

            if (request.StageSequence == 0U)
            {
                reason = "request_stage_sequence_required";
                return false;
            }

            if (request.MapId <= 0)
            {
                reason = "request_positive_map_id_required";
                return false;
            }

            if (!IsSupportedChannel(request.Channel))
            {
                reason = $"incident_channel_invalid:{(byte)request.Channel}";
                return false;
            }

            if (!IsSupportedPayloadKind(request.PayloadKind))
            {
                reason =
                    $"incident_payload_kind_invalid:{(byte)request.PayloadKind}";
                return false;
            }

            if ((request.Channel == NetworkRunIncidentChannel.External
                    && request.PayloadKind
                        != NetworkRunIncidentPayloadKind.EventManagerEvent)
                || (request.Channel == NetworkRunIncidentChannel.Internal
                    && request.PayloadKind
                        != NetworkRunIncidentPayloadKind.ShipAccident))
            {
                reason =
                    $"incident_channel_payload_mismatch:" +
                    $"{request.Channel}:{request.PayloadKind}";
                return false;
            }

            if (!IsSupportedFamily(request.IncidentFamily))
            {
                reason =
                    $"incident_family_invalid:{(byte)request.IncidentFamily}";
                return false;
            }

            if (request.ContentId <= 0)
            {
                reason = "positive_incident_content_id_required";
                return false;
            }

            if (!IsSupportedSourceKind(request.SourceKind))
            {
                reason =
                    $"incident_source_kind_invalid:{(byte)request.SourceKind}";
                return false;
            }

            if (request.PressureCost == 0)
            {
                reason = "positive_pressure_cost_required";
                return false;
            }

            var targetKey = request.TargetId.ToString();
            if (!string.IsNullOrEmpty(targetKey)
                && (!string.Equals(
                        targetKey,
                        targetKey.Trim(),
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(targetKey)))
            {
                reason = "target_id_not_normalized";
                return false;
            }

            if (float.IsNaN(request.WarpChargeMultiplier)
                || float.IsInfinity(request.WarpChargeMultiplier)
                || request.WarpChargeMultiplier < 0f
                || request.WarpChargeMultiplier > 1f)
            {
                reason = "warp_charge_multiplier_out_of_range";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryResolveHistoryWriteSlot(
            out int replacementIndex,
            out string reason)
        {
            replacementIndex = -1;
            if (synchronizedCommands.Count < maximumCommandHistory)
            {
                reason = null;
                return true;
            }

            if (synchronizedCommands.Count > maximumCommandHistory)
            {
                reason = "command_history_capacity_invariant_failed";
                return false;
            }

            for (var index = 0; index < synchronizedCommands.Count; index++)
            {
                if (synchronizedCommands[index].IsTerminal)
                {
                    replacementIndex = index;
                    reason = null;
                    return true;
                }
            }

            reason = "command_history_full";
            return false;
        }

        private bool HasNonTerminalCommand()
        {
            for (var index = 0; index < synchronizedCommands.Count; index++)
            {
                if (!synchronizedCommands[index].IsTerminal)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindCommandIndex(ulong commandId, out int index)
        {
            if (commandId != 0UL)
            {
                for (var candidate = 0;
                     candidate < synchronizedCommands.Count;
                     candidate++)
                {
                    if (synchronizedCommands[candidate].CommandId == commandId)
                    {
                        index = candidate;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        private void UpdateCommandRecord(NetworkRunIncidentCommand command)
        {
            if (command.StageSequence == Snapshot.StageSequence)
            {
                currentStageCommandRecords[command.CommandId] = command;
            }
        }

        private bool RequireServer(out string reason)
        {
            if (IsSpawned
                && IsServer
                && OwnerClientId == NetworkManager.ServerClientId)
            {
                reason = null;
                return true;
            }

            reason = "server_required";
            return false;
        }

        private static bool TryCreateActivationTarget(
            string value,
            FixedString64Bytes fallback,
            out FixedString64Bytes fixedValue,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (fallback.IsEmpty)
                {
                    fixedValue = default;
                    reason = "target_id_required";
                    return false;
                }

                fixedValue = fallback;
                reason = null;
                return true;
            }

            if (!TryCreateRequiredFixedString64(
                    value,
                    "target_id",
                    out fixedValue,
                    out reason))
            {
                return false;
            }

            if (!fallback.IsEmpty && !fallback.Equals(fixedValue))
            {
                fixedValue = default;
                reason = "target_id_conflict";
                return false;
            }

            return true;
        }

        private static bool TryCreateRequiredFixedString64(
            string value,
            string fieldName,
            out FixedString64Bytes fixedValue,
            out string reason)
        {
            fixedValue = default;
            var normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
            if (normalized == null)
            {
                reason = $"{fieldName}_required";
                return false;
            }

            if (fixedValue.CopyFrom(normalized) != CopyError.None)
            {
                fixedValue = default;
                reason = $"{fieldName}_too_long";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsSupportedChannel(
            NetworkRunIncidentChannel channel)
        {
            return channel == NetworkRunIncidentChannel.External
                || channel == NetworkRunIncidentChannel.Internal;
        }

        private static bool IsSupportedPayloadKind(
            NetworkRunIncidentPayloadKind payloadKind)
        {
            return payloadKind
                    == NetworkRunIncidentPayloadKind.EventManagerEvent
                || payloadKind
                    == NetworkRunIncidentPayloadKind.ShipAccident;
        }

        private static bool IsSupportedFamily(
            NetworkRunIncidentFamily family)
        {
            var value = (byte)family;
            return value > (byte)NetworkRunIncidentFamily.None
                && value <= (byte)NetworkRunIncidentFamily.EMP;
        }

        private static bool IsSupportedSourceKind(
            NetworkRunIncidentSourceKind sourceKind)
        {
            var value = (byte)sourceKind;
            return value >= (byte)NetworkRunIncidentSourceKind.Scheduled
                && value <= (byte)NetworkRunIncidentSourceKind.Validation;
        }

        private static uint IncrementNonZero(uint value)
        {
            value++;
            return value == 0U ? 1U : value;
        }

        private static ulong IncrementNonZero(ulong value)
        {
            value++;
            return value == 0UL ? 1UL : value;
        }

        private static uint AddWithoutZeroWrap(uint value, uint addition)
        {
            var result = value + addition;
            return result == 0U && addition != 0U ? 1U : result;
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            synchronizedSnapshot.OnValueChanged += HandleSnapshotChanged;
            synchronizedCommands.OnListChanged += HandleCommandChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            synchronizedSnapshot.OnValueChanged -= HandleSnapshotChanged;
            synchronizedCommands.OnListChanged -= HandleCommandChanged;
            isSubscribed = false;
        }

        private void HandleSnapshotChanged(
            NetworkRunIncidentSnapshot previousValue,
            NetworkRunIncidentSnapshot currentValue)
        {
            InvokeSnapshotChanged(previousValue, currentValue);
            FlushCommandEvents(currentValue.Revision);
        }

        private void HandleCommandChanged(
            NetworkListEvent<NetworkRunIncidentCommand> changeEvent)
        {
            var requiredRevision = changeEvent.Value.StateRevision;
            if (requiredRevision != 0U
                && requiredRevision > Snapshot.Revision)
            {
                pendingCommandEvents.Enqueue(changeEvent);
                return;
            }

            InvokeCommandChanged(changeEvent);
        }

        private void FlushCommandEvents(uint snapshotRevision)
        {
            while (pendingCommandEvents.Count > 0)
            {
                var changeEvent = pendingCommandEvents.Peek();
                var requiredRevision = changeEvent.Value.StateRevision;
                if (requiredRevision != 0U
                    && requiredRevision > snapshotRevision)
                {
                    return;
                }

                pendingCommandEvents.Dequeue();
                InvokeCommandChanged(changeEvent);
            }
        }

        private void InvokeSnapshotChanged(
            NetworkRunIncidentSnapshot previousValue,
            NetworkRunIncidentSnapshot currentValue)
        {
            var handlers = SnapshotChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<
                         NetworkRunIncidentSnapshot,
                         NetworkRunIncidentSnapshot> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(previousValue, currentValue);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_RUN_INCIDENT_OBSERVER_FAILED event=snapshot " +
                        $"observer={handler.Method.Name} " +
                        $"exception={exception.GetType().Name}",
                        this);
                }
            }
        }

        private void InvokeCommandChanged(
            NetworkListEvent<NetworkRunIncidentCommand> changeEvent)
        {
            var handlers = CommandChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<
                         NetworkListEvent<NetworkRunIncidentCommand>> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(changeEvent);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_RUN_INCIDENT_OBSERVER_FAILED event=command " +
                        $"observer={handler.Method.Name} " +
                        $"exception={exception.GetType().Name}",
                        this);
                }
            }
        }
    }
}
