using System;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGameOverSequenceCoordinator :
        NetworkBehaviour,
        IGameOverSequenceStatus
    {
        [Header("Sequence Durations")]
        [SerializeField, Min(0.1f)] private float timeOverDurationSeconds = 8.5f;
        [SerializeField, Min(0.1f)] private float shipDestroyedDurationSeconds = 5f;

        private readonly NetworkVariable<NetworkGameOverSequenceSnapshot> synchronizedSnapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkGameOverSequenceSnapshot Snapshot => synchronizedSnapshot.Value;
        public NetworkGameOverSequenceState State => Snapshot.State;
        public uint Revision => Snapshot.Revision;
        public bool IsPlaying => State == NetworkGameOverSequenceState.Playing;
        public bool IsCompleted => State == NetworkGameOverSequenceState.Completed;

        public event Action<
            NetworkGameOverSequenceSnapshot,
            NetworkGameOverSequenceSnapshot> SequenceChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_GAME_OVER_SEQUENCE_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            timeOverDurationSeconds = Mathf.Max(0.1f, timeOverDurationSeconds);
            shipDestroyedDurationSeconds = Mathf.Max(0.1f, shipDestroyedDurationSeconds);
            synchronizedSnapshot.OnValueChanged += HandleSnapshotChanged;
            Debug.Log(
                $"PHS_GAME_OVER_SEQUENCE_READY server={IsServer} state={State} revision={Revision}",
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
                || State != NetworkGameOverSequenceState.Playing
                || NetworkManager.ServerTime.Time < Snapshot.CompletesServerTime)
            {
                return;
            }

            CompleteServer();
        }

        public bool TryBeginServer(GameOverReason reason, out string failureReason)
        {
            if (!RequireServer(out failureReason))
            {
                return false;
            }

            if (reason == GameOverReason.None)
            {
                failureReason = "game_over_reason_required";
                return false;
            }

            if (State != NetworkGameOverSequenceState.Idle)
            {
                failureReason = $"sequence_already_started:{State}";
                return false;
            }

            var startedServerTime = NetworkManager.ServerTime.Time;
            var durationSeconds = reason == GameOverReason.TimeOver
                ? timeOverDurationSeconds
                : shipDestroyedDurationSeconds;
            synchronizedSnapshot.Value = new NetworkGameOverSequenceSnapshot(
                NetworkGameOverSequenceState.Playing,
                reason,
                IncrementNonZero(Revision),
                startedServerTime,
                startedServerTime + durationSeconds);
            failureReason = null;
            Debug.Log(
                $"PHS_GAME_OVER_SEQUENCE_STARTED reason={reason} revision={Revision} duration={durationSeconds:0.###}",
                this);
            return true;
        }

        public bool TryResetServer(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            synchronizedSnapshot.Value = default;
            reason = null;
            Debug.Log("PHS_GAME_OVER_SEQUENCE_RESET", this);
            return true;
        }

        private void CompleteServer()
        {
            var current = Snapshot;
            synchronizedSnapshot.Value = new NetworkGameOverSequenceSnapshot(
                NetworkGameOverSequenceState.Completed,
                current.Reason,
                current.Revision,
                current.StartedServerTime,
                current.CompletesServerTime);
            Debug.Log(
                $"PHS_GAME_OVER_SEQUENCE_COMPLETED reason={current.Reason} revision={current.Revision}",
                this);
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
            NetworkGameOverSequenceSnapshot previousValue,
            NetworkGameOverSequenceSnapshot currentValue)
        {
            SequenceChanged?.Invoke(previousValue, currentValue);
        }
    }
}
