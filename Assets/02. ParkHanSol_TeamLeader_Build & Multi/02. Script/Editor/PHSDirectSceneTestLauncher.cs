#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    [InitializeOnLoad]
    public static class PHSDirectSceneTestLauncher
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string FeatureInspectionScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity";
        private const string PlayMenuPath =
            "Tools/ParkHanSol/Scene Test/Play Current Scene As Local Host _F6";
        private const string MainLoopValidationMenuPath =
            "Tools/ParkHanSol/Scene Test/Run Main Loop Validation";
        private const string PendingTargetSceneKey = "PHS.DirectSceneTest.PendingTargetScene";
        private const string PendingEmbeddedPlayerKey =
            "PHS.DirectSceneTest.PendingEmbeddedPlayer";
        private const string PreviousStartSceneKey = "PHS.DirectSceneTest.PreviousStartScene";
        private const string PreviousStartSceneStoredKey = "PHS.DirectSceneTest.PreviousStartSceneStored";
        private const double LaunchTimeoutSeconds = 20d;

        private enum LaunchPhase
        {
            None,
            WaitingForLobbyBootstrap,
            WaitingForHost,
            WaitingForTargetScene
        }

        private static LaunchPhase launchPhase;
        private static string targetScenePath;
        private static bool targetHasEmbeddedPlayer;
        private static double launchDeadline;
        private static NetworkManager networkManager;

        static PHSDirectSceneTestLauncher()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.delayCall += ResumePendingLaunch;
        }

        [MenuItem(PlayMenuPath)]
        public static void PlayCurrentSceneAsLocalHost()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("PHS_DIRECT_SCENE_TEST_SKIPPED reason=play_mode_active");
                return;
            }

            var targetScene = SceneManager.GetActiveScene();
            if (!TryValidateTargetScene(targetScene, out var reason))
            {
                EditorUtility.DisplayDialog("PHS Scene Test", reason, "OK");
                return;
            }

            var lobbyScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyScenePath);
            if (lobbyScene == null)
            {
                EditorUtility.DisplayDialog(
                    "PHS Scene Test",
                    $"Lobby scene missing:\n{LobbyScenePath}",
                    "OK");
                return;
            }

            StorePreviousPlayModeStartScene();
            SessionState.SetString(PendingTargetSceneKey, targetScene.path);
            SessionState.SetBool(
                PendingEmbeddedPlayerKey,
                HasEmbeddedPlayer(targetScene));
            EditorSceneManager.playModeStartScene = lobbyScene;

            Debug.Log($"PHS_DIRECT_SCENE_TEST_QUEUED target={targetScene.path}");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(MainLoopValidationMenuPath)]
        public static void RunMainLoopValidation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "PHS_EDITOR_LOOP_VALIDATION_FAILED reason=play_mode_active");
                return;
            }

            var lobbyScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyScenePath);
            if (lobbyScene == null)
            {
                Debug.LogError(
                    $"PHS_EDITOR_LOOP_VALIDATION_FAILED reason=lobby_scene_missing path={LobbyScenePath}");
                return;
            }

            var openedScene = EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Single);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                Debug.LogError(
                    $"PHS_EDITOR_LOOP_VALIDATION_FAILED reason=lobby_scene_open_failed path={LobbyScenePath}");
                return;
            }

            StorePreviousPlayModeStartScene();
            EditorSceneManager.playModeStartScene = lobbyScene;
            Debug.Log(
                $"PHS_EDITOR_LOOP_VALIDATION_QUEUED scene={LobbyScenePath}");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(MainLoopValidationMenuPath, true)]
        private static bool ValidateRunMainLoopValidation()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(PlayMenuPath, true)]
        private static bool ValidatePlayCurrentSceneAsLocalHost()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            return scene.IsValid()
                && scene.isLoaded
                && !string.IsNullOrWhiteSpace(scene.path)
                && !string.Equals(scene.path, LobbyScenePath, StringComparison.Ordinal);
        }

        private static bool TryValidateTargetScene(Scene targetScene, out string reason)
        {
            if (!targetScene.IsValid() || !targetScene.isLoaded || string.IsNullOrWhiteSpace(targetScene.path))
            {
                reason = "Save target scene before direct test.";
                return false;
            }

            if (targetScene.isDirty)
            {
                reason = "Save current scene first. Direct test loads saved scene data.";
                return false;
            }

            if (string.Equals(targetScene.path, LobbyScenePath, StringComparison.Ordinal))
            {
                reason = "Lobby is bootstrap scene. Use normal Play for lobby test.";
                return false;
            }

            if (!EditorBuildSettings.scenes.Any(
                    scene => scene.enabled
                        && string.Equals(scene.path, targetScene.path, StringComparison.Ordinal)))
            {
                reason = $"Target scene is not enabled in Build Settings:\n{targetScene.path}";
                return false;
            }

            if (GameplaySceneContext.FindForScene(targetScene) == null)
            {
                reason = $"GameplaySceneContext missing in target scene:\n{targetScene.path}";
                return false;
            }

            if (UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include)
                .Any(manager => manager.gameObject.scene == targetScene))
            {
                reason = "Target already owns NetworkManager. Use normal Play for this scene.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    QueueFeatureInspectionAutoLaunch();
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    RestorePreviousPlayModeStartScene();
                    BeginPendingLaunch();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    StopLaunchTick();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    RestorePreviousPlayModeStartScene();
                    ClearPendingLaunch();
                    break;
            }
        }

        private static void QueueFeatureInspectionAutoLaunch()
        {
            if (!string.IsNullOrWhiteSpace(
                    SessionState.GetString(PendingTargetSceneKey, string.Empty)))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(
                    activeScene.path,
                    FeatureInspectionScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            var lobbyScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyScenePath);
            if (lobbyScene == null)
            {
                Debug.LogError(
                    $"PHS_FEATURE_INSPECTION_AUTO_BOOTSTRAP_FAILED reason=lobby_scene_missing path={LobbyScenePath}");
                return;
            }

            StorePreviousPlayModeStartScene();
            SessionState.SetString(PendingTargetSceneKey, activeScene.path);
            SessionState.SetBool(
                PendingEmbeddedPlayerKey,
                HasEmbeddedPlayer(activeScene));
            EditorSceneManager.playModeStartScene = lobbyScene;
            Debug.Log(
                $"PHS_FEATURE_INSPECTION_AUTO_BOOTSTRAP_QUEUED target={activeScene.path}");
        }

        private static void ResumePendingLaunch()
        {
            if (EditorApplication.isPlaying)
            {
                RestorePreviousPlayModeStartScene();
                BeginPendingLaunch();
            }
        }

        private static void BeginPendingLaunch()
        {
            if (launchPhase != LaunchPhase.None)
            {
                return;
            }

            targetScenePath = SessionState.GetString(PendingTargetSceneKey, string.Empty);
            if (string.IsNullOrWhiteSpace(targetScenePath))
            {
                return;
            }

            targetHasEmbeddedPlayer = SessionState.GetBool(
                PendingEmbeddedPlayerKey,
                false);

            StopLaunchTick();
            launchPhase = LaunchPhase.WaitingForLobbyBootstrap;
            launchDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;
            EditorApplication.update += TickLaunch;
        }

        private static void TickLaunch()
        {
            if (!EditorApplication.isPlaying)
            {
                StopLaunchTick();
                return;
            }

            if (EditorApplication.timeSinceStartup >= launchDeadline)
            {
                FailLaunch($"timeout phase={launchPhase} target={targetScenePath}");
                return;
            }

            switch (launchPhase)
            {
                case LaunchPhase.WaitingForLobbyBootstrap:
                    StartLocalHostWhenReady();
                    break;

                case LaunchPhase.WaitingForHost:
                    LoadTargetSceneWhenHostReady();
                    break;

                case LaunchPhase.WaitingForTargetScene:
                    CompleteWhenTargetSceneReady();
                    break;
            }
        }

        private static void StartLocalHostWhenReady()
        {
            networkManager = NetworkManager.Singleton;
            var gameCore = GameCore.Instance;
            if (networkManager == null || gameCore == null || gameCore.Services == null)
            {
                return;
            }

            if (networkManager.IsListening)
            {
                FailLaunch("network_manager_already_listening");
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval = false;
            if (!networkManager.StartHost())
            {
                FailLaunch("start_host_failed");
                return;
            }

            launchPhase = LaunchPhase.WaitingForHost;
            Debug.Log($"PHS_DIRECT_SCENE_TEST_HOST_STARTED target={targetScenePath}");
        }

        private static void LoadTargetSceneWhenHostReady()
        {
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsHost
                || !networkManager.ConnectedClients.ContainsKey(NetworkManager.ServerClientId)
                || networkManager.LocalClient == null
                || networkManager.LocalClient.PlayerObject == null)
            {
                return;
            }

            var gameCore = GameCore.Instance;
            var commands = gameCore == null ? null : gameCore.Commands;
            var state = gameCore == null ? null : gameCore.State;
            if (commands == null || state == null)
            {
                FailLaunch("economy_services_missing");
                return;
            }

            commands.StartGame();
            if (state.Phase != GamePhase.ZoneSelect)
            {
                FailLaunch($"game_start_failed phase={state.Phase}");
                return;
            }

            if (targetHasEmbeddedPlayer)
            {
                var bootstrapPlayer = networkManager.LocalClient.PlayerObject;
                bootstrapPlayer.Despawn(true);
                Debug.Log(
                    $"PHS_DIRECT_SCENE_TEST_BOOTSTRAP_PLAYER_REMOVED target={targetScenePath}");
            }

            var status = networkManager.SceneManager.LoadScene(targetScenePath, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                FailLaunch($"scene_load_failed status={status} target={targetScenePath}");
                return;
            }

            launchPhase = LaunchPhase.WaitingForTargetScene;
            Debug.Log($"PHS_DIRECT_SCENE_TEST_LOAD_STARTED target={targetScenePath}");
        }

        private static void CompleteWhenTargetSceneReady()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, targetScenePath, StringComparison.Ordinal))
            {
                return;
            }

            var player = targetHasEmbeddedPlayer
                ? UnityEngine.Object.FindObjectsByType<NetworkPlayerController>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate.gameObject.scene == activeScene
                        && candidate.IsSpawned
                        && candidate.IsOwner)
                : networkManager?.LocalClient?.PlayerObject
                    ?.GetComponent<NetworkPlayerController>();
            var playerReady = player != null;
            if (!playerReady || GameplaySceneContext.FindForScene(activeScene) == null)
            {
                return;
            }

            Debug.Log(
                $"PHS_DIRECT_SCENE_TEST_READY target={targetScenePath} " +
                $"clientId={networkManager.LocalClientId} player={player.name}");
            ClearPendingLaunch();
            StopLaunchTick();
        }

        private static void FailLaunch(string reason)
        {
            Debug.LogError($"PHS_DIRECT_SCENE_TEST_FAILED reason={reason}");
            ClearPendingLaunch();
            StopLaunchTick();
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static void StopLaunchTick()
        {
            EditorApplication.update -= TickLaunch;
            launchPhase = LaunchPhase.None;
            networkManager = null;
        }

        private static void ClearPendingLaunch()
        {
            targetScenePath = string.Empty;
            targetHasEmbeddedPlayer = false;
            SessionState.SetString(PendingTargetSceneKey, string.Empty);
            SessionState.SetBool(PendingEmbeddedPlayerKey, false);
        }

        private static bool HasEmbeddedPlayer(Scene scene)
        {
            return UnityEngine.Object.FindObjectsByType<NetworkPlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Any(player => player.gameObject.scene == scene);
        }

        private static void StorePreviousPlayModeStartScene()
        {
            var previousScene = EditorSceneManager.playModeStartScene;
            SessionState.SetString(
                PreviousStartSceneKey,
                previousScene == null ? string.Empty : AssetDatabase.GetAssetPath(previousScene));
            SessionState.SetBool(PreviousStartSceneStoredKey, true);
        }

        private static void RestorePreviousPlayModeStartScene()
        {
            if (!SessionState.GetBool(PreviousStartSceneStoredKey, false))
            {
                return;
            }

            var previousPath = SessionState.GetString(PreviousStartSceneKey, string.Empty);
            EditorSceneManager.playModeStartScene = string.IsNullOrWhiteSpace(previousPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousPath);
            SessionState.SetString(PreviousStartSceneKey, string.Empty);
            SessionState.SetBool(PreviousStartSceneStoredKey, false);
        }
    }
}
#endif
