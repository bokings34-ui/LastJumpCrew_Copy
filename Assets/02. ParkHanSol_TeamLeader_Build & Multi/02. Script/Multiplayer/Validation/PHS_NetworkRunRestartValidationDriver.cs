using System;
using System.Collections;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DisallowMultipleComponent]
    public sealed class PHS_NetworkRunRestartValidationDriver : MonoBehaviour
    {
        private const string ScenarioFlag = "-phsNetworkRunRestartValidation";
        private const string LobbySceneName = "ParkHanSol_LobbyScene";
        private const float SetupTimeoutSeconds = 30f;
        private const float RestartTimeoutSeconds = 120f;
        private static PHS_NetworkRunRestartValidationDriver instance;
        private ValidationScenario scenario;
        private bool failed;

        private enum ValidationScenario
        {
            None = 0,
            Success = 1,
            ExpectFailure = 2,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!TryReadScenario(out var selectedScenario, out var reason))
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.LogError(
                        $"PHS_NETWORK_RUN_RESTART_VALIDATION FAIL step=argument reason={reason}");
                }

                return;
            }

            if (instance != null)
            {
                return;
            }

            var driverObject = new GameObject(nameof(PHS_NetworkRunRestartValidationDriver));
            DontDestroyOnLoad(driverObject);
            instance = driverObject.AddComponent<PHS_NetworkRunRestartValidationDriver>();
            instance.scenario = selectedScenario;
        }

        private IEnumerator Start()
        {
            Debug.Log(
                $"PHS_NETWORK_RUN_RESTART_VALIDATION START scenario={scenario}",
                this);

            // Service authentication and Relay allocation happen before NGO starts listening.
            // The restart contract timeout begins only after the network session exists.
            yield return new WaitUntil(
                () => NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsListening);

            NetworkManager networkManager = null;
            NetworkRunSessionRoot initialRoot = null;
            yield return WaitForCondition(
                () =>
                {
                    networkManager = NetworkManager.Singleton;
                    initialRoot = NetworkRunSessionRoot.Instance;
                    return networkManager != null
                        && networkManager.IsListening
                        && networkManager.ConnectedClients.Count >= 2
                        && networkManager.LocalClient?.PlayerObject != null
                        && initialRoot != null
                        && initialRoot.Restart != null
                        && initialRoot.RunFlow != null;
                },
                "network_session_ready",
                SetupTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            yield return WaitForCondition(
                () => networkManager.IsHost
                    ? initialRoot.RunFlow.Phase == NetworkRunPhase.Charging
                    : initialRoot.RunFlow.Phase == NetworkRunPhase.Charging
                        || initialRoot.RunFlow.Phase == NetworkRunPhase.GameOver
                        || initialRoot.Restart.RestartEpoch > 0,
                "initial_gameplay_started",
                SetupTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            var oldPlayer = networkManager.LocalClient.PlayerObject;
            var oldPlayerId = oldPlayer.NetworkObjectId;
            var initialEpoch = initialRoot.Restart.RestartEpoch;

            if (networkManager.IsHost)
            {
                yield return ValidateHostAndStartRestart(initialRoot);
            }
            else
            {
                yield return ValidateClientAuthority(initialRoot.Restart);
            }

            if (failed)
            {
                yield break;
            }

            yield return ObserveRestartOutcome(
                networkManager,
                oldPlayer,
                oldPlayerId,
                initialEpoch);
            if (!failed)
            {
                Debug.Log(
                    $"PHS_NETWORK_RUN_RESTART_VALIDATION COMPLETE result=PASS " +
                    $"scenario={scenario} role={(networkManager.IsHost ? "host" : "client")}",
                    this);
            }
        }

        private IEnumerator ValidateHostAndStartRestart(NetworkRunSessionRoot root)
        {
            INetworkRunRestartService service = root.Restart;
            if (root.RunFlow.Phase == NetworkRunPhase.Clear
                || root.RunFlow.Phase == NetworkRunPhase.GameOver)
            {
                Fail("host_terminal_only_precondition", $"phase={root.RunFlow.Phase}");
                yield break;
            }

            var accepted = service.CanRequestRestart(out var reason);
            Require(
                !accepted
                    && reason.StartsWith("terminal_phase_required:", StringComparison.Ordinal),
                "host_terminal_only",
                $"accepted={accepted} reason={reason}");
            if (failed)
            {
                yield break;
            }

            if (GameCore.Instance?.Commands == null)
            {
                Fail("force_terminal", "game_commands_missing");
                yield break;
            }

            GameCore.Instance.Commands.ReportGameOver(GameOverReason.CrewWipedOut);
            yield return WaitForCondition(
                () => root.RunFlow.Phase == NetworkRunPhase.GameOver,
                "terminal_phase_synchronized",
                SetupTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            yield return WaitForCondition(
                () => NetworkRunResultPanelController.IsLocalResultVisible,
                "old_player_result_visible",
                SetupTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            accepted = service.CanRequestRestart(out reason);
            Require(
                accepted,
                "host_terminal_restart_allowed",
                $"reason={reason}");
            if (failed)
            {
                yield break;
            }

            accepted = service.TryRequestRestart(out reason);
            Require(
                accepted,
                "host_restart_started",
                $"reason={reason}");
            if (failed)
            {
                yield break;
            }

            var duplicateAccepted = service.TryRequestRestart(out var duplicateReason);
            Require(
                !duplicateAccepted
                    && duplicateReason.StartsWith(
                        "restart_unavailable:",
                        StringComparison.Ordinal),
                "duplicate_pending_blocked",
                $"accepted={duplicateAccepted} reason={duplicateReason}");
        }

        private IEnumerator ValidateClientAuthority(INetworkRunRestartService service)
        {
            var accepted = service.CanRequestRestart(out var reason);
            Require(
                !accepted && reason == "host_authority_required",
                "client_can_request_blocked",
                $"accepted={accepted} reason={reason}");
            if (failed)
            {
                yield break;
            }

            accepted = service.TryRequestRestart(out reason);
            Require(
                !accepted && reason == "host_authority_required",
                "client_restart_blocked",
                $"accepted={accepted} reason={reason}");
            yield break;
        }

        private IEnumerator ObserveRestartOutcome(
            NetworkManager networkManager,
            NetworkObject oldPlayer,
            ulong oldPlayerId,
            uint initialEpoch)
        {
            yield return WaitForCondition(
                () =>
                {
                    var root = NetworkRunSessionRoot.Instance;
                    return root != null
                        && root.Restart != null
                        && root.Restart.RestartEpoch != initialEpoch;
                },
                "restart_epoch_advanced",
                RestartTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            var targetEpoch = NetworkRunSessionRoot.Instance.Restart.RestartEpoch;
            yield return WaitForCondition(
                () =>
                {
                    var root = NetworkRunSessionRoot.Instance;
                    return root != null
                        && root.Restart != null
                        && (networkManager.IsHost
                            ? root.Restart.RestartEpoch == targetEpoch
                                && (root.Restart.RestartState == NetworkRunRestartState.Completed
                                || root.Restart.RestartState == NetworkRunRestartState.Failed
                                )
                            : networkManager.LocalClient?.PlayerObject != null
                                && networkManager.LocalClient.PlayerObject.NetworkObjectId != oldPlayerId);
                },
                "restart_terminal_state",
                RestartTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            var restart = NetworkRunSessionRoot.Instance.Restart;
            if (scenario == ValidationScenario.ExpectFailure)
            {
                Require(
                    restart.RestartState == NetworkRunRestartState.Failed,
                    "restart_failed_state",
                    $"state={restart.RestartState}");
                Require(
                    !string.IsNullOrWhiteSpace(restart.LastFailureReason),
                    "restart_failure_reason",
                    $"reason={restart.LastFailureReason}");
                yield break;
            }

            Require(
                networkManager.IsHost
                    ? restart.RestartState == NetworkRunRestartState.Completed
                    : networkManager.LocalClient?.PlayerObject != null
                        && networkManager.LocalClient.PlayerObject.NetworkObjectId != oldPlayerId,
                "restart_completed_state",
                $"state={restart.RestartState} reason={restart.LastFailureReason}");
            if (failed)
            {
                yield break;
            }

            yield return WaitForCondition(
                () => networkManager != null
                    && networkManager.IsListening
                    && networkManager.LocalClient?.PlayerObject != null
                    && networkManager.LocalClient.PlayerObject.NetworkObjectId != oldPlayerId,
                "fresh_player_spawned",
                SetupTimeoutSeconds);
            if (failed)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
            var newPlayer = networkManager.LocalClient.PlayerObject;
            Require(
                newPlayer.NetworkObjectId != oldPlayerId,
                "fresh_player_identity",
                $"old={oldPlayerId} new={newPlayer.NetworkObjectId}");
            Require(
                networkManager.IsListening
                    && SceneManager.GetActiveScene().name != LobbySceneName
                    && !NetworkRunResultPanelController.IsLocalResultVisible,
                "old_player_exit_suppressed",
                $"listening={networkManager.IsListening} " +
                $"scene={SceneManager.GetActiveScene().name} " +
                $"resultVisible={NetworkRunResultPanelController.IsLocalResultVisible}");
        }

        private IEnumerator WaitForCondition(
            Func<bool> predicate,
            string step,
            float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(step, $"timeout_seconds={timeoutSeconds}");
                    yield break;
                }

                yield return null;
            }

            Debug.Log(
                $"PHS_NETWORK_RUN_RESTART_VALIDATION PASS step={step}",
                this);
        }

        private void Require(bool condition, string step, string detail)
        {
            if (!condition)
            {
                Fail(step, detail);
                return;
            }

            Debug.Log(
                $"PHS_NETWORK_RUN_RESTART_VALIDATION PASS step={step} detail={detail}",
                this);
        }

        private void Fail(string step, string reason)
        {
            failed = true;
            Debug.LogError(
                $"PHS_NETWORK_RUN_RESTART_VALIDATION FAIL step={step} reason={reason}",
                this);
        }

        private static bool TryReadScenario(
            out ValidationScenario selectedScenario,
            out string reason)
        {
            selectedScenario = ValidationScenario.None;
            reason = null;
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                string value = null;
                if (string.Equals(arguments[index], ScenarioFlag, StringComparison.Ordinal))
                {
                    if (index + 1 >= arguments.Length)
                    {
                        reason = "scenario_value_missing";
                        return false;
                    }

                    value = arguments[index + 1];
                }
                else if (arguments[index].StartsWith(
                             $"{ScenarioFlag}=",
                             StringComparison.Ordinal))
                {
                    value = arguments[index].Substring(ScenarioFlag.Length + 1);
                }

                if (value == null)
                {
                    continue;
                }

                if (string.Equals(value, "success", StringComparison.OrdinalIgnoreCase))
                {
                    selectedScenario = ValidationScenario.Success;
                    return true;
                }

                if (string.Equals(
                        value,
                        "expect-failure",
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectedScenario = ValidationScenario.ExpectFailure;
                    return true;
                }

                reason = $"scenario_value_invalid:{value}";
                return false;
            }

            return false;
        }
    }
}
