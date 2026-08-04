using System;
using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkFoamCoordinator : NetworkBehaviour
    {
        public const string FoamItemId = "foam_sealant_gun";
        public const int FireBlobThreshold = 1;
        public const int HullBreachBlobThreshold = 1;
        public const int SurfaceBlobThreshold = 3;

        [Header("Prefab")]
        [SerializeField] private GameObject foamBlobPrefab;

        [Header("Server Projectile")]
        [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1f)] private float projectileSpeed = 18f;
        [SerializeField, Min(0.1f)] private float maximumRange = 8f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.08f;

        [Header("Capacity")]
        [SerializeField, Min(1)] private int maximumBlobsPerOwner = 20;
        [SerializeField, Min(1)] private int maximumBlobsGlobal = 96;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float pendingTargetLifetime = 8f;
        [SerializeField, Min(0.1f)] private float surfaceLifetime = 20f;
        [SerializeField, Min(0f)] private float completionHoldSeconds = 2f;
        [SerializeField, Min(0.05f)] private float dissolveSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float hullCaptureRadius = 0.9f;
        [SerializeField, Min(0.05f)] private float surfaceClusterRadius = 0.65f;

        private readonly NetworkList<NetworkFoamTargetSnapshot>
            targetSnapshots = new(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly Dictionary<ulong, PHSNetworkFoamBlob> activeBlobs =
            new();
        private readonly Dictionary<ulong, int> ownerBlobCounts = new();
        private readonly Dictionary<string, FoamAccumulator> accumulators =
            new(StringComparer.Ordinal);
        private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
        private readonly List<string> cleanupKeys = new();

        private uint nextSurfaceClusterId;
        private bool setupValid;

        public static PHSNetworkFoamCoordinator Instance { get; private set; }
        public int TargetSnapshotCount => targetSnapshots.Count;
        public GameObject FoamBlobPrefab => foamBlobPrefab;
        public LayerMask HitLayers => hitLayers;
        public float ProjectileSpeed => projectileSpeed;
        public float MaximumRange => maximumRange;
        public float CollisionRadius => collisionRadius;
        public int MaximumBlobsPerOwner => maximumBlobsPerOwner;
        public int MaximumBlobsGlobal => maximumBlobsGlobal;
        public float PendingTargetLifetime => pendingTargetLifetime;
        public float SurfaceLifetime => surfaceLifetime;
        public float CompletionHoldSeconds => completionHoldSeconds;
        public float DissolveSeconds => dissolveSeconds;
        public float HullCaptureRadius => hullCaptureRadius;
        public float SurfaceClusterRadius => surfaceClusterRadius;
        public int ImpactBufferCapacity => hitBuffer.Length;
        public bool RejectsSaturatedImpactCasts => true;
        public bool HasRequiredReferences => foamBlobPrefab != null
            && foamBlobPrefab.GetComponent<NetworkObject>() != null
            && foamBlobPrefab.GetComponent<PHSNetworkFoamBlob>() != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            setupValid = ValidateSetup(out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_FOAM_SETUP_FAILED reason={reason} coordinator={name}",
                    this);
                enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!setupValid || OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_FOAM_SETUP_FAILED reason=server_owned_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_FOAM_SETUP_FAILED reason=duplicate current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            activeBlobs.Clear();
            ownerBlobCounts.Clear();
            accumulators.Clear();
            cleanupKeys.Clear();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsSpawned && IsServer)
            {
                UpdateServerCleanup(NetworkManager.ServerTime.Time);
            }
        }

        public NetworkFoamTargetSnapshot GetTargetSnapshotAt(int index)
        {
            if (index < 0 || index >= targetSnapshots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return targetSnapshots[index];
        }

        public bool TrySpawnShotServer(
            NetworkObject shooter,
            Vector3 origin,
            Vector3 direction,
            uint shotSequence,
            out string reason)
        {
            if (!IsSpawned || !IsServer || !setupValid)
            {
                reason = "server_not_ready";
                return false;
            }

            if (shooter == null
                || !shooter.IsSpawned
                || shotSequence == 0U
                || !IsFinite(origin)
                || !IsFinite(direction)
                || direction.sqrMagnitude < 0.99f
                || direction.sqrMagnitude > 1.01f)
            {
                reason = "shot_contract_invalid";
                return false;
            }

            var ownerClientId = shooter.OwnerClientId;
            ownerBlobCounts.TryGetValue(ownerClientId, out var ownerCount);
            if (ownerCount >= maximumBlobsPerOwner)
            {
                reason = "owner_capacity";
                return false;
            }

            if (activeBlobs.Count >= maximumBlobsGlobal)
            {
                reason = "global_capacity";
                return false;
            }

            var instance = Instantiate(
                foamBlobPrefab,
                origin,
                Quaternion.LookRotation(direction, Vector3.up));
            var networkObject = instance.GetComponent<NetworkObject>();
            var blob = instance.GetComponent<PHSNetworkFoamBlob>();
            if (networkObject == null || blob == null)
            {
                Destroy(instance);
                reason = "blob_prefab_contract";
                return false;
            }

            networkObject.Spawn(true);
            var now = NetworkManager.ServerTime.Time;
            blob.InitializeServer(
                this,
                shooter.NetworkObjectId,
                ownerClientId,
                shotSequence,
                origin,
                direction.normalized,
                projectileSpeed,
                now,
                now + (maximumRange / projectileSpeed));
            activeBlobs.Add(networkObject.NetworkObjectId, blob);
            ownerBlobCounts[ownerClientId] = ownerCount + 1;
            reason = null;
            return true;
        }

        internal bool TryResolveFirstImpactServer(
            PHSNetworkFoamBlob blob,
            Vector3 origin,
            Vector3 direction,
            float distance,
            out RaycastHit resolvedHit,
            out bool shotTerminated)
        {
            resolvedHit = default;
            shotTerminated = false;
            if (!IsServer || blob == null || distance <= 0f)
            {
                return false;
            }

            if (!TryResolveShooter(blob, out var shooter))
            {
                ReleaseFlyingBlobServer(blob, "shooter_missing");
                shotTerminated = true;
                return false;
            }

            var count = Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                hitBuffer,
                distance,
                hitLayers,
                QueryTriggerInteraction.Collide);
            if (count >= hitBuffer.Length)
            {
                ReleaseFlyingBlobServer(blob, "impact_cast_saturated");
                shotTerminated = true;
                return false;
            }

            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidate = hitBuffer[index];
                if (candidate.collider == null
                    || candidate.collider.transform.root == shooter.transform.root
                    || candidate.collider.GetComponentInParent<PHSNetworkFoamBlob>() != null
                    || (candidate.collider.isTrigger
                        && !IsSupportedTargetTrigger(candidate.collider))
                    || candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                resolvedHit = candidate;
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }

        internal void HandleImpactServer(
            PHSNetworkFoamBlob blob,
            RaycastHit hit)
        {
            if (!IsServer
                || blob == null
                || blob.Snapshot.Phase != NetworkFoamBlobPhase.Flying
                || hit.collider == null)
            {
                return;
            }

            if (!TryResolveShooter(blob, out var shooter))
            {
                ReleaseFlyingBlobServer(blob, "shooter_missing");
                return;
            }

            var now = NetworkManager.ServerTime.Time;
            if (!TryResolveAccumulator(
                    hit,
                    now,
                    out var accumulator,
                    out var attachPosition))
            {
                ReleaseFlyingBlobServer(blob, "impact_target_invalid");
                return;
            }

            accumulator.Current = Mathf.Min(
                accumulator.Required,
                accumulator.Current + 1);
            accumulator.LastHitAt = now;
            accumulator.Normal = BlendNormal(
                accumulator.Normal,
                hit.normal,
                accumulator.Current);
            accumulator.BlobIds.Add(blob.NetworkObjectId);
            if (accumulator.Kind == NetworkFoamTargetKind.Surface)
            {
                accumulator.Center = Vector3.Lerp(
                    accumulator.Center,
                    hit.point,
                    1f / accumulator.Current);
            }

            blob.AttachServer(
                accumulator.Kind,
                accumulator.ClusterId,
                attachPosition,
                accumulator.Normal,
                now,
                now + ResolvePendingLifetime(accumulator.Kind));
            PublishAccumulator(accumulator);
            var interactionAudio = shooter.GetComponent<
                PHSNetworkItemInteractionAudioRelay>();
            interactionAudio?.TryBroadcastConfirmedServer(
                NetworkAudioCue.FoamAttach,
                blob.ShotSequence);

            if (accumulator.Current < accumulator.Required)
            {
                return;
            }

            if (accumulator.Kind == NetworkFoamTargetKind.Surface)
            {
                var wasHardened = accumulator.State
                    == NetworkFoamTargetState.Hardened;
                accumulator.State = NetworkFoamTargetState.Hardened;
                HardenAccumulator(accumulator, now, now + surfaceLifetime);
                PublishAccumulator(accumulator);
                if (!wasHardened)
                {
                    interactionAudio?.TryBroadcastConfirmedServer(
                        NetworkAudioCue.FoamHarden,
                        blob.ShotSequence);
                    Debug.Log(
                        $"PHS_FOAM_TARGET_HARDENED kind=Surface target={accumulator.Key} blobs={accumulator.Current}/{accumulator.Required}",
                        this);
                }

                return;
            }

            if (accumulator.Target is not IUtilityAttackTarget utilityTarget
                || !TryResolveFoamConsumption(
                    shooter,
                    out var itemRecord,
                    out var expectedRevision)
                || !utilityTarget.TryResolveUtilityAttack(
                    new UtilityAttackHit(
                        FoamItemId,
                        shooter.gameObject,
                        blob.ShotSequence)))
            {
                Debug.LogWarning(
                    $"PHS_FOAM_THRESHOLD_REJECTED target={accumulator.Key} kind={accumulator.Kind}",
                    this);
                BeginDissolveAccumulator(accumulator, now);
                return;
            }

            if (!itemRecord.TryConsumeHeldItemServer(
                    FoamItemId,
                    expectedRevision))
            {
                Debug.LogError(
                    $"PHS_FOAM_TRANSACTION_FAILED reason=item_consume_failed target={accumulator.Key} owner={shooter.OwnerClientId}",
                    this);
                BeginDissolveAccumulator(accumulator, now);
                return;
            }

            accumulator.State = NetworkFoamTargetState.Completed;
            accumulator.DissolveAt = now + completionHoldSeconds;
            accumulator.RemoveAt = accumulator.DissolveAt + dissolveSeconds;
            HardenAccumulator(accumulator, now, accumulator.RemoveAt);
            PublishAccumulator(accumulator);
            PublishCompletionFeedback(shooter, accumulator);
            interactionAudio?.TryBroadcastConfirmedServer(
                accumulator.Kind == NetworkFoamTargetKind.Fire
                    ? NetworkAudioCue.FoamFireComplete
                    : NetworkAudioCue.FoamSealComplete,
                blob.ShotSequence);
            Debug.Log(
                $"PHS_FOAM_TARGET_COMPLETED kind={accumulator.Kind} target={accumulator.Key} blobs={accumulator.Current}/{accumulator.Required}",
                this);
        }

        private static bool TryResolveFoamConsumption(
            NetworkObject shooter,
            out NetworkPlayerItemRecord itemRecord,
            out uint expectedRevision)
        {
            itemRecord = shooter == null
                ? null
                : shooter.GetComponent<NetworkPlayerItemRecord>();
            expectedRevision = itemRecord == null ? 0U : itemRecord.Revision;
            return itemRecord != null
                && itemRecord.IsSpawned
                && itemRecord.IsServer
                && itemRecord.HeldItemId == FoamItemId;
        }

        internal void ReleaseFlyingBlobServer(
            PHSNetworkFoamBlob blob,
            string reason)
        {
            if (!IsServer || blob == null)
            {
                return;
            }

            ReleaseBlobServer(blob);
            Debug.Log(
                $"PHS_FOAM_BLOB_RELEASED reason={reason}",
                this);
        }

        private bool TryResolveAccumulator(
            RaycastHit hit,
            double now,
            out FoamAccumulator accumulator,
            out Vector3 attachPosition)
        {
            var fireTarget = hit.collider
                .GetComponentInParent<PHSFirePatchRuntimeTarget>();
            if (fireTarget != null
                && fireTarget.IsActive
                && fireTarget.Patch != null)
            {
                var locationHash = Animator.StringToHash(
                    fireTarget.LocationId ?? string.Empty);
                var key =
                    $"f:{fireTarget.AccidentInstanceId}:{fireTarget.Patch.PatchId}:{locationHash}";
                accumulator = GetOrCreateAccumulator(
                    key,
                    NetworkFoamTargetKind.Fire,
                    FireBlobThreshold,
                    hit.point,
                    hit.normal,
                    fireTarget,
                    hit.collider.GetEntityId(),
                    now);
                if (accumulator.State != NetworkFoamTargetState.Accumulating)
                {
                    attachPosition = default;
                    return false;
                }

                attachPosition = hit.point + ResolveNormal(hit.normal) * 0.025f;
                return true;
            }

            var hullTarget = hit.collider
                .GetComponentInParent<PHSShipAccidentAnchor>();
            if (hullTarget != null
                && hullTarget.AccidentId == PHSShipAccidentId.HullBreach
                && !hullTarget.IsRepairComplete
                && Vector3.Distance(hit.point, hullTarget.RepairPosition)
                    <= hullCaptureRadius)
            {
                var key = $"h:{hullTarget.AccidentInstanceId}";
                accumulator = GetOrCreateAccumulator(
                    key,
                    NetworkFoamTargetKind.HullBreach,
                    HullBreachBlobThreshold,
                    hullTarget.RepairPosition,
                    hit.normal,
                    hullTarget,
                    hit.collider.GetEntityId(),
                    now);
                if (accumulator.State != NetworkFoamTargetState.Accumulating)
                {
                    attachPosition = default;
                    return false;
                }

                attachPosition = ResolveHullBlobPosition(
                    hullTarget.RepairPosition,
                    hit.normal,
                    accumulator.Current,
                    HullBreachBlobThreshold);
                return true;
            }

            accumulator = FindSurfaceAccumulator(
                hit.collider.GetEntityId(),
                hit.point,
                hit.normal);
            if (accumulator == null)
            {
                nextSurfaceClusterId++;
                if (nextSurfaceClusterId == 0U)
                {
                    nextSurfaceClusterId = 1U;
                }

                var key = $"s:{nextSurfaceClusterId}";
                accumulator = GetOrCreateAccumulator(
                    key,
                    NetworkFoamTargetKind.Surface,
                    SurfaceBlobThreshold,
                    hit.point,
                    hit.normal,
                    null,
                    hit.collider.GetEntityId(),
                    now);
            }

            attachPosition = hit.point + ResolveNormal(hit.normal) * 0.025f;
            return true;
        }

        private FoamAccumulator GetOrCreateAccumulator(
            string key,
            NetworkFoamTargetKind kind,
            int required,
            Vector3 center,
            Vector3 normal,
            Component target,
            EntityId surfaceEntityId,
            double now)
        {
            if (accumulators.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var accumulator = new FoamAccumulator
            {
                Key = key,
                Kind = kind,
                State = NetworkFoamTargetState.Accumulating,
                Required = required,
                Center = center,
                Normal = ResolveNormal(normal),
                Target = target,
                SurfaceEntityId = surfaceEntityId,
                ClusterId = NextClusterId(kind),
                LastHitAt = now,
                Revision = 1U
            };
            accumulators.Add(key, accumulator);
            return accumulator;
        }

        private FoamAccumulator FindSurfaceAccumulator(
            EntityId surfaceEntityId,
            Vector3 position,
            Vector3 normal)
        {
            var resolvedNormal = ResolveNormal(normal);
            foreach (var candidate in accumulators.Values)
            {
                if (candidate.Kind != NetworkFoamTargetKind.Surface
                    || candidate.State == NetworkFoamTargetState.Dissolving
                    || candidate.SurfaceEntityId != surfaceEntityId
                    || Vector3.Distance(candidate.Center, position)
                        > surfaceClusterRadius
                    || Vector3.Dot(candidate.Normal, resolvedNormal) < 0.65f)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private uint NextClusterId(NetworkFoamTargetKind kind)
        {
            if (kind == NetworkFoamTargetKind.Surface)
            {
                return nextSurfaceClusterId;
            }

            nextSurfaceClusterId++;
            if (nextSurfaceClusterId == 0U)
            {
                nextSurfaceClusterId = 1U;
            }

            return nextSurfaceClusterId;
        }

        private void UpdateServerCleanup(double now)
        {
            cleanupKeys.Clear();
            cleanupKeys.AddRange(accumulators.Keys);
            foreach (var key in cleanupKeys)
            {
                if (!accumulators.TryGetValue(key, out var accumulator))
                {
                    continue;
                }

                if (accumulator.State == NetworkFoamTargetState.Dissolving)
                {
                    if (now >= accumulator.RemoveAt)
                    {
                        RemoveAccumulator(accumulator);
                    }

                    continue;
                }

                if (accumulator.State == NetworkFoamTargetState.Completed)
                {
                    if (now >= accumulator.DissolveAt)
                    {
                        BeginDissolveAccumulator(accumulator, now);
                    }

                    continue;
                }

                var lifetime = accumulator.Kind == NetworkFoamTargetKind.Surface
                    ? surfaceLifetime
                    : pendingTargetLifetime;
                if (now - accumulator.LastHitAt >= lifetime)
                {
                    BeginDissolveAccumulator(accumulator, now);
                }
            }
        }

        private void BeginDissolveAccumulator(
            FoamAccumulator accumulator,
            double now)
        {
            accumulator.State = NetworkFoamTargetState.Dissolving;
            accumulator.DissolveAt = now;
            accumulator.RemoveAt = now + dissolveSeconds;
            foreach (var blobId in accumulator.BlobIds)
            {
                if (activeBlobs.TryGetValue(blobId, out var blob)
                    && blob != null)
                {
                    blob.BeginDissolveServer(now, accumulator.RemoveAt);
                }
            }

            PublishAccumulator(accumulator);
        }

        private void HardenAccumulator(
            FoamAccumulator accumulator,
            double now,
            double expireAt)
        {
            foreach (var blobId in accumulator.BlobIds)
            {
                if (activeBlobs.TryGetValue(blobId, out var blob)
                    && blob != null)
                {
                    blob.HardenServer(now, expireAt);
                }
            }
        }

        private void RemoveAccumulator(FoamAccumulator accumulator)
        {
            foreach (var blobId in accumulator.BlobIds)
            {
                if (activeBlobs.TryGetValue(blobId, out var blob)
                    && blob != null)
                {
                    ReleaseBlobServer(blob);
                }
            }

            var snapshotIndex = FindSnapshotIndex(accumulator.Key);
            if (snapshotIndex >= 0)
            {
                targetSnapshots.RemoveAt(snapshotIndex);
            }

            accumulators.Remove(accumulator.Key);
        }

        private void ReleaseBlobServer(PHSNetworkFoamBlob blob)
        {
            if (blob == null)
            {
                return;
            }

            var blobId = blob.NetworkObjectId;
            var ownerClientId = blob.ShooterClientId;
            activeBlobs.Remove(blobId);
            if (ownerBlobCounts.TryGetValue(ownerClientId, out var count))
            {
                count = Mathf.Max(0, count - 1);
                if (count == 0)
                {
                    ownerBlobCounts.Remove(ownerClientId);
                }
                else
                {
                    ownerBlobCounts[ownerClientId] = count;
                }
            }

            if (blob.NetworkObject != null && blob.NetworkObject.IsSpawned)
            {
                blob.NetworkObject.Despawn(true);
            }
        }

        private void PublishAccumulator(FoamAccumulator accumulator)
        {
            accumulator.Revision++;
            if (accumulator.Revision == 0U)
            {
                accumulator.Revision = 1U;
            }

            var snapshot = new NetworkFoamTargetSnapshot
            {
                TargetKey = new FixedString64Bytes(accumulator.Key),
                KindValue = (byte)accumulator.Kind,
                StateValue = (byte)accumulator.State,
                Current = (byte)Mathf.Clamp(
                    accumulator.Current,
                    0,
                    byte.MaxValue),
                Required = (byte)Mathf.Clamp(
                    accumulator.Required,
                    1,
                    byte.MaxValue),
                WorldPosition = accumulator.Center,
                SurfaceNormal = accumulator.Normal,
                Revision = accumulator.Revision
            };
            var index = FindSnapshotIndex(accumulator.Key);
            if (index >= 0)
            {
                targetSnapshots[index] = snapshot;
            }
            else
            {
                targetSnapshots.Add(snapshot);
            }
        }

        private int FindSnapshotIndex(string key)
        {
            for (var index = 0; index < targetSnapshots.Count; index++)
            {
                if (targetSnapshots[index].TargetKey.ToString() == key)
                {
                    return index;
                }
            }

            return -1;
        }

        private void PublishCompletionFeedback(
            NetworkObject shooter,
            FoamAccumulator accumulator)
        {
            var feedback = shooter
                .GetComponent<PHSNetworkItemUseFeedbackController>();
            if (feedback == null)
            {
                Debug.LogError(
                    $"PHS_FOAM_COMPLETION_FEEDBACK_FAILED reason=controller_missing shooter={shooter.name}",
                    this);
                return;
            }

            feedback.PublishServerFeedback(
                PHSItemUseFeedbackKind.FireExtinguisher,
                PHSItemUseFeedbackShape.Sphere,
                accumulator.Center,
                accumulator.Normal,
                accumulator.Kind == NetworkFoamTargetKind.HullBreach
                    ? 0.65f
                    : 0.5f,
                0f,
                new[] { accumulator.Center });
        }

        private bool TryResolveShooter(
            PHSNetworkFoamBlob blob,
            out NetworkObject shooter)
        {
            shooter = null;
            return NetworkManager != null
                && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    blob.ShooterNetworkObjectId,
                    out shooter)
                && shooter != null
                && shooter.OwnerClientId == blob.ShooterClientId;
        }

        private float ResolvePendingLifetime(NetworkFoamTargetKind kind)
        {
            return kind == NetworkFoamTargetKind.Surface
                ? surfaceLifetime
                : pendingTargetLifetime;
        }

        private bool ValidateSetup(out string reason)
        {
            if (!HasRequiredReferences)
            {
                reason = "blob_prefab_missing";
                return false;
            }

            if (hitLayers.value == 0
                || projectileSpeed <= 0f
                || maximumRange <= 0f
                || collisionRadius <= 0f
                || maximumBlobsPerOwner <= 0
                || maximumBlobsGlobal < maximumBlobsPerOwner
                || pendingTargetLifetime <= 0f
                || surfaceLifetime <= 0f
                || completionHoldSeconds < 0f
                || dissolveSeconds <= 0f
                || hullCaptureRadius <= 0f
                || surfaceClusterRadius <= 0f)
            {
                reason = "configuration_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private static Vector3 ResolveHullBlobPosition(
            Vector3 center,
            Vector3 normal,
            int currentCount,
            int requiredCount)
        {
            var resolvedNormal = ResolveNormal(normal);
            var tangent = Vector3.Cross(resolvedNormal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(resolvedNormal, Vector3.right);
            }

            tangent.Normalize();
            var bitangent = Vector3.Cross(resolvedNormal, tangent).normalized;
            var angle = currentCount * (Mathf.PI * 2f / requiredCount);
            var radius = currentCount == 0 ? 0f : 0.16f;
            return center
                + tangent * (Mathf.Cos(angle) * radius)
                + bitangent * (Mathf.Sin(angle) * radius)
                + resolvedNormal * 0.025f;
        }

        private static Vector3 BlendNormal(
            Vector3 current,
            Vector3 next,
            int count)
        {
            var blended = ResolveNormal(current) * Mathf.Max(0, count - 1)
                + ResolveNormal(next);
            return ResolveNormal(blended);
        }

        private static Vector3 ResolveNormal(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.001f
                ? normal.normalized
                : Vector3.up;
        }

        private static bool IsSupportedTargetTrigger(Collider collider)
        {
            var fireTarget = collider.GetComponentInParent<
                PHSFirePatchRuntimeTarget>();
            if (fireTarget != null && fireTarget.IsActive)
            {
                return true;
            }

            var hullTarget = collider.GetComponentInParent<
                PHSShipAccidentAnchor>();
            return hullTarget != null
                && hullTarget.AccidentId == PHSShipAccidentId.HullBreach
                && !hullTarget.IsRepairComplete;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class FoamAccumulator
        {
            public string Key;
            public NetworkFoamTargetKind Kind;
            public NetworkFoamTargetState State;
            public int Current;
            public int Required;
            public Vector3 Center;
            public Vector3 Normal;
            public Component Target;
            public EntityId SurfaceEntityId;
            public uint ClusterId;
            public uint Revision;
            public double LastHitAt;
            public double DissolveAt;
            public double RemoveAt;
            public readonly List<ulong> BlobIds = new();
        }
    }
}
