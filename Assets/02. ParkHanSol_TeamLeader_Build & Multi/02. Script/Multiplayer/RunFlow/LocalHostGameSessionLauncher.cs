using System.Collections;
using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.RunFlow
{
    [DisallowMultipleComponent]
    public sealed class LocalHostGameSessionLauncher :
        MonoBehaviour,
        ILocalGameSessionLauncher
    {
        [SerializeField] private Button singlePlayButton;
        [SerializeField] private string playSceneName = "PHS_Map_ver1";
        [SerializeField, Min(1f)] private float launchTimeoutSeconds = 15f;

        private Coroutine launchRoutine;

        public bool IsLaunching => launchRoutine != null;

        private void Awake()
        {
            if (singlePlayButton == null)
            {
                Debug.LogError(
                    $"PHS_SINGLE_PLAY_SETUP_FAILED reason=button_missing launcher={name}",
                    this);
                enabled = false;
                return;
            }

            singlePlayButton.onClick.AddListener(LaunchSinglePlayer);
        }

        private void OnDestroy()
        {
            if (singlePlayButton != null)
            {
                singlePlayButton.onClick.RemoveListener(LaunchSinglePlayer);
            }
        }

        public void LaunchSinglePlayer()
        {
            if (IsLaunching)
            {
                return;
            }

            launchRoutine = StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            singlePlayButton.interactable = false;
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Fail("network_manager_missing");
                yield break;
            }

            if (networkManager.IsListening)
            {
                Fail("network_manager_already_listening");
                yield break;
            }

            networkManager.NetworkConfig.ConnectionApproval = false;
            if (!networkManager.StartHost())
            {
                Fail("start_host_failed");
                yield break;
            }

            Debug.Log("PHS_SINGLE_PLAY_HOST_STARTED", this);
            var deadline = Time.realtimeSinceStartup + launchTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (networkManager.IsListening
                    && networkManager.IsHost
                    && networkManager.LocalClient != null
                    && networkManager.LocalClient.PlayerObject != null)
                {
                    break;
                }

                yield return null;
            }

            if (!networkManager.IsListening
                || !networkManager.IsHost
                || networkManager.LocalClient == null
                || networkManager.LocalClient.PlayerObject == null)
            {
                networkManager.Shutdown();
                Fail("local_player_spawn_timeout");
                yield break;
            }

            var customization = networkManager.LocalClient.PlayerObject
                .GetComponent<NetworkPlayerCustomization>();
            if (customization == null)
            {
                networkManager.Shutdown();
                Fail("customization_component_missing");
                yield break;
            }

            while (Time.realtimeSinceStartup < deadline
                   && !customization.IsProfileReady)
            {
                if (!string.IsNullOrWhiteSpace(
                        customization.ProfileFailureReason))
                {
                    networkManager.Shutdown();
                    Fail(
                        $"customization_profile_failed_{customization.ProfileFailureReason}");
                    yield break;
                }

                yield return null;
            }

            if (!customization.IsProfileReady)
            {
                networkManager.Shutdown();
                Fail("customization_profile_timeout");
                yield break;
            }

            Debug.Log("PHS_SINGLE_PLAY_CUSTOMIZATION_READY", this);
            var gameCore = GameCore.Instance;
            var commands = gameCore == null ? null : gameCore.Commands;
            var state = gameCore == null ? null : gameCore.State;
            if (commands == null || state == null)
            {
                networkManager.Shutdown();
                Fail("economy_services_missing");
                yield break;
            }

            commands.StartGame();
            if (state.Phase != GamePhase.ZoneSelect)
            {
                networkManager.Shutdown();
                Fail($"game_start_failed_{state.Phase}");
                yield break;
            }

            var status = networkManager.SceneManager.LoadScene(
                playSceneName,
                LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                networkManager.Shutdown();
                Fail($"scene_load_failed_{status}");
                yield break;
            }

            Debug.Log(
                $"PHS_SINGLE_PLAY_LOAD_STARTED scene={playSceneName}",
                this);
            launchRoutine = null;
        }

        private void Fail(string reason)
        {
            Debug.LogError(
                $"PHS_SINGLE_PLAY_FAILED reason={reason} launcher={name}",
                this);
            launchRoutine = null;
            if (singlePlayButton != null)
            {
                singlePlayButton.interactable = true;
            }
        }
    }
}
