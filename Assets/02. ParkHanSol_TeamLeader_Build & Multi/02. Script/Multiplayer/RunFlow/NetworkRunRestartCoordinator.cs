using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkRunFlowCoordinator))]
    public sealed class NetworkRunRestartCoordinator :
        NetworkBehaviour,
        INetworkRunRestartService
    {
        private readonly NetworkVariable<uint> synchronizedRestartEpoch = new(
            0U,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkRunRestartState> synchronizedRestartState = new(
            NetworkRunRestartState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> synchronizedFailureReason = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkRunFlowCoordinator runFlow;

        public static NetworkRunRestartCoordinator Instance { get; private set; }

        public uint RestartEpoch => synchronizedRestartEpoch.Value;
        public NetworkRunRestartState RestartState => synchronizedRestartState.Value;
        public string LastFailureReason => synchronizedFailureReason.Value.ToString();
        public bool IsRestartInProgress =>
            RestartState == NetworkRunRestartState.LoadingScene
            || RestartState == NetworkRunRestartState.Committing;
        public bool BlocksRun => IsRestartInProgress
            || RestartState == NetworkRunRestartState.Failed;

        public event Action<NetworkRunRestartState, NetworkRunRestartState> RestartStateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            runFlow = GetComponent<NetworkRunFlowCoordinator>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_RUN_RESTART_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_RUN_RESTART_SETUP_FAILED reason=duplicate_coordinator current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            synchronizedRestartState.OnValueChanged += HandleRestartStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            synchronizedRestartState.OnValueChanged -= HandleRestartStateChanged;
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        public bool CanRequestRestart(out string reason)
        {
            if (!IsSpawned || !IsServer || !IsHost)
            {
                reason = "host_authority_required";
                return false;
            }

            if (BlocksRun)
            {
                reason = $"restart_unavailable:{RestartState}";
                return false;
            }

            if (runFlow == null
                || (runFlow.Phase != NetworkRunPhase.Clear
                    && runFlow.Phase != NetworkRunPhase.GameOver))
            {
                reason = $"terminal_phase_required:{runFlow?.Phase.ToString() ?? "missing"}";
                return false;
            }

            var bootstrap = NetworkManager.GetComponent<NetworkRunSessionRootBootstrap>();
            if (bootstrap == null)
            {
                reason = "restart_bootstrap_missing";
                return false;
            }

            return bootstrap.CanBeginRestartServer(this, out reason);
        }

        public bool TryRequestRestart(out string reason)
        {
            if (!CanRequestRestart(out reason))
            {
                Debug.LogError($"PHS_RUN_RESTART_REQUEST_REJECTED reason={reason}", this);
                return false;
            }

            return NetworkManager
                .GetComponent<NetworkRunSessionRootBootstrap>()
                .TryBeginRestartServer(this, out reason);
        }

        internal void BeginLoadingServer(uint restartEpoch)
        {
            RequireServerStateMutation();
            synchronizedRestartEpoch.Value = restartEpoch;
            synchronizedFailureReason.Value = default;
            synchronizedRestartState.Value = NetworkRunRestartState.LoadingScene;
        }

        internal void BeginCommitServer(uint restartEpoch)
        {
            RequireServerStateMutation();
            synchronizedRestartEpoch.Value = restartEpoch;
            synchronizedRestartState.Value = NetworkRunRestartState.Committing;
        }

        internal void CompleteServer(uint restartEpoch)
        {
            RequireServerStateMutation();
            synchronizedRestartEpoch.Value = restartEpoch;
            synchronizedRestartState.Value = NetworkRunRestartState.Completed;
        }

        internal void FailServer(uint restartEpoch, string reason)
        {
            RequireServerStateMutation();
            synchronizedRestartEpoch.Value = restartEpoch;
            synchronizedFailureReason.Value = new FixedString128Bytes(reason ?? "unknown");
            synchronizedRestartState.Value = NetworkRunRestartState.Failed;
            Debug.LogError(
                $"PHS_RUN_RESTART_FAILED epoch={restartEpoch} reason={reason}",
                this);
        }

        private void RequireServerStateMutation()
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException(
                    "Restart state mutation requires a spawned server coordinator.");
            }
        }

        private void HandleRestartStateChanged(
            NetworkRunRestartState previousState,
            NetworkRunRestartState currentState)
        {
            RestartStateChanged?.Invoke(previousState, currentState);
        }
    }
}
