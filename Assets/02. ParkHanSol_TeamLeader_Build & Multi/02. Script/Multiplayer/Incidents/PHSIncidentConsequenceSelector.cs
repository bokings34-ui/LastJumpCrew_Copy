using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PHSIncidentConsequenceSelector : MonoBehaviour
    {
        private const double UnitDoubleFromUInt64 =
            1d / 9007199254740992d;

        [Header("Inspector References")]
        [SerializeField] private PHSIncidentRequestGateway requestGateway;
        [SerializeField] private PHSNetworkShipAccidentCoordinator accidentCoordinator;

        private readonly List<RunIncidentWeightedEntry> eligibleEntries = new();
        private readonly List<string> compatibleAnchorIds = new();
        private bool setupValid;

        public PHSIncidentRequestGateway RequestGateway => requestGateway;
        public PHSNetworkShipAccidentCoordinator AccidentCoordinator =>
            accidentCoordinator;
        public bool IsConfigured => setupValid;

        private void Awake()
        {
            setupValid = TryValidateReferences(out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_INCIDENT_CONSEQUENCE_SETUP_FAILED reason={reason}",
                    this);
                enabled = false;
            }
        }

        public bool TryRequestForFailedExternalEventServer(
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (!setupValid)
            {
                reason = "selector_setup_invalid";
                return false;
            }

            var root = NetworkRunSessionRoot.Instance;
            var ledger = root == null ? null : root.Incidents;
            var director = root == null ? null : root.IncidentDirector;
            var randomLedger = root == null ? null : root.Rng;
            if (root == null
                || !root.IsSpawned
                || !root.IsServer
                || ledger == null
                || director == null
                || randomLedger == null
                || director.Definition == null)
            {
                reason = "run_incident_services_not_ready";
                return false;
            }

            if (TryGetExistingConsequence(
                    ledger,
                    parentCommandId,
                    out command,
                    out reason))
            {
                return true;
            }

            if (reason != null)
            {
                return false;
            }

            eligibleEntries.Clear();
            foreach (var entry in director.Definition.InternalEntries)
            {
                var accidentId = (PHSShipAccidentId)(ushort)entry.ContentId;
                if (accidentCoordinator.TryCopyAvailableCompatibleAnchorIdsServer(
                        accidentId,
                        compatibleAnchorIds,
                        out _))
                {
                    eligibleEntries.Add(entry);
                }
            }

            if (eligibleEntries.Count == 0)
            {
                reason = "eligible_internal_consequence_unavailable";
                return false;
            }

            var previousContentId = GetPreviousConsequenceContentId(
                ledger,
                parentCommandId);
            var avoidPrevious = eligibleEntries.Count > 1
                && previousContentId > 0
                && eligibleEntries.Exists(
                    entry => entry.ContentId != previousContentId);
            var totalWeight = 0d;
            foreach (var entry in eligibleEntries)
            {
                if (!avoidPrevious || entry.ContentId != previousContentId)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (double.IsNaN(totalWeight)
                || double.IsInfinity(totalWeight)
                || totalWeight <= 0d)
            {
                reason = "eligible_consequence_weight_invalid";
                return false;
            }

            if (!randomLedger.TryCreateServerScope(
                    NetworkRunRandomStream.IncidentConsequence,
                    parentCommandId,
                    out var random,
                    out var randomReason))
            {
                reason = $"consequence_random_scope_failed:{randomReason}";
                return false;
            }

            var weightedRoll =
                ((random.NextUInt64() >> 11) * UnitDoubleFromUInt64)
                * totalWeight;
            var selectedEntry = default(RunIncidentWeightedEntry);
            var selected = false;
            foreach (var entry in eligibleEntries)
            {
                if (avoidPrevious && entry.ContentId == previousContentId)
                {
                    continue;
                }

                selectedEntry = entry;
                selected = true;
                weightedRoll -= entry.Weight;
                if (weightedRoll < 0d)
                {
                    break;
                }
            }

            if (!selected)
            {
                reason = "consequence_selection_failed";
                return false;
            }

            if (!requestGateway.TrySubmitConsequenceServer(
                    in selectedEntry,
                    parentCommandId,
                    out command,
                    out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_INCIDENT_CONSEQUENCE_SELECTED parent={parentCommandId} " +
                $"command={command.CommandId} accident={selectedEntry.ContentId} " +
                $"eligible={eligibleEntries.Count} avoidedPrevious={avoidPrevious}",
                this);
            return true;
        }

        public bool TryValidateReferences(out string reason)
        {
            if (requestGateway == null)
            {
                reason = "request_gateway_missing";
                return false;
            }

            if (accidentCoordinator == null)
            {
                reason = "accident_coordinator_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryGetExistingConsequence(
            NetworkRunIncidentLedger ledger,
            ulong parentCommandId,
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            var found = false;
            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var candidate = ledger.GetCommandAt(index);
                if (candidate.ParentCommandId != parentCommandId
                    || candidate.SourceKind
                        != NetworkRunIncidentSourceKind.Consequence)
                {
                    continue;
                }

                if (found)
                {
                    reason = "duplicate_parent_consequence_commands";
                    command = default;
                    return false;
                }

                command = candidate;
                found = true;
            }

            reason = null;
            return found;
        }

        private static int GetPreviousConsequenceContentId(
            NetworkRunIncidentLedger ledger,
            ulong currentParentCommandId)
        {
            var latestCommandId = 0UL;
            var contentId = 0;
            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var command = ledger.GetCommandAt(index);
                if (command.SourceKind
                        != NetworkRunIncidentSourceKind.Consequence
                    || command.ParentCommandId == currentParentCommandId
                    || command.CommandId <= latestCommandId)
                {
                    continue;
                }

                latestCommandId = command.CommandId;
                contentId = command.ContentId;
                if (command.PayloadKind
                        == NetworkRunIncidentPayloadKind.EventManagerEvent
                    && IncidentRequestContentContract
                        .TryMapEventToLegacyAccident(
                            command.ContentId,
                            out var legacyAccidentId))
                {
                    contentId = legacyAccidentId;
                }
            }

            return contentId;
        }
    }
}
