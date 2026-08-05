using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using SM;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [Serializable]
    public sealed class PHSIncidentRequestRoute
    {
        [SerializeField] private string sourceId;
        [SerializeField] private NetworkRunIncidentChannel channel;
        [SerializeField] private NetworkRunIncidentPayloadKind payloadKind;
        [SerializeField] private NetworkRunIncidentFamily incidentFamily;
        [SerializeField] private int contentId;
        [SerializeField] private NetworkRunIncidentSourceKind sourceKind =
            NetworkRunIncidentSourceKind.Device;
        [Min(1)]
        [SerializeField] private int pressureCost = 1;
        [Range(0f, 1f)]
        [SerializeField] private float warpChargeMultiplier = 1f;
        [SerializeField] private bool requiresTarget = true;
        [Min(0f)]
        [SerializeField] private float cooldownSeconds;

        public string SourceId => sourceId;
        public NetworkRunIncidentChannel Channel => channel;
        public NetworkRunIncidentPayloadKind PayloadKind => payloadKind;
        public NetworkRunIncidentFamily IncidentFamily => incidentFamily;
        public int ContentId => contentId;
        public NetworkRunIncidentSourceKind SourceKind => sourceKind;
        public ushort PressureCost => (ushort)pressureCost;
        public float WarpChargeMultiplier => warpChargeMultiplier;
        public bool RequiresTarget => requiresTarget;
        public float CooldownSeconds => cooldownSeconds;

        public bool TryValidate(out string reason)
        {
            if (!IncidentStableId.IsValid(sourceId))
            {
                reason = "source_id_invalid";
                return false;
            }

            var maximumRequestId = default(FixedString64Bytes);
            var requestIdProbe =
                $"source:{uint.MaxValue}:{sourceId}:{uint.MaxValue}";
            if (maximumRequestId.CopyFrom(requestIdProbe) != CopyError.None)
            {
                reason = $"source_id_too_long:{sourceId}";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetworkRunIncidentChannel), channel))
            {
                reason = $"channel_invalid:{sourceId}";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(NetworkRunIncidentPayloadKind),
                    payloadKind))
            {
                reason = $"payload_kind_invalid:{sourceId}";
                return false;
            }

            if ((channel == NetworkRunIncidentChannel.External
                    && payloadKind
                        != NetworkRunIncidentPayloadKind.EventManagerEvent)
                || (channel == NetworkRunIncidentChannel.Internal
                    && payloadKind
                        != NetworkRunIncidentPayloadKind.ShipAccident))
            {
                reason = $"channel_payload_mismatch:{sourceId}";
                return false;
            }

            if (incidentFamily == NetworkRunIncidentFamily.None
                || !Enum.IsDefined(
                    typeof(NetworkRunIncidentFamily),
                    incidentFamily))
            {
                reason = $"incident_family_invalid:{sourceId}";
                return false;
            }

            if (!IncidentRequestContentContract.TryValidate(
                    channel,
                    payloadKind,
                    incidentFamily,
                    contentId,
                    out var contentContractReason))
            {
                reason = $"{contentContractReason}:{sourceId}";
                return false;
            }

            if (sourceKind == NetworkRunIncidentSourceKind.Scheduled
                || sourceKind == NetworkRunIncidentSourceKind.Validation
                || !Enum.IsDefined(
                    typeof(NetworkRunIncidentSourceKind),
                    sourceKind))
            {
                reason = $"source_kind_not_triggerable:{sourceId}";
                return false;
            }

            if (pressureCost <= 0 || pressureCost > ushort.MaxValue)
            {
                reason = $"pressure_cost_invalid:{sourceId}";
                return false;
            }

            if (float.IsNaN(warpChargeMultiplier)
                || float.IsInfinity(warpChargeMultiplier)
                || warpChargeMultiplier < 0f
                || warpChargeMultiplier > 1f)
            {
                reason = $"warp_multiplier_invalid:{sourceId}";
                return false;
            }

            if (float.IsNaN(cooldownSeconds)
                || float.IsInfinity(cooldownSeconds)
                || cooldownSeconds < 0f)
            {
                reason = $"cooldown_invalid:{sourceId}";
                return false;
            }

            reason = null;
            return true;
        }
    }

    /// <summary>
    /// Scene-owned server gateway. Team trigger components submit only source
    /// and target IDs; Inspector routes keep content and balance authority here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PHSIncidentRequestGateway :
        MonoBehaviour,
        IIncidentRequestGateway
    {
        [SerializeField] private PHSIncidentRequestRoute[] routes =
            Array.Empty<PHSIncidentRequestRoute>();

        private readonly Dictionary<string, PHSIncidentRequestRoute>
            routesBySourceId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> nextAllowedServerTimes =
            new(StringComparer.Ordinal);

        private NetworkRunSessionRoot runSessionRoot;
        private NetworkRunIncidentLedger incidentLedger;
        private bool setupValid;
        private uint observedStageSequence;

        public bool IsReady => TryRequireServer(out _);
        public int RouteCount => routesBySourceId.Count;

        private void Awake()
        {
            setupValid = TryValidateRoutes(out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_INCIDENT_REQUEST_GATEWAY_SETUP_FAILED reason={reason}",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleRootAvailable;
            NetworkRunSessionRoot.InstanceAvailable += HandleRootAvailable;
            TryBindRoot(NetworkRunSessionRoot.Instance);
        }

        private void OnDisable()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleRootAvailable;
        }

        public bool TrySubmitServer(
            IIncidentRequestSource source,
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (source == null)
            {
                reason = "request_source_missing";
                return false;
            }

            return TrySubmitServer(
                source.IncidentSourceId,
                source.IncidentTargetId,
                parentCommandId,
                out command,
                out reason);
        }

        public bool TrySubmitServer(
            string sourceId,
            string targetId,
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!TryRequireServer(out reason))
            {
                return false;
            }

            if (!IncidentStableId.IsValid(sourceId))
            {
                reason = "source_id_invalid";
                return false;
            }

            if (!routesBySourceId.TryGetValue(sourceId, out var route))
            {
                reason = $"request_route_missing:{sourceId}";
                return false;
            }

            var normalizedTargetId = string.IsNullOrEmpty(targetId)
                ? null
                : targetId;
            if (normalizedTargetId != null
                && !IncidentStableId.IsValid(normalizedTargetId))
            {
                reason = "target_id_invalid";
                return false;
            }

            if (route.RequiresTarget && normalizedTargetId == null)
            {
                reason = "target_id_required";
                return false;
            }

            if (route.SourceKind == NetworkRunIncidentSourceKind.Consequence
                && parentCommandId == 0UL)
            {
                reason = "consequence_parent_command_required";
                return false;
            }

            var fixedTargetId = default(FixedString64Bytes);
            if (normalizedTargetId != null
                && fixedTargetId.CopyFrom(normalizedTargetId) != CopyError.None)
            {
                reason = "target_id_too_long";
                return false;
            }

            var currentServerTime =
                runSessionRoot.NetworkManager.ServerTime.Time;
            if (nextAllowedServerTimes.TryGetValue(
                    sourceId,
                    out var nextAllowedServerTime)
                && currentServerTime < nextAllowedServerTime)
            {
                reason =
                    $"source_cooldown_active:" +
                    $"{Math.Max(0d, nextAllowedServerTime - currentServerTime):F3}";
                return false;
            }

            var snapshot = incidentLedger.Snapshot;
            var requestOrdinal =
                snapshot.StageIssuedCount == uint.MaxValue
                    ? 1U
                    : snapshot.StageIssuedCount + 1U;
            var fixedRequestId = default(FixedString64Bytes);
            var requestId =
                $"source:{snapshot.StageSequence}:{sourceId}:{requestOrdinal}";
            if (fixedRequestId.CopyFrom(requestId) != CopyError.None)
            {
                reason = "request_id_too_long";
                return false;
            }

            if (!IncidentRequestContentContract.TryNormalize(
                    route.Channel,
                    route.PayloadKind,
                    route.ContentId,
                    out var normalizedPayloadKind,
                    out var normalizedContentId,
                    out reason))
            {
                return false;
            }

            var request = new NetworkRunIncidentRequest(
                fixedRequestId,
                parentCommandId,
                snapshot.StageSequence,
                snapshot.MapId,
                route.Channel,
                normalizedPayloadKind,
                route.IncidentFamily,
                normalizedContentId,
                route.SourceKind,
                route.PressureCost,
                route.WarpChargeMultiplier,
                fixedTargetId);
            if (!incidentLedger.TryReserveCommandServer(
                    in request,
                    out command,
                    out reason))
            {
                return false;
            }

            if (route.CooldownSeconds > 0f)
            {
                nextAllowedServerTimes[sourceId] =
                    currentServerTime + route.CooldownSeconds;
            }

            Debug.Log(
                $"PHS_INCIDENT_REQUEST_ACCEPTED source={sourceId} " +
                $"target={fixedTargetId} command={command.CommandId} " +
                $"content={route.ContentId}",
                this);
            reason = null;
            return true;
        }

        public bool TrySubmitTerminalEventServer(
            EventId eventId,
            string targetId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!TryRequireServer(out reason))
            {
                return false;
            }

            var director = runSessionRoot.IncidentDirector;
            var definition = director == null ? null : director.Definition;
            if (definition == null)
            {
                reason = "incident_schedule_definition_missing";
                return false;
            }

            var entryFound = false;
            var selectedEntry = default(RunIncidentWeightedEntry);
            foreach (var entry in definition.ExternalEntries)
            {
                if (entry.ContentId != (int)eventId)
                {
                    continue;
                }

                selectedEntry = entry;
                entryFound = true;
                break;
            }

            if (!entryFound)
            {
                reason = $"external_event_not_configured:{eventId}";
                return false;
            }

            if (!IncidentRequestContentContract.TryValidate(
                    NetworkRunIncidentChannel.External,
                    NetworkRunIncidentPayloadKind.EventManagerEvent,
                    selectedEntry.IncidentFamily,
                    selectedEntry.ContentId,
                    out var contractReason))
            {
                reason = $"content_contract_invalid:{contractReason}";
                return false;
            }

            if (!TryCreateTargetId(targetId, out var fixedTargetId, out reason))
            {
                return false;
            }

            var snapshot = incidentLedger.Snapshot;
            var requestOrdinal = snapshot.StageIssuedCount == uint.MaxValue
                ? 1U
                : snapshot.StageIssuedCount + 1U;
            var fixedRequestId = default(FixedString64Bytes);
            var requestId =
                $"terminal:{snapshot.StageSequence}:{selectedEntry.ContentId}:{requestOrdinal}";
            if (fixedRequestId.CopyFrom(requestId) != CopyError.None)
            {
                reason = "request_id_too_long";
                return false;
            }

            var request = new NetworkRunIncidentRequest(
                fixedRequestId,
                0UL,
                snapshot.StageSequence,
                snapshot.MapId,
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                selectedEntry.IncidentFamily,
                selectedEntry.ContentId,
                NetworkRunIncidentSourceKind.Terminal,
                selectedEntry.PressureCost,
                selectedEntry.WarpChargeMultiplier,
                fixedTargetId);
            if (!incidentLedger.TryReserveCommandServer(
                    in request,
                    out command,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_INCIDENT_TERMINAL_ACCEPTED event={eventId} " +
                $"target={fixedTargetId} command={command.CommandId}",
                this);
            reason = null;
            return true;
        }

        public bool TrySubmitConsequenceServer(
            in RunIncidentWeightedEntry entry,
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!TryRequireServer(out reason))
            {
                return false;
            }

            if (parentCommandId == 0UL)
            {
                reason = "consequence_parent_command_required";
                return false;
            }

            if (!incidentLedger.TryGetCommand(
                    parentCommandId,
                    out var parentCommand))
            {
                reason = "parent_command_missing";
                return false;
            }

            if (parentCommand.Channel != NetworkRunIncidentChannel.External
                || parentCommand.State != NetworkRunIncidentCommandState.Failed)
            {
                reason =
                    $"parent_command_not_failed_external:" +
                    $"{parentCommand.Channel}:{parentCommand.State}";
                return false;
            }

            var snapshot = incidentLedger.Snapshot;
            if (parentCommand.StageSequence != snapshot.StageSequence
                || parentCommand.MapId != snapshot.MapId)
            {
                reason = "parent_command_stage_mismatch";
                return false;
            }

            var director = runSessionRoot.IncidentDirector;
            var definition = director == null ? null : director.Definition;
            if (definition == null)
            {
                reason = "incident_schedule_definition_missing";
                return false;
            }

            var configured = false;
            foreach (var candidate in definition.InternalEntries)
            {
                if (candidate.Equals(entry))
                {
                    configured = true;
                    break;
                }
            }

            if (!configured)
            {
                reason = $"internal_consequence_not_configured:{entry.ContentId}";
                return false;
            }

            if (!IncidentRequestContentContract.TryValidate(
                    NetworkRunIncidentChannel.Internal,
                    NetworkRunIncidentPayloadKind.ShipAccident,
                    entry.IncidentFamily,
                    entry.ContentId,
                    out var contractReason))
            {
                reason = $"content_contract_invalid:{contractReason}";
                return false;
            }

            var fixedRequestId = default(FixedString64Bytes);
            var requestId =
                $"consequence:{snapshot.StageSequence}:{parentCommandId}";
            if (fixedRequestId.CopyFrom(requestId) != CopyError.None)
            {
                reason = "request_id_too_long";
                return false;
            }

            if (!IncidentRequestContentContract.TryNormalize(
                    NetworkRunIncidentChannel.Internal,
                    NetworkRunIncidentPayloadKind.ShipAccident,
                    entry.ContentId,
                    out var normalizedPayloadKind,
                    out var normalizedContentId,
                    out reason))
            {
                return false;
            }

            var request = new NetworkRunIncidentRequest(
                fixedRequestId,
                parentCommandId,
                snapshot.StageSequence,
                snapshot.MapId,
                NetworkRunIncidentChannel.Internal,
                normalizedPayloadKind,
                entry.IncidentFamily,
                normalizedContentId,
                NetworkRunIncidentSourceKind.Consequence,
                entry.PressureCost,
                entry.WarpChargeMultiplier,
                default);
            if (!incidentLedger.TryReserveCommandServer(
                    in request,
                    out command,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_INCIDENT_CONSEQUENCE_ACCEPTED parent={parentCommandId} " +
                $"command={command.CommandId} content={entry.ContentId}",
                this);
            reason = null;
            return true;
        }

        private void HandleRootAvailable(NetworkRunSessionRoot root)
        {
            TryBindRoot(root);
        }

        private void TryBindRoot(NetworkRunSessionRoot root)
        {
            if (root == null || root.Incidents == null)
            {
                return;
            }

            if (runSessionRoot != root)
            {
                nextAllowedServerTimes.Clear();
                observedStageSequence = 0U;
            }

            runSessionRoot = root;
            incidentLedger = root.Incidents;
        }

        private bool TryRequireServer(out string reason)
        {
            if (!setupValid)
            {
                reason = "setup_invalid";
                return false;
            }

            if (runSessionRoot == null
                || runSessionRoot != NetworkRunSessionRoot.Instance)
            {
                TryBindRoot(NetworkRunSessionRoot.Instance);
            }

            if (runSessionRoot == null
                || incidentLedger == null
                || runSessionRoot.StageClock == null
                || !runSessionRoot.IsSpawned
                || !runSessionRoot.IsServer
                || !incidentLedger.IsSpawned
                || !incidentLedger.IsServer
                || !runSessionRoot.StageClock.IsSpawned
                || !runSessionRoot.StageClock.IsServer
                || runSessionRoot.NetworkManager == null
                || runSessionRoot.OwnerClientId
                    != Unity.Netcode.NetworkManager.ServerClientId)
            {
                reason = "server_authority_required";
                return false;
            }

            var snapshot = incidentLedger.Snapshot;
            if (snapshot.State != NetworkRunIncidentStageState.Active
                || snapshot.MapId <= 0
                || snapshot.StageSequence == 0U
                || snapshot.MapId != runSessionRoot.StageClock.MapId
                || snapshot.StageSequence
                    != runSessionRoot.StageClock.StageSequence)
            {
                reason = "incident_stage_not_active";
                return false;
            }

            if (runSessionRoot.StageClock.State
                != NetworkRunStageClockState.Running)
            {
                reason =
                    $"stage_clock_not_running:" +
                    $"{runSessionRoot.StageClock.State}";
                return false;
            }

            if (observedStageSequence != snapshot.StageSequence)
            {
                nextAllowedServerTimes.Clear();
                observedStageSequence = snapshot.StageSequence;
            }

            reason = null;
            return true;
        }

        private static bool TryCreateTargetId(
            string targetId,
            out FixedString64Bytes fixedTargetId,
            out string reason)
        {
            fixedTargetId = default;
            if (string.IsNullOrEmpty(targetId))
            {
                reason = null;
                return true;
            }

            if (!IncidentStableId.IsValid(targetId))
            {
                reason = "target_id_invalid";
                return false;
            }

            if (fixedTargetId.CopyFrom(targetId) != CopyError.None)
            {
                reason = "target_id_too_long";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryValidateRoutes(out string reason)
        {
            routesBySourceId.Clear();
            if (routes == null || routes.Length == 0)
            {
                reason = "routes_empty";
                return false;
            }

            foreach (var route in routes)
            {
                if (route == null)
                {
                    reason = "route_missing";
                    return false;
                }

                if (!route.TryValidate(out reason))
                {
                    return false;
                }

                if (!routesBySourceId.TryAdd(route.SourceId, route))
                {
                    reason = $"source_id_duplicate:{route.SourceId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
