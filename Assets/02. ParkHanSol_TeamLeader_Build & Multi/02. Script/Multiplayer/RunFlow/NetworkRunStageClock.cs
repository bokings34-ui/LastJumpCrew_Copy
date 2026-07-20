using System;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Server-authoritative stage clock. Only state transitions are replicated;
    /// running time is derived from the synchronized server deadline.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRunStageClock : NetworkBehaviour, IRunStageClock
    {
        private readonly NetworkVariable<NetworkRunStageClockSnapshot> synchronizedSnapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkRunStageClockSnapshot Snapshot => synchronizedSnapshot.Value;
        public int MapId => Snapshot.MapId;
        public uint StageSequence => Snapshot.StageSequence;
        public uint Revision => Snapshot.Revision;
        public NetworkRunStageClockState State => Snapshot.State;
        public float RemainingSeconds => CalculateRemainingSeconds(Snapshot);

        public event Action<
            NetworkRunStageClockSnapshot,
            NetworkRunStageClockSnapshot> SnapshotChanged;
        public event Action<NetworkRunStageClockSnapshot> ExpiredServer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_STAGE_CLOCK_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            synchronizedSnapshot.OnValueChanged += HandleSnapshotChanged;
            Debug.Log(
                $"PHS_STAGE_CLOCK_READY server={IsServer} state={State} sequence={StageSequence} revision={Revision}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            synchronizedSnapshot.OnValueChanged -= HandleSnapshotChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned
                || !IsServer
                || State != NetworkRunStageClockState.Running
                || RemainingSeconds > 0f)
            {
                return;
            }

            TryMarkExpiredServer(out _);
        }

        public bool TryStartServer(
            int mapId,
            float durationSeconds,
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

            if (float.IsNaN(durationSeconds)
                || float.IsInfinity(durationSeconds)
                || durationSeconds <= 0f)
            {
                reason = "positive_finite_duration_required";
                return false;
            }

            if (State == NetworkRunStageClockState.Running
                || State == NetworkRunStageClockState.Paused)
            {
                reason = $"clock_active:{State}";
                return false;
            }

            var current = Snapshot;
            var nextSequence = IncrementNonZero(current.StageSequence);
            synchronizedSnapshot.Value = new NetworkRunStageClockSnapshot(
                mapId,
                nextSequence,
                IncrementNonZero(current.Revision),
                NetworkRunStageClockState.Running,
                NetworkManager.ServerTime.Time + durationSeconds,
                durationSeconds);
            reason = null;
            Debug.Log(
                $"PHS_STAGE_CLOCK_STARTED map={mapId} sequence={nextSequence} duration={durationSeconds:0.###} revision={Revision}",
                this);
            return true;
        }

        public bool TryPauseServer(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (State != NetworkRunStageClockState.Running)
            {
                reason = $"clock_not_running:{State}";
                return false;
            }

            var remainingSeconds = RemainingSeconds;
            if (remainingSeconds <= 0f)
            {
                return TryMarkExpiredServer(out reason);
            }

            var current = Snapshot;
            synchronizedSnapshot.Value = new NetworkRunStageClockSnapshot(
                current.MapId,
                current.StageSequence,
                IncrementNonZero(current.Revision),
                NetworkRunStageClockState.Paused,
                0d,
                remainingSeconds);
            reason = null;
            Debug.Log(
                $"PHS_STAGE_CLOCK_PAUSED map={MapId} sequence={StageSequence} remaining={RemainingSeconds:0.###} revision={Revision}",
                this);
            return true;
        }

        public bool TryResumeServer(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (State != NetworkRunStageClockState.Paused)
            {
                reason = $"clock_not_paused:{State}";
                return false;
            }

            var current = Snapshot;
            if (current.FrozenRemainingSeconds <= 0f)
            {
                return TryMarkExpiredServer(out reason);
            }

            synchronizedSnapshot.Value = new NetworkRunStageClockSnapshot(
                current.MapId,
                current.StageSequence,
                IncrementNonZero(current.Revision),
                NetworkRunStageClockState.Running,
                NetworkManager.ServerTime.Time + current.FrozenRemainingSeconds,
                current.FrozenRemainingSeconds);
            reason = null;
            Debug.Log(
                $"PHS_STAGE_CLOCK_RESUMED map={MapId} sequence={StageSequence} remaining={RemainingSeconds:0.###} revision={Revision}",
                this);
            return true;
        }

        public bool TryStopServer(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (State == NetworkRunStageClockState.Stopped)
            {
                reason = "clock_already_stopped";
                return false;
            }

            if (State == NetworkRunStageClockState.Expired)
            {
                reason = "clock_already_expired";
                return false;
            }

            var current = Snapshot;
            synchronizedSnapshot.Value = new NetworkRunStageClockSnapshot(
                current.MapId,
                current.StageSequence,
                IncrementNonZero(current.Revision),
                NetworkRunStageClockState.Stopped,
                0d,
                0f);
            reason = null;
            Debug.Log(
                $"PHS_STAGE_CLOCK_STOPPED map={MapId} sequence={StageSequence} revision={Revision}",
                this);
            return true;
        }

        public bool TryMarkExpiredServer(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (State != NetworkRunStageClockState.Running
                && State != NetworkRunStageClockState.Paused)
            {
                reason = $"clock_not_active:{State}";
                return false;
            }

            var current = Snapshot;
            var expiredSnapshot = new NetworkRunStageClockSnapshot(
                current.MapId,
                current.StageSequence,
                IncrementNonZero(current.Revision),
                NetworkRunStageClockState.Expired,
                0d,
                0f);
            synchronizedSnapshot.Value = expiredSnapshot;
            reason = null;
            Debug.Log(
                $"PHS_STAGE_CLOCK_EXPIRED map={MapId} sequence={StageSequence} revision={Revision}",
                this);
            ExpiredServer?.Invoke(expiredSnapshot);
            return true;
        }

        private float CalculateRemainingSeconds(NetworkRunStageClockSnapshot snapshot)
        {
            if (snapshot.State == NetworkRunStageClockState.Paused)
            {
                return Mathf.Max(0f, snapshot.FrozenRemainingSeconds);
            }

            if (snapshot.State != NetworkRunStageClockState.Running)
            {
                return 0f;
            }

            if (NetworkManager == null || !NetworkManager.IsListening)
            {
                return Mathf.Max(0f, snapshot.FrozenRemainingSeconds);
            }

            return (float)Math.Max(
                0d,
                snapshot.DeadlineServerTime - NetworkManager.ServerTime.Time);
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

        private static uint IncrementNonZero(uint value)
        {
            value++;
            return value == 0U ? 1U : value;
        }

        private void HandleSnapshotChanged(
            NetworkRunStageClockSnapshot previousValue,
            NetworkRunStageClockSnapshot currentValue)
        {
            SnapshotChanged?.Invoke(previousValue, currentValue);
        }
    }
}
