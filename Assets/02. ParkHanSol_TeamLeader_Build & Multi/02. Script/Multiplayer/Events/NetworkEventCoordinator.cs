using System;
using System.Collections;
using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using SM;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkEventCoordinator :
        NetworkBehaviour,
        IEventRuntimeBridge,
        IEventEffectRuntimeBridge,
        IEventRepairRuntimeBridge,
        IShipPowerEventRuntimeBridge
    {
        [Header("Event Domain References")]
        [SerializeField] private EventManager eventManager;
        [SerializeField] private PHSNetworkEventScheduler eventScheduler;
        [SerializeField] private RoomRegistry roomRegistry;

        [Header("Client Effect Presentation")]
        [SerializeField] private NetworkEventEffectMirrorPresenter effectMirrorPresenter;

        [Header("Server Runtime")]
        [SerializeField] private bool startSchedulerOnServerSpawn;
        [SerializeField, Min(0.05f)] private float terminalSnapshotRetentionSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float effectRemovalSnapshotRetentionSeconds = 0.25f;
        [SerializeField, Min(0.1f)] private float serverRepairDistance = 3f;
        [SerializeField, Min(0.01f)] private float serverRepairStep = 1f;

        [Header("Server Ship Impact")]
        [SerializeField, Min(1)] private int fireHullDamagePerEffect = 2;
        [SerializeField, Min(1)] private int oxygenLifeSupportDamagePerEffect = 5;
        [SerializeField, Min(1)] private int enemyEngineDamagePerEffect = 3;

        private readonly NetworkList<NetworkEventLifecycleSnapshot> lifecycleSnapshots = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkEventEffectSnapshot> effectSnapshots = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<ulong, NetworkEventLifecycleSnapshot> snapshotCache = new();
        private readonly Dictionary<uint, NetworkEventEffectSnapshot> effectSnapshotCache = new();
        private readonly Dictionary<uint, IEventRepairableEffect> repairTargets = new();
        private readonly Dictionary<(ulong ClientId, uint ItemRevision), uint> repairRequestSequences = new();
        private readonly List<uint> effectRemovalBuffer = new();

        private bool setupValid;
        private bool suppressTerminalShipImpact;
        private ulong nextEventInstanceId;
        private uint nextEffectInstanceId;

        public static NetworkEventCoordinator Instance { get; private set; }

        public bool IsAuthoritative => IsSpawned && IsServer;
        public int SnapshotCount => lifecycleSnapshots.Count;
        public int EffectSnapshotCount => effectSnapshots.Count;

        public NetworkEventLifecycleSnapshot GetLifecycleSnapshotAt(int index)
        {
            if (index < 0 || index >= lifecycleSnapshots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return lifecycleSnapshots[index];
        }

        public event Action LifecycleSnapshotsChanged;
        public event Action EffectSnapshotsChanged;
        public event Action<ulong, EventId, bool> ServerEventFinished;

        private void Awake()
        {
            setupValid = ValidateSetup();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (Instance != null && Instance != this)
            {
                Debug.LogError("PHS_EVENT_COORDINATOR_DUPLICATE", this);
                enabled = false;
                return;
            }

            Instance = this;
            lifecycleSnapshots.OnListChanged += HandleLifecycleSnapshotsChanged;
            effectSnapshots.OnListChanged += HandleEffectSnapshotsChanged;
            RebuildSnapshotCache("network_spawn");
            RebuildEffectSnapshotCache("network_spawn");

            if (!setupValid)
            {
                Debug.LogError("PHS_EVENT_COORDINATOR_DISABLED reason=invalid_setup", this);
                return;
            }

            if (!eventManager.ConfigureRuntimeBridge(this))
            {
                setupValid = false;
                Debug.LogError("PHS_EVENT_COORDINATOR_DISABLED reason=bridge_rejected", this);
                return;
            }

            if (IsServer)
            {
                InitializeServerSequence();
                if (startSchedulerOnServerSpawn)
                {
                    TryStartSchedulerServer();
                }
            }

            Debug.Log(
                $"PHS_EVENT_COORDINATOR_READY server={IsServer} snapshots={lifecycleSnapshots.Count} effects={effectSnapshots.Count} effect_sync=true",
                this);
        }

        public override void OnNetworkDespawn()
        {
            lifecycleSnapshots.OnListChanged -= HandleLifecycleSnapshotsChanged;
            effectSnapshots.OnListChanged -= HandleEffectSnapshotsChanged;

            if (eventManager != null)
            {
                eventManager.ClearRuntimeBridge(this);
            }

            if (Instance == this)
            {
                Instance = null;
            }

            snapshotCache.Clear();
            effectSnapshotCache.Clear();
            repairTargets.Clear();
            repairRequestSequences.Clear();
            effectMirrorPresenter?.ClearMirrors();
            base.OnNetworkDespawn();
        }

        public bool RegisterRepairTarget(IEventRepairableEffect target)
        {
            if (!IsAuthoritative || target == null || target.EventInstanceId == 0UL || target.EffectInstanceId == 0U)
            {
                Debug.LogError("PHS_EVENT_REPAIR_REGISTER_FAILED reason=invalid_target_or_authority", this);
                return false;
            }

            if (repairTargets.ContainsKey(target.EffectInstanceId))
            {
                Debug.LogError(
                    $"PHS_EVENT_REPAIR_REGISTER_FAILED reason=duplicate effect={target.EffectInstanceId}",
                    this);
                return false;
            }

            repairTargets.Add(target.EffectInstanceId, target);
            Debug.Log(
                $"PHS_EVENT_REPAIR_TARGET_REGISTERED event={target.EventInstanceId} effect={target.EffectInstanceId} kind={target.EffectKind} item={target.RequiredItemId}",
                this);
            return true;
        }

        public void UnregisterRepairTarget(ulong eventInstanceId, uint effectInstanceId)
        {
            if (!IsAuthoritative || effectInstanceId == 0U)
            {
                return;
            }

            if (repairTargets.TryGetValue(effectInstanceId, out var target)
                && target.EventInstanceId == eventInstanceId)
            {
                repairTargets.Remove(effectInstanceId);
                Debug.Log(
                    $"PHS_EVENT_REPAIR_TARGET_UNREGISTERED event={eventInstanceId} effect={effectInstanceId}",
                    this);
            }
        }

        public bool RequestEffectRepair(
            IEventRepairTargetHandle target,
            NetworkPlayerItemRecord itemRecord,
            uint requestSequence)
        {
            if (!setupValid || !IsSpawned || target == null || itemRecord == null)
            {
                Debug.LogWarning("PHS_EVENT_REPAIR_REQUEST_REJECTED reason=request_not_ready", this);
                return false;
            }

            var itemId = itemRecord.HeldItemId;
            var itemRevision = itemRecord.Revision;
            if (itemId != target.RequiredItemId || requestSequence == 0U)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_REPAIR_REQUEST_REJECTED reason=local_contract item={itemId} required={target.RequiredItemId} sequence={requestSequence}",
                    this);
                return false;
            }

            if (IsServer)
            {
                var localClientId = NetworkManager != null
                    ? NetworkManager.LocalClientId
                    : Unity.Netcode.NetworkManager.ServerClientId;
                return TryApplyEffectRepairServer(
                    target.EventInstanceId,
                    target.EffectInstanceId,
                    itemId,
                    itemRevision,
                    requestSequence,
                    localClientId);
            }

            RequestEffectRepairServerRpc(
                target.EventInstanceId,
                target.EffectInstanceId,
                new FixedString64Bytes(itemId),
                itemRevision,
                requestSequence);
            Debug.Log(
                $"PHS_EVENT_REPAIR_REQUEST_SENT event={target.EventInstanceId} effect={target.EffectInstanceId} item={itemId} revision={itemRevision} sequence={requestSequence}",
                this);
            return true;
        }

        public bool TryGetRepairTargetServer(
            ulong eventInstanceId,
            out IEventRepairTargetHandle target)
        {
            target = null;
            if (!IsAuthoritative)
            {
                return false;
            }

            foreach (var repairTarget in repairTargets.Values)
            {
                if (repairTarget != null
                    && repairTarget.EventInstanceId == eventInstanceId
                    && !repairTarget.IsRepairComplete)
                {
                    target = repairTarget;
                    return true;
                }
            }

            return false;
        }

        public bool IsEventActive(EventId eventId)
        {
            foreach (var pair in snapshotCache)
            {
                var snapshot = pair.Value;
                if (snapshot.EventId == eventId && !snapshot.IsTerminal)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSnapshot(ulong instanceId, out NetworkEventLifecycleSnapshot snapshot)
        {
            return snapshotCache.TryGetValue(instanceId, out snapshot);
        }

        public bool TryGetSnapshotAt(int index, out NetworkEventLifecycleSnapshot snapshot)
        {
            if (index < 0 || index >= lifecycleSnapshots.Count)
            {
                snapshot = default;
                return false;
            }

            snapshot = lifecycleSnapshots[index];
            return true;
        }

        public void CopySnapshotsTo(List<NetworkEventLifecycleSnapshot> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (var snapshot in lifecycleSnapshots)
            {
                destination.Add(snapshot);
            }
        }

        public void CopyEffectSnapshotsTo(List<NetworkEventEffectSnapshot> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (var snapshot in effectSnapshots)
            {
                destination.Add(snapshot);
            }
        }

        public bool RequestEventFromTerminal(EventId eventId)
        {
            if (!setupValid || !IsSpawned)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_TERMINAL_REQUEST_REJECTED reason=coordinator_not_ready event={eventId}",
                    this);
                return false;
            }

            if (IsServer)
            {
                var localClientId = NetworkManager != null
                    ? NetworkManager.LocalClientId
                    : Unity.Netcode.NetworkManager.ServerClientId;
                return TrySpawnFromTerminalServer(eventId, localClientId);
            }

            RequestEventFromTerminalServerRpc(eventId);
            Debug.Log($"PHS_EVENT_TERMINAL_REQUEST_SENT event={eventId}", this);
            return true;
        }

        public bool RequestMiniGameResult(EventId eventId, bool succeeded)
        {
            if (!setupValid || !IsSpawned)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_MINIGAME_RESULT_REJECTED reason=coordinator_not_ready event={eventId}",
                    this);
                return false;
            }

            if (IsServer)
            {
                var localClientId = NetworkManager != null
                    ? NetworkManager.LocalClientId
                    : Unity.Netcode.NetworkManager.ServerClientId;
                return TryApplyMiniGameResultServer(eventId, succeeded, localClientId);
            }

            RequestMiniGameResultServerRpc(eventId, succeeded);
            Debug.Log(
                $"PHS_EVENT_MINIGAME_RESULT_SENT event={eventId} succeeded={succeeded}",
                this);
            return true;
        }

        public bool TrySpawnEventServer(EventId eventId, out ulong instanceId)
        {
            instanceId = 0UL;

            if (!CanSpawnEventServer(eventId))
            {
                return false;
            }

            var room = roomRegistry.GetRandomRoom();
            if (room == null)
            {
                Debug.LogWarning($"PHS_EVENT_SERVER_SPAWN_REJECTED reason=room_missing event={eventId}", this);
                return false;
            }

            return TrySpawnEventInRoomServer(eventId, room, out instanceId);
        }

        public bool TrySpawnEventServer(
            EventId eventId,
            ShipRoom room,
            out ulong instanceId)
        {
            instanceId = 0UL;

            if (!CanSpawnEventServer(eventId))
            {
                return false;
            }

            if (room == null)
            {
                Debug.LogWarning($"PHS_EVENT_SERVER_SPAWN_REJECTED reason=room_missing event={eventId}", this);
                return false;
            }

            return TrySpawnEventInRoomServer(eventId, room, out instanceId);
        }

        private bool CanSpawnEventServer(EventId eventId)
        {
            if (!IsAuthoritative)
            {
                Debug.LogWarning($"PHS_EVENT_SERVER_SPAWN_REJECTED reason=not_server event={eventId}", this);
                return false;
            }

            if (!setupValid || eventManager == null || roomRegistry == null)
            {
                Debug.LogError($"PHS_EVENT_SERVER_SPAWN_REJECTED reason=invalid_setup event={eventId}", this);
                return false;
            }

            if (IsEventActive(eventId) || eventManager.IsActive(eventId))
            {
                Debug.LogWarning($"PHS_EVENT_SERVER_SPAWN_REJECTED reason=already_active event={eventId}", this);
                return false;
            }

            return true;
        }

        private bool TrySpawnEventInRoomServer(
            EventId eventId,
            IRoom room,
            out ulong instanceId)
        {
            var accepted = eventManager.TrySpawnEvent(eventId, room, out instanceId);
            Debug.Log(
                $"PHS_EVENT_SERVER_SPAWN_RESULT accepted={accepted} instance={instanceId} event={eventId} room={room.RoomId}",
                this);
            return accepted;
        }

        public bool TryStartSchedulerServer()
        {
            if (!IsAuthoritative || !setupValid || eventScheduler == null)
            {
                Debug.LogWarning("PHS_EVENT_SCHEDULER_REQUEST_REJECTED action=start reason=not_ready_or_server", this);
                return false;
            }

            eventScheduler.StartScheduler();
            Debug.Log("PHS_EVENT_SCHEDULER_SERVER_STARTED", this);
            return true;
        }

        public bool TryStopSchedulerServer()
        {
            if (!IsAuthoritative || !setupValid || eventScheduler == null)
            {
                Debug.LogWarning("PHS_EVENT_SCHEDULER_REQUEST_REJECTED action=stop reason=not_ready_or_server", this);
                return false;
            }

            eventScheduler.StopScheduler();
            Debug.Log("PHS_EVENT_SCHEDULER_SERVER_STOPPED", this);
            return true;
        }

        public bool TryTerminateAllServer()
        {
            if (!IsAuthoritative || !setupValid || eventManager == null)
            {
                Debug.LogWarning("PHS_EVENT_TERMINATE_REJECTED reason=not_ready_or_server", this);
                return false;
            }

            eventScheduler?.ResetScheduler();
            var previousSuppression = suppressTerminalShipImpact;
            suppressTerminalShipImpact = true;
            try
            {
                eventManager.ForceClearAll();
            }
            finally
            {
                suppressTerminalShipImpact = previousSuppression;
            }

            Debug.Log("PHS_EVENT_TERMINATE_ALL_SERVER_COMPLETED", this);
            return true;
        }

        public bool TryApplyPowerOff(ulong eventInstanceId, out string reason)
        {
            if (!IsAuthoritative)
            {
                reason = "server_required";
                return false;
            }

            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null)
            {
                reason = "ship_systems_missing";
                return false;
            }

            if (!shipSystems.IsPowerEnabled && !shipSystems.IsGravityEnabled)
            {
                reason = null;
                Debug.Log(
                    $"PHS_EVENT_POWER_OFF_SKIPPED reason=already_applied instance={eventInstanceId}",
                    this);
                return true;
            }

            if (!shipSystems.TryPowerOff(out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_EVENT_POWER_OFF_APPLIED instance={eventInstanceId} revision={shipSystems.Revision}",
                this);
            return true;
        }

        public bool TryGetPowerOffState(out bool isPowerOff)
        {
            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null)
            {
                isPowerOff = false;
                return false;
            }

            isPowerOff = !shipSystems.IsPowerEnabled;
            return true;
        }

        public ulong AllocateEventInstanceId()
        {
            if (!IsAuthoritative)
            {
                Debug.LogError("PHS_EVENT_INSTANCE_ID_REJECTED reason=not_server", this);
                return 0UL;
            }

            nextEventInstanceId++;
            if (nextEventInstanceId == 0UL)
            {
                nextEventInstanceId++;
            }

            return nextEventInstanceId;
        }

        public uint AllocateEffectInstanceId(ulong eventInstanceId)
        {
            if (!IsAuthoritative || eventInstanceId == 0UL)
            {
                Debug.LogError(
                    $"PHS_EVENT_EFFECT_ID_REJECTED reason=server_or_event_required event={eventInstanceId}",
                    this);
                return 0U;
            }

            nextEffectInstanceId++;
            if (nextEffectInstanceId == 0U)
            {
                nextEffectInstanceId++;
            }

            return nextEffectInstanceId;
        }

        public void PublishEffectSpawned(
            ulong eventInstanceId,
            uint effectInstanceId,
            EventEffectKind effectKind,
            Vector3 worldPosition,
            byte variant)
        {
            if (!IsAuthoritative || eventInstanceId == 0UL || effectInstanceId == 0U)
            {
                Debug.LogError(
                    $"PHS_EVENT_EFFECT_SPAWN_REJECTED reason=authority_or_id_invalid event={eventInstanceId} effect={effectInstanceId}",
                    this);
                return;
            }

            if (FindEffectSnapshotIndex(effectInstanceId) >= 0)
            {
                Debug.LogError($"PHS_EVENT_EFFECT_SNAPSHOT_DUPLICATE effect={effectInstanceId}", this);
                return;
            }

            effectSnapshots.Add(new NetworkEventEffectSnapshot(
                eventInstanceId,
                effectInstanceId,
                effectKind,
                worldPosition,
                variant,
                EventEffectLifecycle.Active,
                1U,
                GetServerTime()));
            ApplyEffectShipImpact(eventInstanceId, effectInstanceId, effectKind);
            Debug.Log(
                $"PHS_EVENT_EFFECT_SPAWNED event={eventInstanceId} effect={effectInstanceId} kind={effectKind} variant={variant} position={worldPosition}",
                this);
        }

        private void ApplyEffectShipImpact(
            ulong eventInstanceId,
            uint effectInstanceId,
            EventEffectKind effectKind)
        {
            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_SHIP_IMPACT_FAILED reason=ship_systems_missing event={eventInstanceId} effect={effectInstanceId} kind={effectKind}",
                    this);
                return;
            }

            bool applied;
            string reason;
            switch (effectKind)
            {
                case EventEffectKind.Fire:
                    applied = shipSystems.TryApplyShipDamage(
                        fireHullDamagePerEffect,
                        "event_fire",
                        out reason);
                    break;
                case EventEffectKind.OxygenLeak:
                    applied = shipSystems.TryApplyModuleDamage(
                        NetworkShipModuleId.LifeSupport,
                        oxygenLifeSupportDamagePerEffect,
                        false,
                        "event_oxygen_leak",
                        out reason);
                    break;
                case EventEffectKind.Enemy:
                    applied = shipSystems.TryApplyModuleDamage(
                        NetworkShipModuleId.Engine,
                        enemyEngineDamagePerEffect,
                        false,
                        "event_enemy_intrusion",
                        out reason);
                    break;
                default:
                    applied = false;
                    reason = "unsupported_effect_kind";
                    break;
            }

            if (!applied)
            {
                Debug.LogError(
                    $"PHS_EVENT_SHIP_IMPACT_FAILED reason={reason} event={eventInstanceId} effect={effectInstanceId} kind={effectKind}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_EVENT_SHIP_IMPACT_APPLIED event={eventInstanceId} effect={effectInstanceId} kind={effectKind} shipRevision={shipSystems.Revision}",
                this);
        }

        public void PublishEffectRemoved(ulong eventInstanceId, uint effectInstanceId)
        {
            if (!IsAuthoritative)
            {
                return;
            }

            var index = FindEffectSnapshotIndex(effectInstanceId);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_EFFECT_REMOVE_SKIPPED reason=snapshot_missing event={eventInstanceId} effect={effectInstanceId}",
                    this);
                return;
            }

            var current = effectSnapshots[index];
            if (current.EventInstanceId != eventInstanceId || !current.IsActive)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_EFFECT_REMOVE_SKIPPED reason=event_or_lifecycle_mismatch event={eventInstanceId} effect={effectInstanceId}",
                    this);
                return;
            }

            var nextRevision = current.Revision + 1U;
            effectSnapshots[index] = new NetworkEventEffectSnapshot(
                current.EventInstanceId,
                current.EffectInstanceId,
                current.Kind,
                current.WorldPosition,
                current.Variant,
                EventEffectLifecycle.Removed,
                nextRevision,
                GetServerTime());
            StartCoroutine(RemoveEffectSnapshotAfterDelay(effectInstanceId, nextRevision));
            Debug.Log(
                $"PHS_EVENT_EFFECT_REMOVED event={eventInstanceId} effect={effectInstanceId} revision={nextRevision}",
                this);
        }

        public void PublishEffectPositionChanged(
            ulong eventInstanceId,
            uint effectInstanceId,
            Vector3 worldPosition)
        {
            if (!IsAuthoritative)
            {
                return;
            }

            var index = FindEffectSnapshotIndex(effectInstanceId);
            if (index < 0)
            {
                return;
            }

            var current = effectSnapshots[index];
            if (current.EventInstanceId != eventInstanceId || !current.IsActive
                || (current.WorldPosition - worldPosition).sqrMagnitude < 0.0001f)
            {
                return;
            }

            effectSnapshots[index] = new NetworkEventEffectSnapshot(
                current.EventInstanceId,
                current.EffectInstanceId,
                current.Kind,
                worldPosition,
                current.Variant,
                EventEffectLifecycle.Active,
                current.Revision + 1U,
                GetServerTime());
        }

        public void PublishEventStarted(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state)
        {
            if (!IsAuthoritative)
            {
                return;
            }

            UpsertLifecycleSnapshot(instanceId, eventId, roomId, state, true);
            Debug.Log(
                $"PHS_EVENT_LIFECYCLE_STARTED instance={instanceId} event={eventId} room={roomId}",
                this);
        }

        public void PublishEventStateChanged(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state)
        {
            if (!IsAuthoritative)
            {
                return;
            }

            var revision = UpsertLifecycleSnapshot(instanceId, eventId, roomId, state, false);
            Debug.Log(
                $"PHS_EVENT_LIFECYCLE_STATE instance={instanceId} event={eventId} state={state} revision={revision}",
                this);
        }

        public void PublishEventFinished(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state,
            bool success)
        {
            if (!IsAuthoritative)
            {
                return;
            }

            if (!suppressTerminalShipImpact)
            {
                ApplyTerminalShipImpact(instanceId, eventId, success);
            }
            else
            {
                Debug.Log(
                    $"PHS_EVENT_TERMINAL_IMPACT_SUPPRESSED " +
                    $"reason=forced_termination instance={instanceId} " +
                    $"event={eventId}",
                    this);
            }

            RemoveActiveEffectsForEvent(instanceId);

            var terminalState = state == EventState.Resolve || state == EventState.Fail
                ? state
                : success ? EventState.Resolve : EventState.Fail;
            var revision = UpsertLifecycleSnapshot(
                instanceId,
                eventId,
                roomId,
                terminalState,
                false);

            Debug.Log(
                $"PHS_EVENT_LIFECYCLE_FINISHED instance={instanceId} event={eventId} success={success} revision={revision}",
                this);
            StartCoroutine(RemoveTerminalSnapshotAfterDelay(instanceId, revision));
            NotifyServerEventFinished(instanceId, eventId, success);
        }

        private void NotifyServerEventFinished(
            ulong instanceId,
            EventId eventId,
            bool success)
        {
            var handlers = ServerEventFinished;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<ulong, EventId, bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(instanceId, eventId, success);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestEventFromTerminalServerRpc(
            EventId eventId,
            ServerRpcParams rpcParams = default)
        {
            TrySpawnFromTerminalServer(eventId, rpcParams.Receive.SenderClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestMiniGameResultServerRpc(
            EventId eventId,
            bool succeeded,
            ServerRpcParams rpcParams = default)
        {
            TryApplyMiniGameResultServer(
                eventId,
                succeeded,
                rpcParams.Receive.SenderClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestEffectRepairServerRpc(
            ulong eventInstanceId,
            uint effectInstanceId,
            FixedString64Bytes expectedItemId,
            uint expectedItemRevision,
            uint requestSequence,
            ServerRpcParams rpcParams = default)
        {
            TryApplyEffectRepairServer(
                eventInstanceId,
                effectInstanceId,
                expectedItemId.ToString(),
                expectedItemRevision,
                requestSequence,
                rpcParams.Receive.SenderClientId);
        }

        private bool TryApplyEffectRepairServer(
            ulong eventInstanceId,
            uint effectInstanceId,
            string expectedItemId,
            uint expectedItemRevision,
            uint requestSequence,
            ulong senderClientId)
        {
            if (!IsAuthoritative || !setupValid)
            {
                return RejectRepair("server_not_ready", eventInstanceId, effectInstanceId, senderClientId);
            }

            if (!repairTargets.TryGetValue(effectInstanceId, out var target)
                || target == null
                || target.EventInstanceId != eventInstanceId
                || target.IsRepairComplete)
            {
                return RejectRepair("target_inactive", eventInstanceId, effectInstanceId, senderClientId);
            }

            if (string.IsNullOrEmpty(expectedItemId) || expectedItemId != target.RequiredItemId)
            {
                return RejectRepair("item_contract", eventInstanceId, effectInstanceId, senderClientId);
            }

            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                return RejectRepair("player_missing", eventInstanceId, effectInstanceId, senderClientId);
            }

            var itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null
                || !itemRecord.IsSpawned
                || itemRecord.OwnerClientId != senderClientId
                || itemRecord.HeldItemId != expectedItemId
                || itemRecord.Revision != expectedItemRevision)
            {
                return RejectRepair("item_record_mismatch", eventInstanceId, effectInstanceId, senderClientId);
            }

            var sequenceKey = (senderClientId, expectedItemRevision);
            if (requestSequence == 0U
                || repairRequestSequences.TryGetValue(sequenceKey, out var previousSequence)
                && requestSequence <= previousSequence)
            {
                return RejectRepair("duplicate_sequence", eventInstanceId, effectInstanceId, senderClientId);
            }

            var distance = Vector3.Distance(client.PlayerObject.transform.position, target.RepairPosition);
            if (distance > serverRepairDistance)
            {
                return RejectRepair("distance", eventInstanceId, effectInstanceId, senderClientId);
            }

            repairRequestSequences[sequenceKey] = requestSequence;
            if (!target.TryApplyRepairStep(serverRepairStep))
            {
                return RejectRepair("apply_failed", eventInstanceId, effectInstanceId, senderClientId);
            }

            Debug.Log(
                $"PHS_EVENT_REPAIR_APPLIED event={eventInstanceId} effect={effectInstanceId} client={senderClientId} item={expectedItemId} revision={expectedItemRevision} sequence={requestSequence} distance={distance:F3} complete={target.IsRepairComplete}",
                this);
            return true;
        }

        private bool RejectRepair(
            string reason,
            ulong eventInstanceId,
            uint effectInstanceId,
            ulong senderClientId)
        {
            Debug.LogWarning(
                $"PHS_EVENT_REPAIR_REJECTED reason={reason} event={eventInstanceId} effect={effectInstanceId} client={senderClientId}",
                this);
            return false;
        }

        private bool TryApplyMiniGameResultServer(
            EventId eventId,
            bool succeeded,
            ulong senderClientId)
        {
            if (!IsAuthoritative || eventManager == null)
            {
                return false;
            }

            if (!TryValidateMiniGameResultRequest(
                    eventId,
                    senderClientId,
                    out var rejectionReason))
            {
                Debug.LogWarning(
                    $"PHS_EVENT_MINIGAME_RESULT_REJECTED reason={rejectionReason} event={eventId} client={senderClientId}",
                    this);
                return false;
            }

            var eventTarget = eventManager.GetMiniGameTarget(eventId.ToString());
            if (eventTarget == null)
            {
                Debug.LogWarning(
                    $"PHS_EVENT_MINIGAME_RESULT_REJECTED reason=event_not_active event={eventId} client={senderClientId}",
                    this);
                return false;
            }

            if (succeeded)
            {
                eventTarget.OnMiniGameSucceeded();
            }
            else
            {
                eventTarget.OnMiniGameFailed();
            }

            Debug.Log(
                $"PHS_EVENT_MINIGAME_RESULT_APPLIED event={eventId} succeeded={succeeded} client={senderClientId}",
                this);
            return true;
        }

        private bool TryValidateMiniGameResultRequest(
            EventId eventId,
            ulong senderClientId,
            out string rejectionReason)
        {
            if (!TryGetMiniGameType(eventId, out var miniGameType))
            {
                rejectionReason = "event_not_supported";
                return false;
            }

            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                rejectionReason = "player_missing";
                return false;
            }

            const float maximumResultDistance = 4f;
            var maximumResultDistanceSquared = maximumResultDistance * maximumResultDistance;
            var playerPosition = client.PlayerObject.transform.position;
            var terminals = FindObjectsByType<PHSFinalMiniGameTerminal>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var terminal in terminals)
            {
                if (terminal != null
                    && terminal.IsConfigured
                    && terminal.ConfiguredEventId == eventId
                    && terminal.ConfiguredMiniGameType == miniGameType
                    && (terminal.WorldPosition - playerPosition).sqrMagnitude
                    <= maximumResultDistanceSquared)
                {
                    rejectionReason = string.Empty;
                    return true;
                }
            }

            rejectionReason = "terminal_or_distance_invalid";
            return false;
        }

        private void ApplyTerminalShipImpact(
            ulong eventInstanceId,
            EventId eventId,
            bool success)
        {
            if (eventId != EventId.EmpAttack
                && eventId != EventId.MeteorAttack
                && eventId != EventId.EnemyScout)
            {
                return;
            }

            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_TERMINAL_IMPACT_FAILED reason=ship_systems_missing instance={eventInstanceId} event={eventId}",
                    this);
                return;
            }

            var impactSink = shipSystems.GetComponent<IShipEventImpactSink>();
            if (impactSink == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_TERMINAL_IMPACT_FAILED reason=impact_sink_missing instance={eventInstanceId} event={eventId}",
                    shipSystems);
                return;
            }

            if (!impactSink.TryApplyTerminalImpact(
                    eventInstanceId,
                    eventId,
                    success,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_EVENT_TERMINAL_IMPACT_FAILED reason={reason} instance={eventInstanceId} event={eventId}",
                    shipSystems);
            }
        }

        private static bool TryGetMiniGameType(EventId eventId, out MiniGameType miniGameType)
        {
            switch (eventId)
            {
                case EventId.EmpAttack:
                    miniGameType = MiniGameType.WireFix;
                    return true;
                case EventId.MeteorAttack:
                    miniGameType = MiniGameType.Cannon;
                    return true;
                case EventId.EnemyScout:
                    miniGameType = MiniGameType.PowerSync;
                    return true;
                default:
                    miniGameType = default;
                    return false;
            }
        }

        private bool TrySpawnFromTerminalServer(EventId eventId, ulong senderClientId)
        {
            if (!IsAuthoritative)
            {
                return false;
            }

            if (!TryValidateTerminalRequest(eventId, senderClientId, out var rejectionReason))
            {
                Debug.LogWarning(
                    $"PHS_EVENT_TERMINAL_REQUEST_REJECTED reason={rejectionReason} event={eventId} client={senderClientId}",
                    this);
                return false;
            }

            return TrySpawnEventServer(eventId, out _);
        }

        private bool TryValidateTerminalRequest(
            EventId eventId,
            ulong senderClientId,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                rejectionReason = "player_missing";
                return false;
            }

            var terminals = FindObjectsByType<ShipAccidentEventTerminal>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var playerPosition = client.PlayerObject.transform.position;
            foreach (var terminal in terminals)
            {
                if (terminal != null && terminal.IsServerRequestValid(eventId, playerPosition))
                {
                    return true;
                }
            }

            rejectionReason = "terminal_or_distance_invalid";
            return false;
        }

        private uint UpsertLifecycleSnapshot(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state,
            bool rejectDuplicate)
        {
            var index = FindSnapshotIndex(instanceId);
            if (index >= 0)
            {
                var current = lifecycleSnapshots[index];
                if (rejectDuplicate)
                {
                    Debug.LogError($"PHS_EVENT_SNAPSHOT_DUPLICATE instance={instanceId}", this);
                    return current.Revision;
                }

                if (current.EventId == eventId
                    && current.State == state
                    && current.RoomId.ToString() == (roomId ?? string.Empty))
                {
                    return current.Revision;
                }

                var nextRevision = current.Revision + 1U;
                lifecycleSnapshots[index] = new NetworkEventLifecycleSnapshot(
                    instanceId,
                    eventId,
                    roomId,
                    state,
                    nextRevision,
                    GetServerTime());
                return nextRevision;
            }

            const uint initialRevision = 1U;
            lifecycleSnapshots.Add(new NetworkEventLifecycleSnapshot(
                instanceId,
                eventId,
                roomId,
                state,
                initialRevision,
                GetServerTime()));
            return initialRevision;
        }

        private IEnumerator RemoveTerminalSnapshotAfterDelay(ulong instanceId, uint terminalRevision)
        {
            yield return new WaitForSecondsRealtime(terminalSnapshotRetentionSeconds);

            if (!IsAuthoritative)
            {
                yield break;
            }

            var index = FindSnapshotIndex(instanceId);
            if (index < 0)
            {
                yield break;
            }

            var snapshot = lifecycleSnapshots[index];
            if (snapshot.Revision != terminalRevision || !snapshot.IsTerminal)
            {
                yield break;
            }

            lifecycleSnapshots.RemoveAt(index);
            Debug.Log(
                $"PHS_EVENT_LIFECYCLE_REMOVED instance={instanceId} revision={terminalRevision}",
                this);
        }

        private IEnumerator RemoveEffectSnapshotAfterDelay(
            uint effectInstanceId,
            uint removedRevision)
        {
            yield return new WaitForSecondsRealtime(effectRemovalSnapshotRetentionSeconds);

            if (!IsAuthoritative)
            {
                yield break;
            }

            var index = FindEffectSnapshotIndex(effectInstanceId);
            if (index < 0)
            {
                yield break;
            }

            var snapshot = effectSnapshots[index];
            if (snapshot.Revision != removedRevision || snapshot.IsActive)
            {
                yield break;
            }

            effectSnapshots.RemoveAt(index);
            Debug.Log(
                $"PHS_EVENT_EFFECT_SNAPSHOT_REMOVED effect={effectInstanceId} revision={removedRevision}",
                this);
        }

        private void RemoveActiveEffectsForEvent(ulong eventInstanceId)
        {
            effectRemovalBuffer.Clear();
            foreach (var snapshot in effectSnapshots)
            {
                if (snapshot.EventInstanceId == eventInstanceId && snapshot.IsActive)
                {
                    effectRemovalBuffer.Add(snapshot.EffectInstanceId);
                }
            }

            foreach (var effectInstanceId in effectRemovalBuffer)
            {
                PublishEffectRemoved(eventInstanceId, effectInstanceId);
            }
        }

        private int FindSnapshotIndex(ulong instanceId)
        {
            for (var i = 0; i < lifecycleSnapshots.Count; i++)
            {
                if (lifecycleSnapshots[i].InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindEffectSnapshotIndex(uint effectInstanceId)
        {
            for (var i = 0; i < effectSnapshots.Count; i++)
            {
                if (effectSnapshots[i].EffectInstanceId == effectInstanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleLifecycleSnapshotsChanged(
            NetworkListEvent<NetworkEventLifecycleSnapshot> changeEvent)
        {
            RebuildSnapshotCache(changeEvent.Type.ToString());
        }

        private void HandleEffectSnapshotsChanged(
            NetworkListEvent<NetworkEventEffectSnapshot> changeEvent)
        {
            RebuildEffectSnapshotCache(changeEvent.Type.ToString());
        }

        private void RebuildSnapshotCache(string reason)
        {
            snapshotCache.Clear();
            foreach (var snapshot in lifecycleSnapshots)
            {
                if (snapshot.InstanceId != 0UL)
                {
                    snapshotCache[snapshot.InstanceId] = snapshot;
                }
            }

            LifecycleSnapshotsChanged?.Invoke();
            Debug.Log(
                $"PHS_EVENT_SNAPSHOT_CACHE_SYNC reason={reason} count={snapshotCache.Count} server={IsServer}",
                this);
        }

        private void RebuildEffectSnapshotCache(string reason)
        {
            effectSnapshotCache.Clear();
            foreach (var snapshot in effectSnapshots)
            {
                if (snapshot.EffectInstanceId != 0U)
                {
                    effectSnapshotCache[snapshot.EffectInstanceId] = snapshot;
                }
            }

            if (!IsServer && effectMirrorPresenter != null)
            {
                effectMirrorPresenter.Reconcile(effectSnapshotCache.Values);
            }

            EffectSnapshotsChanged?.Invoke();
            Debug.Log(
                $"PHS_EVENT_EFFECT_CACHE_SYNC reason={reason} count={effectSnapshotCache.Count} server={IsServer}",
                this);
        }

        private void InitializeServerSequence()
        {
            nextEventInstanceId = 0UL;
            foreach (var snapshot in lifecycleSnapshots)
            {
                if (snapshot.InstanceId > nextEventInstanceId)
                {
                    nextEventInstanceId = snapshot.InstanceId;
                }
            }

            nextEffectInstanceId = 0U;
            foreach (var snapshot in effectSnapshots)
            {
                if (snapshot.EffectInstanceId > nextEffectInstanceId)
                {
                    nextEffectInstanceId = snapshot.EffectInstanceId;
                }
            }
        }

        private double GetServerTime()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.realtimeSinceStartupAsDouble;
        }

        private bool ValidateSetup()
        {
            var valid = true;

            if (eventManager == null)
            {
                Debug.LogError("[NetworkEventCoordinator] EventManager Inspector reference is missing.", this);
                valid = false;
            }

            if (eventScheduler == null)
            {
                Debug.LogError("[NetworkEventCoordinator] EventScheduler Inspector reference is missing.", this);
                valid = false;
            }

            if (roomRegistry == null)
            {
                Debug.LogError("[NetworkEventCoordinator] RoomRegistry Inspector reference is missing.", this);
                valid = false;
            }

            if (effectMirrorPresenter == null)
            {
                Debug.LogError(
                    "[NetworkEventCoordinator] EffectMirrorPresenter Inspector reference is missing.",
                    this);
                valid = false;
            }
            else if (!effectMirrorPresenter.ValidateConfiguration())
            {
                valid = false;
            }

            return valid;
        }
    }
}
