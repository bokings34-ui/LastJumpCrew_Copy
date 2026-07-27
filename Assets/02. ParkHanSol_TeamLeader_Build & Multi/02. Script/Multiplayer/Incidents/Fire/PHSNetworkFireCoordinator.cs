using System;
using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkFireCoordinator : NetworkBehaviour
    {
        private const ulong FallbackScopeDomain = 0xF17E000000000000UL;
        // Start new neighbors at Medium heat so a visible fire front can keep
        // propagating instead of waiting through several Small-only ticks.
        private const ushort NewIgnitionHeat = 60;
        private const double ContainmentRetrySeconds = 0.5d;
        private const double UnitDoubleFromUInt64 =
            1d / 9007199254740992d;

        [Header("Inspector References")]
        [SerializeField]
        private PHSNetworkShipAccidentCoordinator accidentCoordinator;
        [SerializeField]
        private PHSFireAreaDamageGateway areaDamageGateway;
        [SerializeField]
        private PHSFireZone[] fireZones = Array.Empty<PHSFireZone>();

        [Header("Server Suppression Validation")]
        [SerializeField, Min(0.1f)]
        private float maximumSuppressionDistance = 7f;

        private readonly NetworkList<NetworkFirePatchSnapshot> activePatches =
            new(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly Dictionary<string, PHSFireZone> zonesByLocationId =
            new(StringComparer.Ordinal);
        private readonly Dictionary<
            (string LocationId, ushort PatchId),
            PHSFirePatchRuntimeTarget> targetsByPatch = new();
        private readonly Dictionary<uint, ActiveFireRuntime> activeFires =
            new();
        private readonly Dictionary<
            (ulong ClientId, ulong PlayerNetworkObjectId),
            uint> suppressionSequences = new();
        private readonly HashSet<uint> activeAccidentIds = new();
        private readonly List<uint> orderedActiveAccidentIds = new();
        private readonly HashSet<(string LocationId, ushort PatchId)>
            desiredPresentationTargets = new();
        private readonly List<uint> fireRemovalBuffer = new();
        private readonly List<PHSFirePatch> orderedPatchBuffer = new();
        private readonly List<PHSFirePatchLink> spreadLinkBuffer = new();
        private readonly List<NetworkFirePatchSnapshot> patchSnapshotBuffer =
            new();
        private readonly List<FireAreaDamageSample> damageSamples = new();

        private NetworkRunRandomLedger randomLedger;
        private bool setupValid;
        private bool reconcileRequested;

        private sealed class ActiveFireRuntime
        {
            public ActiveFireRuntime(
                uint accidentInstanceId,
                string locationId,
                PHSFireZone zone,
                PHSDeterministicRandom random,
                double nextSpreadTime,
                double nextDamageTime)
            {
                AccidentInstanceId = accidentInstanceId;
                LocationId = locationId;
                Zone = zone;
                Random = random;
                NextSpreadTime = nextSpreadTime;
                NextDamageTime = nextDamageTime;
            }

            public uint AccidentInstanceId { get; }
            public string LocationId { get; }
            public PHSFireZone Zone { get; }
            public PHSDeterministicRandom Random { get; }
            public double NextSpreadTime { get; set; }
            public double NextDamageTime { get; set; }
            public double ContainmentResolveAtServerTime { get; set; }
        }

        public int ActivePatchCount => activePatches.Count;
        public int ServerActiveFireCount => activeFires.Count;
        public IReadOnlyList<PHSFireZone> FireZones =>
            fireZones ?? Array.Empty<PHSFireZone>();
        public event Action ActivePatchesChanged;

        public bool IsPatchBurning(
            string locationId,
            ushort patchId)
        {
            if (string.IsNullOrWhiteSpace(locationId) || patchId == 0)
            {
                return false;
            }

            for (var index = 0; index < activePatches.Count; index++)
            {
                var snapshot = activePatches[index];
                if (snapshot.PatchId == patchId
                    && snapshot.Intensity != PHSFireIntensity.None
                    && string.Equals(
                        snapshot.LocationId.ToString(),
                        locationId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            setupValid = RebuildConfiguration(true, out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_FIRE_SETUP_FAILED reason={reason}",
                    this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            setupValid = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!setupValid
                || OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_FIRE_SETUP_FAILED " +
                    $"reason=server_owned_object_required " +
                    $"owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            activePatches.OnListChanged += HandlePatchListChanged;
            accidentCoordinator.ActiveAccidentsChanged +=
                HandleAccidentsChanged;
            NetworkRunSessionRoot.InstanceAvailable +=
                HandleRunSessionRootAvailable;
            TryBindRunSessionRoot(NetworkRunSessionRoot.Instance);
            if (IsClient)
            {
                RefreshPresentations();
            }

            reconcileRequested = IsServer;
            Debug.Log(
                $"PHS_FIRE_COORDINATOR_READY server={IsServer} " +
                $"zones={zonesByLocationId.Count} " +
                $"patches={targetsByPatch.Count}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            activePatches.OnListChanged -= HandlePatchListChanged;
            if (accidentCoordinator != null)
            {
                accidentCoordinator.ActiveAccidentsChanged -=
                    HandleAccidentsChanged;
            }

            NetworkRunSessionRoot.InstanceAvailable -=
                HandleRunSessionRootAvailable;
            if (IsServer && activePatches.Count > 0)
            {
                activePatches.Clear();
            }

            if (IsClient)
            {
                ClearAllPresentations();
            }

            activeFires.Clear();
            suppressionSequences.Clear();
            randomLedger = null;
            reconcileRequested = false;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !setupValid)
            {
                return;
            }

            if (reconcileRequested)
            {
                ReconcileActiveFireAccidents();
            }

            ProcessContainmentTicks();
            ProcessSpreadTicks();
            ProcessDamageTicks();
        }

        public NetworkFirePatchSnapshot GetActivePatchAt(int index)
        {
            if (index < 0 || index >= activePatches.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return activePatches[index];
        }

        public bool IsManagingAccident(uint accidentInstanceId)
        {
            if (accidentInstanceId == 0U)
            {
                return false;
            }

            if (activeFires.ContainsKey(accidentInstanceId))
            {
                return true;
            }

            for (var index = 0; index < activePatches.Count; index++)
            {
                if (activePatches[index].AccidentInstanceId
                    == accidentInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasBurningPatch(uint accidentInstanceId)
        {
            return accidentInstanceId != 0U
                && CountActivePatches(accidentInstanceId) > 0;
        }

        public bool TryValidate(out string reason)
        {
            setupValid = RebuildConfiguration(false, out reason);
            return setupValid;
        }

        public bool TryStartFireServer(
            uint accidentInstanceId,
            string requestedLocationId,
            ulong randomScopeKey,
            out string reason)
        {
            if (!CanExecuteServer(out reason))
            {
                return false;
            }

            if (!TryGetFireAccidentSnapshot(
                    accidentInstanceId,
                    out var accidentSnapshot))
            {
                reason =
                    $"fire_accident_missing:{accidentInstanceId}";
                return false;
            }

            if (activeFires.TryGetValue(
                    accidentInstanceId,
                    out var existing))
            {
                if (string.IsNullOrWhiteSpace(requestedLocationId)
                    || string.Equals(
                        existing.LocationId,
                        requestedLocationId,
                        StringComparison.Ordinal))
                {
                    reason = null;
                    return true;
                }

                reason =
                    $"fire_location_conflict:{existing.LocationId}:" +
                    $"{requestedLocationId}";
                return false;
            }

            if (!TryCreateRandomScope(
                    accidentInstanceId,
                    randomScopeKey,
                    out var random,
                    out reason))
            {
                return false;
            }

            if (!TryResolveAvailableZone(
                    accidentSnapshot,
                    requestedLocationId,
                    random,
                    out var locationId,
                    out var zone,
                    out reason))
            {
                return false;
            }

            if (!zone.TryCopyOrderedPatches(
                    orderedPatchBuffer,
                    out var patchReason)
                || orderedPatchBuffer.Count == 0)
            {
                reason = $"fire_zone_patches_invalid:{patchReason}";
                return false;
            }

            var seedPatch =
                orderedPatchBuffer[random.NextInt(orderedPatchBuffer.Count)];
            var currentTime = NetworkManager.ServerTime.Time;
            activeFires.Add(
                accidentInstanceId,
                new ActiveFireRuntime(
                    accidentInstanceId,
                    locationId,
                    zone,
                    random,
                    currentTime + zone.SpreadTickSeconds,
                    currentTime + zone.DamageTickSeconds));
            activePatches.Add(
                new NetworkFirePatchSnapshot
                {
                    AccidentInstanceId = accidentInstanceId,
                    LocationId = new FixedString64Bytes(locationId),
                    PatchId = seedPatch.PatchId,
                    Intensity =
                        PHSFireIntensityUtility.FromHeat(
                            zone.InitialHeat),
                    Heat = zone.InitialHeat,
                    Revision = 1U,
                    ChangedAtServerTime = currentTime
                });
            Debug.Log(
                $"PHS_FIRE_STARTED instance={accidentInstanceId} " +
                $"location={locationId} patch={seedPatch.PatchId} " +
                $"heat={zone.InitialHeat} " +
                $"intensity=" +
                $"{PHSFireIntensityUtility.FromHeat(zone.InitialHeat)} " +
                $"scope={random.ScopeKey}",
                this);
            reason = null;
            return true;
        }

        public bool TrySuppressPatchServer(
            uint accidentInstanceId,
            string locationId,
            ushort patchId,
            UtilityAttackHit hit,
            out string reason)
        {
            if (!CanMutateServer(out reason))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hit.ItemId))
            {
                reason = $"item_mismatch:{hit.ItemId}";
                return false;
            }

            if (hit.Attacker == null || hit.RequestSequence == 0U)
            {
                reason = "attacker_contract_invalid";
                return false;
            }

            if (!activeFires.TryGetValue(
                    accidentInstanceId,
                    out var runtime)
                || !string.Equals(
                    runtime.LocationId,
                    locationId,
                    StringComparison.Ordinal))
            {
                reason =
                    $"fire_runtime_missing:{accidentInstanceId}:{locationId}";
                return false;
            }

            var key = (locationId, patchId);
            if (!targetsByPatch.TryGetValue(key, out var target)
                || !runtime.Zone.TryResolvePatch(
                    patchId,
                    out var patch)
                || target.Patch != patch)
            {
                reason = $"fire_patch_missing:{locationId}:{patchId}";
                return false;
            }

            var itemRecord =
                hit.Attacker.GetComponentInParent<NetworkPlayerItemRecord>();
            if (itemRecord == null)
            {
                itemRecord =
                    hit.Attacker.GetComponentInChildren<
                        NetworkPlayerItemRecord>(true);
            }

            if (itemRecord == null
                || !itemRecord.IsSpawned
                || itemRecord.HeldItemId != hit.ItemId)
            {
                reason = "server_item_record_mismatch";
                return false;
            }

            var itemLifecycle =
                hit.Attacker.GetComponentInParent<
                    NetworkPlayerItemLifecycle>();
            if (itemLifecycle == null)
            {
                itemLifecycle =
                    hit.Attacker.GetComponentInChildren<
                        NetworkPlayerItemLifecycle>(true);
            }

            var itemRevision = itemRecord.Revision;
            if (itemLifecycle == null
                || !itemLifecycle.TryResolveHeldItemActionServer(
                    hit.ItemId,
                    itemRevision,
                    UtilityItemActionKind.FireSuppression,
                    out var actionProfile))
            {
                reason = "server_item_profile_mismatch";
                return false;
            }

            var sequenceKey =
                (
                    itemRecord.OwnerClientId,
                    itemRecord.NetworkObjectId);
            if (suppressionSequences.TryGetValue(
                    sequenceKey,
                    out var previousSequence)
                && hit.RequestSequence <= previousSequence)
            {
                reason =
                    $"suppression_sequence_replayed:{hit.RequestSequence}";
                return false;
            }

            var nearestPoint =
                patch.HazardBounds.ClosestPoint(
                    hit.Attacker.transform.position);
            var distance = Vector3.Distance(
                hit.Attacker.transform.position,
                nearestPoint);
            if (distance > maximumSuppressionDistance)
            {
                reason =
                    $"suppression_distance_exceeded:{distance:F3}:" +
                    $"{maximumSuppressionDistance:F3}";
                return false;
            }

            var snapshotIndex = FindPatchSnapshotIndex(
                accidentInstanceId,
                locationId,
                patchId);
            if (snapshotIndex < 0)
            {
                reason =
                    $"burning_patch_missing:{accidentInstanceId}:" +
                    $"{patchId}";
                return false;
            }

            var snapshot = activePatches[snapshotIndex];
            var previousHeat = snapshot.Heat;
            var previousIntensity = snapshot.Intensity;
            var suppressionHeat = (ushort)Mathf.Clamp(
                actionProfile.Amount,
                1,
                ushort.MaxValue);
            if (!itemLifecycle.TryCommitHeldItemActionServer(
                    hit.ItemId,
                    itemRevision,
                    actionProfile))
            {
                reason = "durability_commit_failed";
                return false;
            }

            var nextHeat = snapshot.Heat
                > suppressionHeat
                    ? (ushort)(
                        snapshot.Heat
                        - suppressionHeat)
                    : (ushort)0;
            var nextIntensity =
                PHSFireIntensityUtility.FromHeat(nextHeat);
            var isLastPatch =
                nextHeat == 0
                && CountActivePatches(accidentInstanceId) == 1;

            suppressionSequences[sequenceKey] = hit.RequestSequence;
            var feedback =
                hit.Attacker.GetComponent<PHSNetworkItemUseFeedbackController>();
            feedback?.PublishConfirmedTargetImpactServer(
                UtilityItemActionKind.FireSuppression,
                patch.HazardBounds.ClosestPoint(hit.Attacker.transform.position));
            if (nextHeat == 0)
            {
                activePatches.RemoveAt(snapshotIndex);
                if (isLastPatch)
                {
                    runtime.ContainmentResolveAtServerTime =
                        NetworkManager.ServerTime.Time
                        + runtime.Zone.ContainmentGraceSeconds;
                    Debug.Log(
                        $"PHS_FIRE_CONTAINMENT_STARTED " +
                        $"instance={accidentInstanceId} " +
                        $"location={locationId} " +
                        $"grace={runtime.Zone.ContainmentGraceSeconds:F2}",
                        this);
                }
            }
            else
            {
                snapshot.Intensity = nextIntensity;
                snapshot.Heat = nextHeat;
                snapshot.Revision++;
                snapshot.ChangedAtServerTime =
                    NetworkManager.ServerTime.Time;
                activePatches[snapshotIndex] = snapshot;
            }

            Debug.Log(
                $"PHS_FIRE_SUPPRESSED instance={accidentInstanceId} " +
                $"location={locationId} patch={patchId} " +
                $"heat={previousHeat}->{nextHeat} " +
                $"amount={suppressionHeat} " +
                $"intensity={previousIntensity}->{nextIntensity} " +
                $"client={itemRecord.OwnerClientId} " +
                $"contained={isLastPatch}",
                this);
            reason = null;
            return true;
        }

        public bool TryTerminateFireServer(
            uint accidentInstanceId,
            string cause,
            out string reason)
        {
            if (!CanMutateServer(out reason))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(cause))
            {
                reason = "termination_cause_required";
                return false;
            }

            StopFireRuntimeServer(accidentInstanceId, cause);
            reason = null;
            return true;
        }

        private void ProcessContainmentTicks()
        {
            if (activeFires.Count == 0)
            {
                return;
            }

            var currentTime = NetworkManager.ServerTime.Time;
            fireRemovalBuffer.Clear();
            foreach (var pair in activeFires)
            {
                var runtime = pair.Value;
                if (runtime.ContainmentResolveAtServerTime <= 0d
                    || currentTime
                        < runtime.ContainmentResolveAtServerTime)
                {
                    continue;
                }

                if (CountActivePatches(pair.Key) > 0)
                {
                    runtime.ContainmentResolveAtServerTime = 0d;
                    continue;
                }

                fireRemovalBuffer.Add(pair.Key);
            }

            fireRemovalBuffer.Sort();
            foreach (var accidentInstanceId in fireRemovalBuffer)
            {
                if (!activeFires.TryGetValue(
                        accidentInstanceId,
                        out var runtime))
                {
                    continue;
                }

                if (!accidentCoordinator.TryResolveAccidentServer(
                        accidentInstanceId,
                        "fire_contained",
                        out var resolveReason))
                {
                    runtime.ContainmentResolveAtServerTime =
                        currentTime + ContainmentRetrySeconds;
                    Debug.LogError(
                        $"PHS_FIRE_CONTAINMENT_RESOLVE_FAILED " +
                        $"instance={accidentInstanceId} " +
                        $"reason={resolveReason}",
                        this);
                    continue;
                }

                StopFireRuntimeServer(
                    accidentInstanceId,
                    "fire_contained");
            }
        }

        private void ProcessSpreadTicks()
        {
            if (activeFires.Count == 0)
            {
                return;
            }

            var currentTime = NetworkManager.ServerTime.Time;
            foreach (var runtime in activeFires.Values)
            {
                if (currentTime < runtime.NextSpreadTime)
                {
                    continue;
                }

                runtime.NextSpreadTime =
                    currentTime + runtime.Zone.SpreadTickSeconds;
                ProcessSpreadTick(runtime, currentTime);
            }
        }

        private void ProcessSpreadTick(
            ActiveFireRuntime runtime,
            double currentTime)
        {
            CopyPatchSnapshots(
                runtime.AccidentInstanceId,
                patchSnapshotBuffer);
            if (patchSnapshotBuffer.Count == 0)
            {
                return;
            }

            foreach (var current in patchSnapshotBuffer)
            {
                if (current.Heat >= runtime.Zone.MaximumHeat)
                {
                    continue;
                }

                var index = FindPatchSnapshotIndex(
                    current.AccidentInstanceId,
                    current.LocationId.ToString(),
                    current.PatchId);
                if (index < 0)
                {
                    continue;
                }

                var grown = activePatches[index];
                var heatGrowthRange =
                    runtime.Zone.MaximumHeatGrowthPerTick
                    - runtime.Zone.MinimumHeatGrowthPerTick
                    + 1;
                var heatGrowth =
                    runtime.Zone.MinimumHeatGrowthPerTick
                    + runtime.Random.NextInt(heatGrowthRange);
                grown.Heat = (ushort)Math.Min(
                    runtime.Zone.MaximumHeat,
                    grown.Heat + heatGrowth);
                grown.Intensity =
                    PHSFireIntensityUtility.FromHeat(grown.Heat);
                grown.Revision++;
                grown.ChangedAtServerTime = currentTime;
                activePatches[index] = grown;
            }

            CopyPatchSnapshots(
                runtime.AccidentInstanceId,
                patchSnapshotBuffer);
            var newIgnitions = 0;
            for (var attempt = 0;
                 attempt < runtime.Zone.SpreadAttemptsPerTick
                 && newIgnitions
                    < runtime.Zone.MaximumNewIgnitionsPerTick
                 && CountActivePatches(runtime.AccidentInstanceId)
                    < runtime.Zone.MaximumBurningPatches;
                 attempt++)
            {
                var source =
                    patchSnapshotBuffer[
                        runtime.Random.NextInt(
                            patchSnapshotBuffer.Count)];
                if (!runtime.Zone.TryCopySpreadCandidates(
                        source.PatchId,
                        source.Intensity,
                        spreadLinkBuffer,
                        out _))
                {
                    continue;
                }

                var totalWeight = 0d;
                foreach (var link in spreadLinkBuffer)
                {
                    if (FindPatchSnapshotIndex(
                            runtime.AccidentInstanceId,
                            runtime.LocationId,
                            link.Target.PatchId) < 0)
                    {
                        totalWeight += link.SpreadWeight;
                    }
                }

                if (totalWeight <= 0d)
                {
                    continue;
                }

                var weightedRoll =
                    NextUnit(runtime.Random) * totalWeight;
                PHSFirePatch selectedPatch = null;
                foreach (var link in spreadLinkBuffer)
                {
                    if (FindPatchSnapshotIndex(
                            runtime.AccidentInstanceId,
                            runtime.LocationId,
                            link.Target.PatchId) >= 0)
                    {
                        continue;
                    }

                    selectedPatch = link.Target;
                    weightedRoll -= link.SpreadWeight;
                    if (weightedRoll < 0d)
                    {
                        break;
                    }
                }

                if (selectedPatch == null)
                {
                    continue;
                }

                var spreadChance = Mathf.Clamp01(
                    runtime.Zone.BaseSpreadChance
                    * selectedPatch.Flammability
                    * (byte)source.Intensity
                    / (byte)PHSFireIntensityUtility.FromHeat(
                        runtime.Zone.MaximumHeat));
                if (spreadChance <= 0f
                    || NextUnit(runtime.Random) >= spreadChance)
                {
                    continue;
                }

                activePatches.Add(
                    new NetworkFirePatchSnapshot
                    {
                        AccidentInstanceId =
                            runtime.AccidentInstanceId,
                        LocationId =
                            new FixedString64Bytes(
                                runtime.LocationId),
                        PatchId = selectedPatch.PatchId,
                        Intensity =
                            PHSFireIntensity.Small,
                        Heat = (ushort)Math.Min(
                            NewIgnitionHeat,
                            runtime.Zone.MaximumHeat),
                        Revision = 1U,
                        ChangedAtServerTime = currentTime
                    });
                newIgnitions++;
                CopyPatchSnapshots(
                    runtime.AccidentInstanceId,
                    patchSnapshotBuffer);
                Debug.Log(
                    $"PHS_FIRE_SPREAD instance=" +
                    $"{runtime.AccidentInstanceId} " +
                    $"location={runtime.LocationId} " +
                    $"source={source.PatchId} " +
                    $"target={selectedPatch.PatchId} " +
                    $"burning={patchSnapshotBuffer.Count}",
                    this);
            }
        }

        private void ProcessDamageTicks()
        {
            damageSamples.Clear();
            var currentTime = NetworkManager.ServerTime.Time;
            foreach (var runtime in activeFires.Values)
            {
                if (currentTime < runtime.NextDamageTime)
                {
                    continue;
                }

                runtime.NextDamageTime =
                    currentTime + runtime.Zone.DamageTickSeconds;
                for (var index = 0;
                     index < activePatches.Count;
                     index++)
                {
                    var snapshot = activePatches[index];
                    if (snapshot.AccidentInstanceId
                            != runtime.AccidentInstanceId
                        || !runtime.Zone.TryResolvePatch(
                            snapshot.PatchId,
                            out var patch))
                    {
                        continue;
                    }

                    damageSamples.Add(
                        new FireAreaDamageSample(
                            patch,
                            snapshot.Intensity,
                            runtime.Zone.BaseDamagePerTick,
                            runtime.Zone.DamageableLayers));
                }
            }

            if (damageSamples.Count == 0)
            {
                return;
            }

            if (!areaDamageGateway.TryApplyDamageServer(
                    damageSamples,
                    out var damagedTargets,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_FIRE_DAMAGE_FAILED reason={reason}",
                    this);
                return;
            }

            if (damagedTargets > 0)
            {
                Debug.Log(
                    $"PHS_FIRE_DAMAGE_APPLIED " +
                    $"patches={damageSamples.Count} " +
                    $"targets={damagedTargets}",
                    this);
            }
        }

        private void ReconcileActiveFireAccidents()
        {
            reconcileRequested = false;
            activeAccidentIds.Clear();
            orderedActiveAccidentIds.Clear();
            for (var index = 0;
                 index < accidentCoordinator.ActiveAccidentCount;
                 index++)
            {
                var snapshot =
                    accidentCoordinator.GetActiveAccidentAt(index);
                if (snapshot.AccidentId == PHSShipAccidentId.Fire)
                {
                    activeAccidentIds.Add(snapshot.InstanceId);
                    orderedActiveAccidentIds.Add(
                        snapshot.InstanceId);
                }
            }

            orderedActiveAccidentIds.Sort();
            PruneOrphanPatchSnapshotsServer();

            fireRemovalBuffer.Clear();
            foreach (var pair in activeFires)
            {
                if (!activeAccidentIds.Contains(pair.Key))
                {
                    fireRemovalBuffer.Add(pair.Key);
                }
            }

            foreach (var instanceId in fireRemovalBuffer)
            {
                StopFireRuntimeServer(
                    instanceId,
                    "accident_removed");
            }

            foreach (var instanceId in orderedActiveAccidentIds)
            {
                if (activeFires.ContainsKey(instanceId))
                {
                    continue;
                }

                if (!TryStartFireServer(
                        instanceId,
                        null,
                        FallbackScopeDomain | instanceId,
                        out var reason))
                {
                    Debug.LogError(
                        $"PHS_FIRE_FALLBACK_START_FAILED " +
                        $"instance={instanceId} reason={reason}",
                        this);
                }
            }
        }

        private bool RebuildConfiguration(
            bool bindTargets,
            out string reason)
        {
            zonesByLocationId.Clear();
            targetsByPatch.Clear();

            if (accidentCoordinator == null)
            {
                reason = "accident_coordinator_missing";
                return false;
            }

            if (areaDamageGateway == null)
            {
                reason = "area_damage_gateway_missing";
                return false;
            }

            if (accidentCoordinator.FireCoordinator != this)
            {
                reason =
                    "accident_fire_coordinator_reference_mismatch";
                return false;
            }

            if (areaDamageGateway.gameObject != gameObject)
            {
                reason =
                    "area_damage_gateway_owner_mismatch";
                return false;
            }

            if (!areaDamageGateway.TryValidate(
                    out var damageGatewayReason))
            {
                reason =
                    $"area_damage_gateway_invalid:" +
                    $"{damageGatewayReason}";
                return false;
            }

            if (fireZones == null || fireZones.Length == 0)
            {
                reason = "fire_zones_empty";
                return false;
            }

            if (maximumSuppressionDistance <= 0f
                || float.IsNaN(maximumSuppressionDistance)
                || float.IsInfinity(maximumSuppressionDistance))
            {
                reason =
                    $"suppression_distance_invalid:" +
                    $"{maximumSuppressionDistance}";
                return false;
            }

            foreach (var zone in fireZones)
            {
                if (zone == null)
                {
                    reason = "fire_zone_missing";
                    return false;
                }

                if (!zone.TryValidate(out var zoneReason))
                {
                    reason =
                        $"fire_zone_invalid:{zone.name}:{zoneReason}";
                    return false;
                }

                var location =
                    zone.GetComponent<PHSIncidentLocationAnchor>();
                if (location == null
                    || location.Kind !=
                        IncidentLocationKind.FireSurface)
                {
                    reason =
                        $"fire_location_missing:{zone.name}";
                    return false;
                }

                if (!zonesByLocationId.TryAdd(
                        location.LocationId,
                        zone))
                {
                    reason =
                        $"fire_location_duplicate:" +
                        $"{location.LocationId}";
                    return false;
                }

                foreach (var patch in zone.Patches)
                {
                    var target =
                        patch.GetComponent<
                            PHSFirePatchRuntimeTarget>();
                    var key =
                        (location.LocationId, patch.PatchId);
                    if (target == null
                        || !targetsByPatch.TryAdd(key, target))
                    {
                        reason =
                            $"fire_patch_target_invalid:" +
                            $"{location.LocationId}:" +
                            $"{patch.PatchId}";
                        return false;
                    }

                    if (bindTargets)
                    {
                        target.Bind(
                            this,
                            location.LocationId,
                            zone.PatchPresentationPrefab);
                    }
                }
            }

            reason = null;
            return true;
        }

        private bool CanExecuteServer(out string reason)
        {
            if (!CanMutateServer(out reason))
            {
                return false;
            }

            if (randomLedger == null
                || !randomLedger.IsSpawned
                || !randomLedger.IsServer)
            {
                TryBindRunSessionRoot(
                    NetworkRunSessionRoot.Instance);
                if (randomLedger == null
                    || !randomLedger.IsSpawned
                    || !randomLedger.IsServer)
                {
                    reason = "random_ledger_not_ready";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool CanMutateServer(out string reason)
        {
            if (!setupValid || !IsSpawned || !IsServer)
            {
                reason = "server_authority_required";
                return false;
            }

            if (accidentCoordinator == null
                || !accidentCoordinator.IsSpawned
                || !accidentCoordinator.IsServer)
            {
                reason = "accident_coordinator_not_ready";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryCreateRandomScope(
            uint accidentInstanceId,
            ulong requestedScopeKey,
            out PHSDeterministicRandom random,
            out string reason)
        {
            var scopeKey = requestedScopeKey != 0UL
                ? requestedScopeKey
                : FallbackScopeDomain | accidentInstanceId;
            return randomLedger.TryCreateServerScope(
                NetworkRunRandomStream.IncidentSpread,
                scopeKey,
                out random,
                out reason);
        }

        private bool TryResolveAvailableZone(
            NetworkShipAccidentSnapshot accidentSnapshot,
            string requestedLocationId,
            PHSDeterministicRandom random,
            out string locationId,
            out PHSFireZone zone,
            out string reason)
        {
            var anchorId = accidentSnapshot.AnchorId.ToString();
            if (!string.IsNullOrWhiteSpace(requestedLocationId))
            {
                if (!zonesByLocationId.TryGetValue(
                        requestedLocationId,
                        out zone))
                {
                    locationId = null;
                    reason =
                        $"fire_location_missing:" +
                        $"{requestedLocationId}";
                    return false;
                }

                if (zone.FireAccidentAnchor == null
                    || zone.FireAccidentAnchor.AnchorId != anchorId)
                {
                    locationId = null;
                    zone = null;
                    reason =
                        $"fire_anchor_mismatch:" +
                        $"{requestedLocationId}:{anchorId}";
                    return false;
                }

                if (IsZoneInUse(zone))
                {
                    locationId = null;
                    zone = null;
                    reason =
                        $"fire_zone_occupied:{requestedLocationId}";
                    return false;
                }

                locationId = requestedLocationId;
                reason = null;
                return true;
            }

            var candidateIds = new List<string>();
            foreach (var pair in zonesByLocationId)
            {
                if (pair.Value.FireAccidentAnchor != null
                    && pair.Value.FireAccidentAnchor.AnchorId
                        == anchorId
                    && !IsZoneInUse(pair.Value))
                {
                    candidateIds.Add(pair.Key);
                }
            }

            candidateIds.Sort(StringComparer.Ordinal);
            if (candidateIds.Count == 0)
            {
                locationId = null;
                zone = null;
                reason =
                    $"fire_zone_unavailable:{anchorId}";
                return false;
            }

            locationId =
                candidateIds[random.NextInt(candidateIds.Count)];
            zone = zonesByLocationId[locationId];
            reason = null;
            return true;
        }

        private bool TryGetFireAccidentSnapshot(
            uint accidentInstanceId,
            out NetworkShipAccidentSnapshot snapshot)
        {
            for (var index = 0;
                 index < accidentCoordinator.ActiveAccidentCount;
                 index++)
            {
                var candidate =
                    accidentCoordinator.GetActiveAccidentAt(index);
                if (candidate.InstanceId == accidentInstanceId
                    && candidate.AccidentId
                        == PHSShipAccidentId.Fire)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private bool IsZoneInUse(PHSFireZone zone)
        {
            foreach (var runtime in activeFires.Values)
            {
                if (runtime.Zone == zone)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopFireRuntimeServer(
            uint accidentInstanceId,
            string cause)
        {
            activeFires.Remove(accidentInstanceId);
            for (var index = activePatches.Count - 1;
                 index >= 0;
                 index--)
            {
                if (activePatches[index].AccidentInstanceId
                    == accidentInstanceId)
                {
                    activePatches.RemoveAt(index);
                }
            }

            if (activeFires.Count == 0)
            {
                suppressionSequences.Clear();
            }

            Debug.Log(
                $"PHS_FIRE_STOPPED instance={accidentInstanceId} " +
                $"cause={cause}",
                this);
        }

        private void PruneOrphanPatchSnapshotsServer()
        {
            var removedCount = 0;
            for (var index = activePatches.Count - 1;
                 index >= 0;
                 index--)
            {
                var accidentInstanceId =
                    activePatches[index].AccidentInstanceId;
                if (activeAccidentIds.Contains(accidentInstanceId)
                    && activeFires.ContainsKey(accidentInstanceId))
                {
                    continue;
                }

                activePatches.RemoveAt(index);
                removedCount++;
            }

            if (removedCount > 0)
            {
                Debug.LogWarning(
                    $"PHS_FIRE_ORPHAN_PATCHES_PRUNED " +
                    $"count={removedCount}",
                    this);
            }
        }

        private int FindPatchSnapshotIndex(
            uint accidentInstanceId,
            string locationId,
            ushort patchId)
        {
            for (var index = 0;
                 index < activePatches.Count;
                 index++)
            {
                var snapshot = activePatches[index];
                if (snapshot.AccidentInstanceId
                        == accidentInstanceId
                    && snapshot.PatchId == patchId
                    && snapshot.LocationId.ToString()
                        == locationId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountActivePatches(uint accidentInstanceId)
        {
            var count = 0;
            for (var index = 0;
                 index < activePatches.Count;
                 index++)
            {
                if (activePatches[index].AccidentInstanceId
                    == accidentInstanceId)
                {
                    count++;
                }
            }

            return count;
        }

        private void CopyPatchSnapshots(
            uint accidentInstanceId,
            List<NetworkFirePatchSnapshot> destination)
        {
            destination.Clear();
            for (var index = 0;
                 index < activePatches.Count;
                 index++)
            {
                var snapshot = activePatches[index];
                if (snapshot.AccidentInstanceId
                    == accidentInstanceId)
                {
                    destination.Add(snapshot);
                }
            }
        }

        private static double NextUnit(
            PHSDeterministicRandom random)
        {
            return (random.NextUInt64() >> 11)
                * UnitDoubleFromUInt64;
        }

        private void HandlePatchListChanged(
            NetworkListEvent<NetworkFirePatchSnapshot> changeEvent)
        {
            if (IsClient)
            {
                RefreshPresentations();
            }

            ActivePatchesChanged?.Invoke();
        }

        private void HandleAccidentsChanged()
        {
            if (IsServer)
            {
                reconcileRequested = true;
            }
        }

        private void HandleRunSessionRootAvailable(
            NetworkRunSessionRoot root)
        {
            TryBindRunSessionRoot(root);
            if (IsServer)
            {
                reconcileRequested = true;
            }
        }

        private void TryBindRunSessionRoot(
            NetworkRunSessionRoot root)
        {
            if (root != null && root.Rng != null)
            {
                randomLedger = root.Rng;
            }
        }

        private void RefreshPresentations()
        {
            desiredPresentationTargets.Clear();
            for (var index = 0;
                 index < activePatches.Count;
                 index++)
            {
                var snapshot = activePatches[index];
                var key = (
                    LocationId: snapshot.LocationId.ToString(),
                    PatchId: snapshot.PatchId);
                if (!targetsByPatch.TryGetValue(
                        key,
                        out var target))
                {
                    Debug.LogError(
                        $"PHS_FIRE_PRESENTATION_FAILED " +
                        $"reason=patch_target_missing " +
                        $"location={key.LocationId} " +
                        $"patch={key.PatchId}",
                        this);
                    continue;
                }

                desiredPresentationTargets.Add(key);
                target.ApplySnapshot(
                    snapshot.AccidentInstanceId,
                    snapshot.Intensity);
            }

            foreach (var pair in targetsByPatch)
            {
                if (!desiredPresentationTargets.Contains(pair.Key))
                {
                    pair.Value.ClearSnapshot();
                }
            }
        }

        private void ClearAllPresentations()
        {
            foreach (var target in targetsByPatch.Values)
            {
                target.ClearSnapshot();
            }
        }
    }
}
