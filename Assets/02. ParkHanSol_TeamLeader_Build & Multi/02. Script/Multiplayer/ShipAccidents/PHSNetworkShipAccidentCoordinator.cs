using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkShipAccidentCoordinator :
        NetworkBehaviour,
        IShipAccidentScheduleConfigurator
    {
        [Header("Inspector References")]
        [SerializeField] private PHSShipAccidentCatalogSO accidentCatalog;
        [SerializeField] private NetworkShipSystemsState shipSystemsState;
        [SerializeField] private PHSShipAccidentAnchor[] anchors = Array.Empty<PHSShipAccidentAnchor>();

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float maximumRepairDistance = 3f;

        private readonly NetworkList<NetworkShipAccidentSnapshot> activeAccidents = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<string, PHSShipAccidentAnchor> anchorsById = new(StringComparer.Ordinal);
        private readonly Dictionary<uint, double> nextDamageTimes = new();
        private readonly Dictionary<(ulong ClientId, uint ItemRevision), uint> repairRequestSequences = new();

        private PHSMapShipAccidentWeight[] configuredEntries = Array.Empty<PHSMapShipAccidentWeight>();
        private float intervalMinSeconds;
        private float intervalMaxSeconds;
        private int maximumActiveAccidents;
        private float moduleDamageMultiplier = 1f;
        private float shipDamageMultiplier = 1f;
        private double nextSpawnTime;
        private uint nextInstanceId;
        private bool setupValid;
        private bool scheduleValid;
        private bool isRunning;

        public int ActiveAccidentCount => activeAccidents.Count;
        public static PHSNetworkShipAccidentCoordinator Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public NetworkShipAccidentSnapshot GetActiveAccidentAt(int index)
        {
            if (index < 0 || index >= activeAccidents.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return activeAccidents[index];
        }

        private void Awake()
        {
            setupValid = ValidateSetup();
            enabled = setupValid;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_SHIP_ACCIDENT_SETUP_FAILED reason=server_owned_object_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_SETUP_FAILED reason=duplicate_coordinator current={name} existing={Instance.name}", this);
                enabled = false;
                return;
            }

            Instance = this;
            activeAccidents.OnListChanged += HandleAccidentListChanged;
            RefreshPresentations();
        }

        public override void OnNetworkDespawn()
        {
            activeAccidents.OnListChanged -= HandleAccidentListChanged;
            isRunning = false;
            nextDamageTimes.Clear();
            repairRequestSequences.Clear();
            foreach (var anchor in anchors)
            {
                if (anchor != null)
                {
                    anchor.ClearSnapshot();
                }
            }

            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !setupValid)
            {
                return;
            }

            ApplyPeriodicDamage();
            if (!isRunning || NetworkManager.ServerTime.Time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = NetworkManager.ServerTime.Time + RollNextInterval();
            if (activeAccidents.Count >= maximumActiveAccidents)
            {
                return;
            }

            if (!TrySelectSpawnCandidate(out var definition, out var anchor, out var reason))
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_SPAWN_SKIPPED reason={reason}", this);
                return;
            }

            TrySpawnAccidentServer(definition.Id, anchor.AnchorId, out _, out _);
        }

        public bool TryConfigureServer(
            PHSMapShipAccidentWeight[] entries,
            float newIntervalMinSeconds,
            float newIntervalMaxSeconds,
            int newMaximumActiveAccidents,
            float newModuleDamageMultiplier,
            float newShipDamageMultiplier,
            out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (isRunning)
            {
                reason = "scheduler_running";
                return false;
            }

            if (!ValidateSchedule(
                    entries,
                    newIntervalMinSeconds,
                    newIntervalMaxSeconds,
                    newMaximumActiveAccidents,
                    newModuleDamageMultiplier,
                    newShipDamageMultiplier,
                    out reason))
            {
                return false;
            }

            configuredEntries = (PHSMapShipAccidentWeight[])entries.Clone();
            intervalMinSeconds = newIntervalMinSeconds;
            intervalMaxSeconds = newIntervalMaxSeconds;
            maximumActiveAccidents = newMaximumActiveAccidents;
            moduleDamageMultiplier = newModuleDamageMultiplier;
            shipDamageMultiplier = newShipDamageMultiplier;
            scheduleValid = true;
            Debug.Log(
                $"PHS_SHIP_ACCIDENT_SCHEDULE_CONFIGURED entries={entries.Length} interval={intervalMinSeconds:0.###}-{intervalMaxSeconds:0.###} maxActive={maximumActiveAccidents} moduleMultiplier={moduleDamageMultiplier:0.###} shipMultiplier={shipDamageMultiplier:0.###}",
                this);
            reason = null;
            return true;
        }

        public bool TryStartServer(out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (!scheduleValid)
            {
                reason = "schedule_invalid";
                return false;
            }

            if (!isRunning)
            {
                isRunning = true;
                nextSpawnTime = NetworkManager.ServerTime.Time + RollNextInterval();
            }

            reason = null;
            return true;
        }

        public bool TryStopServer(out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            isRunning = false;
            nextSpawnTime = 0d;
            reason = null;
            return true;
        }

        public bool TrySpawnAccidentServer(
            PHSShipAccidentId accidentId,
            string requestedAnchorId,
            out uint instanceId,
            out string reason)
        {
            instanceId = 0U;
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (!accidentCatalog.TryResolve(accidentId, out var definition))
            {
                reason = $"definition_missing:{accidentId}";
                return false;
            }

            PHSShipAccidentAnchor anchor;
            if (string.IsNullOrWhiteSpace(requestedAnchorId))
            {
                if (!TryFindAvailableAnchor(definition, out anchor))
                {
                    reason = $"compatible_anchor_unavailable:{accidentId}";
                    return false;
                }
            }
            else if (!anchorsById.TryGetValue(requestedAnchorId, out anchor)
                || !anchor.Supports(definition)
                || IsAnchorOccupied(requestedAnchorId))
            {
                reason = $"requested_anchor_unavailable:{requestedAnchorId}";
                return false;
            }

            if (!TryApplyInitialImpact(definition, out reason))
            {
                return false;
            }

            nextInstanceId++;
            if (nextInstanceId == 0U)
            {
                nextInstanceId++;
            }

            instanceId = nextInstanceId;
            var snapshot = new NetworkShipAccidentSnapshot(
                instanceId,
                definition.Id,
                new FixedString64Bytes(anchor.AnchorId),
                0,
                definition.RequiredRepairProgress,
                1U);
            activeAccidents.Add(snapshot);
            nextDamageTimes[instanceId] = NetworkManager.ServerTime.Time + definition.DamageIntervalSeconds;
            Debug.Log(
                $"PHS_SHIP_ACCIDENT_SPAWNED instance={instanceId} accident={definition.Id} anchor={anchor.AnchorId} module={definition.TargetModule}",
                this);
            reason = null;
            return true;
        }

        public bool RequestRepair(
            IShipAccidentRepairTarget target,
            NetworkPlayerItemRecord itemRecord,
            uint requestSequence)
        {
            if (target == null || itemRecord == null || target.IsRepairComplete)
            {
                Debug.LogWarning("PHS_SHIP_ACCIDENT_REPAIR_REQUEST_REJECTED reason=local_contract", this);
                return false;
            }

            var itemId = itemRecord.HeldItemId;
            var itemRevision = itemRecord.Revision;
            if (itemId != target.RequiredItemId || requestSequence == 0U)
            {
                Debug.LogWarning(
                    $"PHS_SHIP_ACCIDENT_REPAIR_REQUEST_REJECTED reason=item_contract item={itemId} required={target.RequiredItemId} sequence={requestSequence}",
                    this);
                return false;
            }

            if (IsServer)
            {
                return CompleteRepairRequest(
                    itemRecord.OwnerClientId,
                    target.AccidentInstanceId,
                    itemId,
                    itemRevision,
                    requestSequence);
            }

            if (!IsClient || !itemRecord.IsOwner)
            {
                Debug.LogError("PHS_SHIP_ACCIDENT_REPAIR_REQUEST_REJECTED reason=owner_client_required", this);
                return false;
            }

            RequestRepairServerRpc(
                target.AccidentInstanceId,
                new FixedString64Bytes(itemId),
                itemRevision,
                requestSequence);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRepairServerRpc(
            uint accidentInstanceId,
            FixedString64Bytes expectedItemId,
            uint expectedItemRevision,
            uint requestSequence,
            ServerRpcParams rpcParams = default)
        {
            CompleteRepairRequest(
                rpcParams.Receive.SenderClientId,
                accidentInstanceId,
                expectedItemId.ToString(),
                expectedItemRevision,
                requestSequence);
        }

        private bool CompleteRepairRequest(
            ulong senderClientId,
            uint accidentInstanceId,
            string expectedItemId,
            uint expectedItemRevision,
            uint requestSequence)
        {
            var index = FindSnapshotIndex(accidentInstanceId);
            if (index < 0)
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=accident_missing instance={accidentInstanceId}", this);
                return false;
            }

            var snapshot = activeAccidents[index];
            if (!accidentCatalog.TryResolve(snapshot.AccidentId, out var definition)
                || !anchorsById.TryGetValue(snapshot.AnchorId.ToString(), out var anchor))
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=runtime_contract_missing instance={accidentInstanceId}", this);
                return false;
            }

            if (!NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=player_missing client={senderClientId}", this);
                return false;
            }

            var itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null
                || itemRecord.OwnerClientId != senderClientId
                || itemRecord.HeldItemId != expectedItemId
                || itemRecord.Revision != expectedItemRevision
                || expectedItemId != definition.RequiredItemId)
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=item_record_mismatch client={senderClientId} instance={accidentInstanceId}", this);
                return false;
            }

            var sequenceKey = (senderClientId, expectedItemRevision);
            if (repairRequestSequences.TryGetValue(sequenceKey, out var previousSequence)
                && requestSequence <= previousSequence)
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=sequence_replayed client={senderClientId} sequence={requestSequence}", this);
                return false;
            }

            var distance = Vector3.Distance(client.PlayerObject.transform.position, anchor.RepairPosition);
            if (distance > maximumRepairDistance)
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=distance client={senderClientId} distance={distance:F3} max={maximumRepairDistance:F3}", this);
                return false;
            }

            var nextProgress = Mathf.Min(
                snapshot.RequiredRepairProgress,
                snapshot.RepairProgress + definition.RepairProgressPerUse);
            var completesRepair = nextProgress >= snapshot.RequiredRepairProgress;
            if (completesRepair)
            {
                if (!TryApplyModuleRepairOnResolve(definition, out var repairReason))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_RESOLVE_FAILED reason={repairReason} instance={accidentInstanceId}", this);
                    return false;
                }

                repairRequestSequences[sequenceKey] = requestSequence;
                activeAccidents.RemoveAt(index);
                nextDamageTimes.Remove(accidentInstanceId);
                Debug.Log(
                    $"PHS_SHIP_ACCIDENT_RESOLVED instance={accidentInstanceId} accident={definition.Id} client={senderClientId} shipHp={shipSystemsState.CurrentShipHp}",
                    this);
                return true;
            }

            repairRequestSequences[sequenceKey] = requestSequence;
            activeAccidents[index] = new NetworkShipAccidentSnapshot(
                snapshot.InstanceId,
                snapshot.AccidentId,
                snapshot.AnchorId,
                nextProgress,
                snapshot.RequiredRepairProgress,
                snapshot.Revision + 1U);
            return true;
        }

        private void ApplyPeriodicDamage()
        {
            var currentTime = NetworkManager.ServerTime.Time;
            for (var index = activeAccidents.Count - 1; index >= 0; index--)
            {
                var snapshot = activeAccidents[index];
                if (!nextDamageTimes.TryGetValue(snapshot.InstanceId, out var dueTime)
                    || currentTime < dueTime)
                {
                    continue;
                }

                if (!accidentCatalog.TryResolve(snapshot.AccidentId, out var definition))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_TICK_FAILED reason=definition_missing instance={snapshot.InstanceId}", this);
                    isRunning = false;
                    return;
                }

                nextDamageTimes[snapshot.InstanceId] = currentTime + definition.DamageIntervalSeconds;
                if (!TryApplyPeriodicImpact(definition, out var reason))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_TICK_FAILED reason={reason} instance={snapshot.InstanceId}", this);
                    isRunning = false;
                    return;
                }
            }
        }

        private bool TryApplyInitialImpact(PHSShipAccidentDefinitionSO definition, out string reason)
        {
            return TryApplyImpact(
                definition,
                definition.InitialModuleDamage,
                definition.InitialShipDamage,
                "initial",
                out reason);
        }

        private bool TryApplyPeriodicImpact(PHSShipAccidentDefinitionSO definition, out string reason)
        {
            return TryApplyImpact(
                definition,
                definition.PeriodicModuleDamage,
                definition.PeriodicShipDamage,
                "periodic",
                out reason);
        }

        private bool TryApplyImpact(
            PHSShipAccidentDefinitionSO definition,
            int baseModuleDamage,
            int baseShipDamage,
            string phase,
            out string reason)
        {
            var cause = $"ship_accident:{definition.Id}:{phase}";
            var moduleDamage = ScaleDamage(baseModuleDamage, moduleDamageMultiplier);
            var shipDamage = ScaleDamage(baseShipDamage, shipDamageMultiplier);

            if (moduleDamage > 0
                && !shipSystemsState.TryApplyModuleDamage(
                    definition.TargetModule,
                    moduleDamage,
                    definition.CausesModuleFault,
                    cause,
                    out var moduleReason))
            {
                reason = $"module_damage_failed:{moduleReason}";
                return false;
            }

            if (shipDamage > 0
                && !shipSystemsState.TryApplyShipDamage(shipDamage, cause, out var shipReason))
            {
                reason = $"ship_damage_failed:{shipReason}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryApplyModuleRepairOnResolve(
            PHSShipAccidentDefinitionSO definition,
            out string reason)
        {
            if (definition.ModuleRepairOnResolve <= 0)
            {
                reason = null;
                return true;
            }

            if (!shipSystemsState.TryGetModuleSnapshot(definition.TargetModule, out var module))
            {
                reason = $"module_snapshot_missing:{definition.TargetModule}";
                return false;
            }

            if (module.CurrentHp >= module.MaximumHp)
            {
                return TryRestoreGravityIfNeeded(definition, out reason);
            }

            if (!shipSystemsState.TryRepairModule(
                definition.TargetModule,
                definition.ModuleRepairOnResolve,
                out reason))
            {
                return false;
            }

            return TryRestoreGravityIfNeeded(definition, out reason);
        }

        private bool TryRestoreGravityIfNeeded(
            PHSShipAccidentDefinitionSO definition,
            out string reason)
        {
            if (definition.TargetModule != NetworkShipModuleId.Gravity)
            {
                reason = null;
                return true;
            }

            return shipSystemsState.TryRestoreGravityAfterRepair(out reason);
        }

        private bool TrySelectSpawnCandidate(
            out PHSShipAccidentDefinitionSO selectedDefinition,
            out PHSShipAccidentAnchor selectedAnchor,
            out string reason)
        {
            var candidates = new List<PHSMapShipAccidentWeight>();
            var candidateAnchors = new List<PHSShipAccidentAnchor>();
            var totalWeight = 0f;
            foreach (var entry in configuredEntries)
            {
                if (!TryFindAvailableAnchor(entry.Definition, out var anchor))
                {
                    continue;
                }

                candidates.Add(entry);
                candidateAnchors.Add(anchor);
                totalWeight += entry.Weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0f)
            {
                selectedDefinition = null;
                selectedAnchor = null;
                reason = "compatible_accident_unavailable";
                return false;
            }

            var roll = UnityEngine.Random.value * totalWeight;
            for (var index = 0; index < candidates.Count; index++)
            {
                roll -= candidates[index].Weight;
                if (roll <= 0f)
                {
                    selectedDefinition = candidates[index].Definition;
                    selectedAnchor = candidateAnchors[index];
                    reason = null;
                    return true;
                }
            }

            selectedDefinition = null;
            selectedAnchor = null;
            reason = "weighted_roll_out_of_range";
            return false;
        }

        private bool TryFindAvailableAnchor(
            PHSShipAccidentDefinitionSO definition,
            out PHSShipAccidentAnchor selectedAnchor)
        {
            var candidates = new List<PHSShipAccidentAnchor>();
            foreach (var anchor in anchors)
            {
                if (anchor != null
                    && anchor.Supports(definition)
                    && !IsAnchorOccupied(anchor.AnchorId))
                {
                    candidates.Add(anchor);
                }
            }

            if (candidates.Count == 0)
            {
                selectedAnchor = null;
                return false;
            }

            selectedAnchor = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }

        private bool IsAnchorOccupied(string anchorId)
        {
            foreach (var snapshot in activeAccidents)
            {
                if (snapshot.AnchorId.ToString() == anchorId)
                {
                    return true;
                }
            }

            return false;
        }

        private int FindSnapshotIndex(uint instanceId)
        {
            for (var index = 0; index < activeAccidents.Count; index++)
            {
                if (activeAccidents[index].InstanceId == instanceId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void HandleAccidentListChanged(
            NetworkListEvent<NetworkShipAccidentSnapshot> changeEvent)
        {
            RefreshPresentations();
        }

        private void RefreshPresentations()
        {
            foreach (var anchor in anchors)
            {
                if (anchor != null)
                {
                    anchor.ClearSnapshot();
                }
            }

            foreach (var snapshot in activeAccidents)
            {
                if (!anchorsById.TryGetValue(snapshot.AnchorId.ToString(), out var anchor)
                    || !accidentCatalog.TryResolve(snapshot.AccidentId, out var definition))
                {
                    Debug.LogError(
                        $"PHS_SHIP_ACCIDENT_PRESENTATION_FAILED reason=runtime_mapping_missing instance={snapshot.InstanceId}",
                        this);
                    continue;
                }

                anchor.ApplySnapshot(snapshot, definition);
            }
        }

        private bool ValidateSetup()
        {
            RegisterSceneAnchors();

            if (accidentCatalog == null)
            {
                Debug.LogError("PHS_SHIP_ACCIDENT_SETUP_FAILED reason=catalog_missing", this);
                return false;
            }

            if (!accidentCatalog.TryValidate(out var catalogReason))
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_SETUP_FAILED reason=catalog_invalid detail={catalogReason}", this);
                return false;
            }

            if (shipSystemsState == null)
            {
                Debug.LogError("PHS_SHIP_ACCIDENT_SETUP_FAILED reason=ship_systems_missing", this);
                return false;
            }

            if (anchors == null || anchors.Length == 0)
            {
                Debug.LogError("PHS_SHIP_ACCIDENT_SETUP_FAILED reason=anchors_missing", this);
                return false;
            }

            anchorsById.Clear();
            foreach (var anchor in anchors)
            {
                if (anchor == null)
                {
                    Debug.LogError("PHS_SHIP_ACCIDENT_SETUP_FAILED reason=anchor_missing", this);
                    return false;
                }

                if (!anchor.TryValidate(out var anchorReason))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_SETUP_FAILED reason=anchor_invalid detail={anchorReason}", this);
                    return false;
                }

                if (!anchorsById.TryAdd(anchor.AnchorId, anchor))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_SETUP_FAILED reason=anchor_duplicate id={anchor.AnchorId}", this);
                    return false;
                }

                anchor.Bind(this);
            }

            if (maximumRepairDistance <= 0f)
            {
                Debug.LogError("PHS_SHIP_ACCIDENT_SETUP_FAILED reason=repair_distance_invalid", this);
                return false;
            }

            return true;
        }

        private void RegisterSceneAnchors()
        {
            var sceneAnchors = FindObjectsByType<PHSShipAccidentAnchor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (sceneAnchors.Length == 0)
            {
                return;
            }

            var registered = new List<PHSShipAccidentAnchor>(anchors);
            foreach (var sceneAnchor in sceneAnchors)
            {
                if (sceneAnchor != null && !registered.Contains(sceneAnchor))
                {
                    registered.Add(sceneAnchor);
                }
            }

            anchors = registered.ToArray();
        }

        private bool CanExecuteServerCommand(out string reason)
        {
            if (!setupValid || !IsSpawned)
            {
                reason = "coordinator_not_ready";
                return false;
            }

            if (!IsServer)
            {
                reason = "server_authority_required";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateSchedule(
            PHSMapShipAccidentWeight[] entries,
            float candidateIntervalMinSeconds,
            float candidateIntervalMaxSeconds,
            int candidateMaximumActiveAccidents,
            float candidateModuleDamageMultiplier,
            float candidateShipDamageMultiplier,
            out string reason)
        {
            if (entries == null || entries.Length == 0)
            {
                reason = "accident_entries_empty";
                return false;
            }

            var ids = new HashSet<PHSShipAccidentId>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    reason = "accident_entry_missing";
                    return false;
                }

                if (!entry.TryValidate(out var entryReason))
                {
                    reason = $"accident_entry_invalid:{entryReason}";
                    return false;
                }

                if (!ids.Add(entry.Definition.Id))
                {
                    reason = $"accident_entry_duplicate:{entry.Definition.Id}";
                    return false;
                }

                if (!accidentCatalog.TryResolve(entry.Definition.Id, out var catalogDefinition)
                    || catalogDefinition != entry.Definition)
                {
                    reason = $"accident_catalog_mismatch:{entry.Definition.Id}";
                    return false;
                }

                var hasCompatibleAnchor = false;
                foreach (var anchor in anchors)
                {
                    if (anchor != null && anchor.Supports(entry.Definition))
                    {
                        hasCompatibleAnchor = true;
                        break;
                    }
                }

                if (!hasCompatibleAnchor)
                {
                    reason = $"accident_anchor_missing:{entry.Definition.Id}";
                    return false;
                }
            }

            if (candidateIntervalMinSeconds <= 0f
                || candidateIntervalMaxSeconds < candidateIntervalMinSeconds)
            {
                reason = "accident_interval_invalid";
                return false;
            }

            if (candidateMaximumActiveAccidents <= 0)
            {
                reason = "maximum_active_accidents_invalid";
                return false;
            }

            if (candidateModuleDamageMultiplier <= 0f
                || float.IsNaN(candidateModuleDamageMultiplier)
                || float.IsInfinity(candidateModuleDamageMultiplier))
            {
                reason = "module_damage_multiplier_invalid";
                return false;
            }

            if (candidateShipDamageMultiplier <= 0f
                || float.IsNaN(candidateShipDamageMultiplier)
                || float.IsInfinity(candidateShipDamageMultiplier))
            {
                reason = "ship_damage_multiplier_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private float RollNextInterval()
        {
            return UnityEngine.Random.Range(intervalMinSeconds, intervalMaxSeconds);
        }

        private static int ScaleDamage(int baseDamage, float multiplier)
        {
            return baseDamage <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }
    }
}
