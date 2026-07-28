using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkFoamBlob : NetworkBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private TrailRenderer flightTrail;
        [SerializeField] private Vector3 attachedScale =
            new(0.22f, 0.12f, 0.22f);
        [SerializeField, Min(0.01f)] private float hardenSeconds = 0.18f;

        private readonly NetworkVariable<NetworkFoamBlobSnapshot> snapshot =
            new(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private PHSNetworkFoamCoordinator serverOwner;
        private ulong shooterNetworkObjectId;
        private ulong shooterClientId;
        private uint shotSequence;
        private Vector3 lastServerPosition;
        private bool serverInitialized;

        public NetworkFoamBlobSnapshot Snapshot => snapshot.Value;
        public ulong ShooterNetworkObjectId => shooterNetworkObjectId;
        public ulong ShooterClientId => shooterClientId;
        public uint ShotSequence => shotSequence;
        public Transform VisualRoot => visualRoot;
        public TrailRenderer FlightTrail => flightTrail;
        public Vector3 AttachedScale => attachedScale;
        public float HardenSeconds => hardenSeconds;
        public bool HasRequiredReferences => visualRoot != null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            snapshot.OnValueChanged += HandleSnapshotChanged;
            ApplySnapshot(snapshot.Value);
        }

        public override void OnNetworkDespawn()
        {
            snapshot.OnValueChanged -= HandleSnapshotChanged;
            serverOwner = null;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer
                && serverInitialized
                && snapshot.Value.Phase == NetworkFoamBlobPhase.Flying)
            {
                UpdateServerFlight();
            }

            ApplySnapshot(snapshot.Value);
        }

        internal void InitializeServer(
            PHSNetworkFoamCoordinator owner,
            ulong shooterObjectId,
            ulong ownerClientId,
            uint sequence,
            Vector3 origin,
            Vector3 direction,
            float speed,
            double serverTime,
            double expireServerTime)
        {
            if (!IsServer || owner == null)
            {
                Debug.LogError(
                    $"PHS_FOAM_BLOB_SETUP_FAILED reason=server_contract blob={name}",
                    this);
                return;
            }

            serverOwner = owner;
            shooterNetworkObjectId = shooterObjectId;
            shooterClientId = ownerClientId;
            shotSequence = sequence;
            lastServerPosition = origin;
            serverInitialized = true;
            snapshot.Value = new NetworkFoamBlobSnapshot
            {
                PhaseValue = (byte)NetworkFoamBlobPhase.Flying,
                TargetKindValue = (byte)NetworkFoamTargetKind.Surface,
                ClusterId = 0U,
                LaunchOrigin = origin,
                LaunchDirection = direction.normalized,
                AttachedPosition = origin,
                AttachedNormal = Vector3.up,
                Speed = speed,
                LaunchServerTime = serverTime,
                PhaseServerTime = serverTime,
                ExpireServerTime = expireServerTime
            };
            transform.position = origin;
        }

        internal void AttachServer(
            NetworkFoamTargetKind targetKind,
            uint clusterId,
            Vector3 position,
            Vector3 normal,
            double serverTime,
            double expireServerTime)
        {
            if (!IsServer || !serverInitialized)
            {
                return;
            }

            var next = snapshot.Value;
            next.PhaseValue = (byte)NetworkFoamBlobPhase.Attached;
            next.TargetKindValue = (byte)targetKind;
            next.ClusterId = clusterId;
            next.AttachedPosition = position;
            next.AttachedNormal = ResolveNormal(normal);
            next.PhaseServerTime = serverTime;
            next.ExpireServerTime = expireServerTime;
            snapshot.Value = next;
        }

        internal void HardenServer(
            double serverTime,
            double expireServerTime)
        {
            SetPhaseServer(
                NetworkFoamBlobPhase.Hardened,
                serverTime,
                expireServerTime);
        }

        internal void BeginDissolveServer(
            double serverTime,
            double expireServerTime)
        {
            SetPhaseServer(
                NetworkFoamBlobPhase.Dissolving,
                serverTime,
                expireServerTime);
        }

        private void SetPhaseServer(
            NetworkFoamBlobPhase phase,
            double serverTime,
            double expireServerTime)
        {
            if (!IsServer || !serverInitialized)
            {
                return;
            }

            var next = snapshot.Value;
            next.PhaseValue = (byte)phase;
            next.PhaseServerTime = serverTime;
            next.ExpireServerTime = expireServerTime;
            snapshot.Value = next;
        }

        private void UpdateServerFlight()
        {
            var current = snapshot.Value;
            var serverTime = NetworkManager.ServerTime.Time;
            if (serverTime >= current.ExpireServerTime)
            {
                serverOwner.ReleaseFlyingBlobServer(this, "flight_timeout");
                return;
            }

            var elapsed = Mathf.Max(
                0f,
                (float)(serverTime - current.LaunchServerTime));
            var nextPosition = current.LaunchOrigin
                + current.LaunchDirection * (current.Speed * elapsed);
            var displacement = nextPosition - lastServerPosition;
            var distance = displacement.magnitude;
            if (distance > 0.0001f)
            {
                var hasImpact = serverOwner.TryResolveFirstImpactServer(
                    this,
                    lastServerPosition,
                    displacement / distance,
                    distance,
                    out var hit,
                    out var shotTerminated);
                if (shotTerminated)
                {
                    return;
                }

                if (hasImpact)
                {
                    serverOwner.HandleImpactServer(this, hit);
                    return;
                }
            }

            lastServerPosition = nextPosition;
            transform.position = nextPosition;
        }

        private void HandleSnapshotChanged(
            NetworkFoamBlobSnapshot previous,
            NetworkFoamBlobSnapshot current)
        {
            ApplySnapshot(current);
        }

        private void ApplySnapshot(NetworkFoamBlobSnapshot current)
        {
            if (visualRoot == null)
            {
                return;
            }

            if (!System.Enum.IsDefined(
                    typeof(NetworkFoamBlobPhase),
                    current.PhaseValue))
            {
                visualRoot.gameObject.SetActive(false);
                if (flightTrail != null)
                {
                    flightTrail.emitting = false;
                }

                return;
            }

            if (!visualRoot.gameObject.activeSelf)
            {
                visualRoot.gameObject.SetActive(true);
            }

            var serverTime = NetworkManager == null
                ? Time.timeAsDouble
                : NetworkManager.ServerTime.Time;
            if (current.Phase == NetworkFoamBlobPhase.Flying)
            {
                var elapsed = Mathf.Max(
                    0f,
                    (float)(serverTime - current.LaunchServerTime));
                transform.position = current.LaunchOrigin
                    + current.LaunchDirection * (current.Speed * elapsed);
                transform.rotation = Quaternion.LookRotation(
                    current.LaunchDirection,
                    Vector3.up);
                visualRoot.localScale = attachedScale * 0.35f;
                if (flightTrail != null)
                {
                    flightTrail.emitting = true;
                }

                return;
            }

            transform.position = current.AttachedPosition;
            transform.rotation = Quaternion.FromToRotation(
                Vector3.up,
                ResolveNormal(current.AttachedNormal));
            if (flightTrail != null)
            {
                flightTrail.emitting = false;
            }

            var phaseElapsed = Mathf.Max(
                0f,
                (float)(serverTime - current.PhaseServerTime));
            var scaleMultiplier = current.Phase switch
            {
                NetworkFoamBlobPhase.Attached => Mathf.Lerp(
                    0.35f,
                    1f,
                    Mathf.Clamp01(phaseElapsed / hardenSeconds)),
                NetworkFoamBlobPhase.Hardened => 1.15f,
                NetworkFoamBlobPhase.Dissolving => Mathf.Clamp01(
                    (float)(current.ExpireServerTime - serverTime)
                    / Mathf.Max(
                        0.01f,
                        (float)(current.ExpireServerTime
                            - current.PhaseServerTime))),
                _ => 1f
            };
            visualRoot.localScale = attachedScale * scaleMultiplier;
        }

        private static Vector3 ResolveNormal(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.001f
                ? normal.normalized
                : Vector3.up;
        }
    }
}
