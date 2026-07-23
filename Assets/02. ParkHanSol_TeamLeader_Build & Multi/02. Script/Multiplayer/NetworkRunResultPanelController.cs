using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkRunResultPanelController : NetworkBehaviour
    {
        public static bool IsLocalResultVisible { get; private set; }

        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private NetworkRunResultPanelView panelView;
        [SerializeField] private string lobbySceneName = "ParkHanSol_LobbyScene";

        private readonly INetworkSessionExitService sessionExitService =
            new NetworkSessionExitService();
        private NetworkRunFlowCoordinator runFlow;
        private NetworkRunEconomyLedger economy;
        private NetworkRunRestartCoordinator restart;
        private bool isRootAvailabilitySubscribed;
        private bool isShowing;
        private bool isExiting;
        private bool isRestarting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsLocalResultVisible = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                panelView?.SetVisible(false);
                return;
            }

            if (playerController == null
                || panelView == null
                || !panelView.HasRequiredReferences)
            {
                Debug.LogError(
                    $"PHS_RUN_RESULT_SETUP_FAILED reason=inspector_reference_missing player={name}",
                    this);
                return;
            }

            panelView.SetVisible(false);
            panelView.RestartRunButton.onClick.AddListener(RestartRun);
            panelView.ReturnToLobbyButton.onClick.AddListener(ReturnToLobby);
            SubscribeRootAvailability();
            if (NetworkRunSessionRoot.Instance != null)
            {
                BindRunSessionRoot(NetworkRunSessionRoot.Instance);
            }
        }

        public override void OnNetworkDespawn()
        {
            var shouldReturnToLobby = IsOwner
                && isShowing
                && !isExiting
                && !isRestarting
                && (restart == null || !restart.BlocksRun);
            UnbindRunSessionRoot();
            UnsubscribeRootAvailability();
            if (panelView != null && panelView.RestartRunButton != null)
            {
                panelView.RestartRunButton.onClick.RemoveListener(RestartRun);
            }

            if (panelView != null && panelView.ReturnToLobbyButton != null)
            {
                panelView.ReturnToLobbyButton.onClick.RemoveListener(ReturnToLobby);
            }

            base.OnNetworkDespawn();
            if (shouldReturnToLobby)
            {
                ReturnToLobby();
            }
        }

        private void OnDisable()
        {
            UnbindRunSessionRoot();
            UnsubscribeRootAvailability();
            if (IsOwner && playerController != null)
            {
                playerController.SetResultInputBlocked(false);
            }

            if (IsOwner)
            {
                IsLocalResultVisible = false;
            }
        }

        private void SubscribeRootAvailability()
        {
            if (isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable += HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = true;
        }

        private void UnsubscribeRootAvailability()
        {
            if (!isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable -= HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = false;
        }

        private void HandleRunSessionRootAvailable(
            NetworkRunSessionRoot runSessionRoot)
        {
            BindRunSessionRoot(runSessionRoot);
        }

        private void BindRunSessionRoot(NetworkRunSessionRoot runSessionRoot)
        {
            if (runSessionRoot == null
                || runSessionRoot.RunFlow == null
                || runSessionRoot.Economy == null
                || runSessionRoot.Restart == null)
            {
                Debug.LogError(
                    $"PHS_RUN_RESULT_BIND_FAILED reason=run_state_missing player={name}",
                    this);
                return;
            }

            if (runFlow != runSessionRoot.RunFlow)
            {
                UnbindRunSessionRoot();
                runFlow = runSessionRoot.RunFlow;
                economy = runSessionRoot.Economy;
                restart = runSessionRoot.Restart;
                runFlow.PhaseChanged += HandlePhaseChanged;
                restart.RestartStateChanged += HandleRestartStateChanged;
            }

            RefreshForPhase(runFlow.Phase);
        }

        private void UnbindRunSessionRoot()
        {
            if (runFlow != null)
            {
                runFlow.PhaseChanged -= HandlePhaseChanged;
            }

            if (restart != null)
            {
                restart.RestartStateChanged -= HandleRestartStateChanged;
            }

            runFlow = null;
            economy = null;
            restart = null;
        }

        private void HandlePhaseChanged(
            NetworkRunPhase previous,
            NetworkRunPhase current)
        {
            RefreshForPhase(current);
        }

        private void RefreshForPhase(NetworkRunPhase phase)
        {
            if (phase != NetworkRunPhase.Clear
                && phase != NetworkRunPhase.GameOver)
            {
                return;
            }

            if (runFlow == null || economy == null)
            {
                Debug.LogError(
                    $"PHS_RUN_RESULT_SHOW_FAILED reason=run_state_unbound player={name}",
                    this);
                return;
            }

            panelView.SetResult(
                phase,
                runFlow.ClearedZoneCount,
                runFlow.CompletedShopCycleCount,
                economy.Credits);
            panelView.SetVisible(true);
            isShowing = true;
            IsLocalResultVisible = true;
            RefreshRestartControls();
            playerController.SetResultInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestartRun()
        {
            if (!isShowing || isRestarting)
            {
                return;
            }

            if (!IsHost)
            {
                const string hostReason = "host_authority_required";
                panelView.SetRestartHostOnly();
                Debug.LogError($"PHS_RUN_RESULT_RESTART_REJECTED reason={hostReason}", this);
                return;
            }

            if (restart == null)
            {
                ShowRestartFailure("restart_service_missing");
                return;
            }

            if (!restart.CanRequestRestart(out var reason))
            {
                ShowRestartFailure(reason);
                return;
            }

            isRestarting = true;
            panelView.SetRestartPending();
            if (!restart.TryRequestRestart(out reason))
            {
                isRestarting = false;
                ShowRestartFailure(
                    string.IsNullOrWhiteSpace(restart.LastFailureReason)
                        ? reason
                        : restart.LastFailureReason);
            }
        }

        private void HandleRestartStateChanged(
            NetworkRunRestartState previous,
            NetworkRunRestartState current)
        {
            if (current == NetworkRunRestartState.LoadingScene
                || current == NetworkRunRestartState.Committing)
            {
                isRestarting = true;
                panelView.SetRestartPending();
                return;
            }

            if (current == NetworkRunRestartState.Failed)
            {
                isRestarting = false;
                ShowRestartFailure(
                    string.IsNullOrWhiteSpace(restart?.LastFailureReason)
                        ? "restart_state_failed"
                        : restart.LastFailureReason);
            }
        }

        private void RefreshRestartControls()
        {
            if (!IsHost)
            {
                panelView.SetRestartHostOnly();
                return;
            }

            if (restart == null)
            {
                ShowRestartFailure("restart_service_missing");
                return;
            }

            if (restart.RestartState == NetworkRunRestartState.Failed)
            {
                ShowRestartFailure(
                    string.IsNullOrWhiteSpace(restart.LastFailureReason)
                        ? "restart_state_failed"
                        : restart.LastFailureReason);
                return;
            }

            if (restart.IsRestartInProgress)
            {
                isRestarting = true;
                panelView.SetRestartPending();
                return;
            }

            if (!restart.CanRequestRestart(out var reason))
            {
                ShowRestartFailure(reason);
                return;
            }

            panelView.SetRestartReady();
        }

        private void ShowRestartFailure(string reason)
        {
            reason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
            panelView.SetRestartFailed(reason);
            Debug.LogError($"PHS_RUN_RESULT_RESTART_FAILED reason={reason}", this);
        }

        private async void ReturnToLobby()
        {
            if (isExiting)
            {
                return;
            }

            isExiting = true;
            panelView.ReturnToLobbyButton.interactable = false;
            if (!await sessionExitService.LeaveToLobbyAsync(lobbySceneName))
            {
                isExiting = false;
                if (panelView != null && panelView.ReturnToLobbyButton != null)
                {
                    panelView.ReturnToLobbyButton.interactable = true;
                }
            }
        }
    }
}
