using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkAudioWiringAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string PlayerPrefabPath =
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string TutorialPlayerPrefabPath =
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab";
        private const string ShopPrefabPath =
            Root + "/03. Prefab/Shop/PHS_NetworkShopCheckoutCounter.prefab";
        private const string RunRootPrefabPath =
            Root + "/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string ResultPrefabPath =
            Root + "/03. Prefab/UI/PHS_NetworkRunResultPanel.prefab";
        private const string TutorialScenePath =
            Root + "/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string DebrisSellPrefabPath =
            Root + "/03. Prefab/Props/Prefabs/ShopCheckoutCounter/PHS_DebrisSellStation.prefab";
        private const string PausePrefabPath =
            Root + "/03. Prefab/UI/PHS_NetworkOwnerPauseUI.prefab";
        private const string LobbyUiPrefabPath =
            Root + "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string MiniGamePrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/Integration0716/PHS_MiniGameRuntimeSystem.prefab";
        private const string GameplayScenePath =
            Root + "/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Audio Wiring")]
        public static void Author()
        {
            PHSCuratedAssetSfxAuthoring.Author();
            var clips = LoadRequiredClips();
            RequireNoDirtyLoadedScenes();
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                ConfigureResultPrefab(clips);
                ConfigurePlayerPrefab(PlayerPrefabPath, clips, false);
                ConfigurePlayerPrefab(TutorialPlayerPrefabPath, clips, true);
                ConfigureShopPrefab(clips);
                ConfigureRunRootPrefab(clips);
                ConfigureDebrisSellPrefab(clips);
                ConfigurePausePrefab(clips);
                ConfigureLobbySettingsPrefab(clips);
                ConfigureMiniGamePrefab(clips);
                ConfigureTutorialScene();
                ConfigureGameplayBackgroundScene();
                AssetDatabase.SaveAssets();
                Debug.Log("PHS_NETWORK_AUDIO_WIRING_COMPLETE");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Audio Prefabs Only")]
        public static void AuthorPrefabsOnly()
        {
            PHSCuratedAssetSfxAuthoring.Author();
            var clips = LoadRequiredClips();
            ConfigureResultPrefab(clips);
            ConfigurePlayerPrefab(PlayerPrefabPath, clips, false);
            ConfigurePlayerPrefab(TutorialPlayerPrefabPath, clips, true);
            ConfigureShopPrefab(clips);
            ConfigureRunRootPrefab(clips);
            ConfigureDebrisSellPrefab(clips);
            ConfigurePausePrefab(clips);
            ConfigureLobbySettingsPrefab(clips);
            ConfigureMiniGamePrefab(clips);
            AssetDatabase.SaveAssets();
            Debug.Log("PHS_NETWORK_AUDIO_PREFABS_ONLY_COMPLETE");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Requested SFX")]
        public static void AuthorRequestedSfx()
        {
            AuthorPrefabsOnly();
            ConfigureGameplayBackgroundScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PHS_REQUESTED_SFX_AUTHORING_COMPLETE");
        }

        private static Dictionary<NetworkAudioCue, AudioClip> LoadRequiredClips()
        {
            var clips = new Dictionary<NetworkAudioCue, AudioClip>
            {
                { NetworkAudioCue.ItemPickup, RequireClip(NetworkAudioCue.ItemPickup) },
                { NetworkAudioCue.ItemDrop, RequireClip(NetworkAudioCue.ItemDrop) },
                { NetworkAudioCue.ItemSwap, RequireClip(NetworkAudioCue.ItemSwap) },
                { NetworkAudioCue.ShopSuccess, RequireClip(NetworkAudioCue.ShopSuccess) },
                { NetworkAudioCue.ShopFailure, RequireClip(NetworkAudioCue.ShopFailure) },
                { NetworkAudioCue.Warning, RequireClip(NetworkAudioCue.Warning) },
                { NetworkAudioCue.RunClear, RequireClip(NetworkAudioCue.RunClear) },
                { NetworkAudioCue.RunGameOver, RequireClip(NetworkAudioCue.RunGameOver) },
                { NetworkAudioCue.RestartRequested, RequireClip(NetworkAudioCue.RestartRequested) },
                { NetworkAudioCue.RestartSucceeded, RequireClip(NetworkAudioCue.RestartSucceeded) },
                { NetworkAudioCue.RestartFailed, RequireClip(NetworkAudioCue.RestartFailed) },
                { NetworkAudioCue.TutorialComplete, RequireClip(NetworkAudioCue.TutorialComplete) },
                { NetworkAudioCue.DebrisDeposit, RequireClip(NetworkAudioCue.DebrisDeposit) },
                { NetworkAudioCue.FootstepWalk, RequireClip(NetworkAudioCue.FootstepWalk) },
                { NetworkAudioCue.FootstepRun, RequireClip(NetworkAudioCue.FootstepRun) },
                { NetworkAudioCue.PlayerJump, RequireClip(NetworkAudioCue.PlayerJump) },
                { NetworkAudioCue.MissionSuccess, RequireClip(NetworkAudioCue.MissionSuccess) },
                { NetworkAudioCue.VendingInteraction, RequireClip(NetworkAudioCue.VendingInteraction) },
                { NetworkAudioCue.InteractionFocus, RequireClip(NetworkAudioCue.InteractionFocus) },
                { NetworkAudioCue.OptionsSaved, RequireClip(NetworkAudioCue.OptionsSaved) },
                { NetworkAudioCue.WarpStart, RequireClip(NetworkAudioCue.WarpStart) },
                { NetworkAudioCue.WarpEnd, RequireClip(NetworkAudioCue.WarpEnd) },
                { NetworkAudioCue.AccidentAppeared, RequireClip(NetworkAudioCue.AccidentAppeared) },
            };

            return clips;
        }

        private static AudioClip RequireClip(NetworkAudioCue cue)
        {
            var path = PHSCuratedAssetSfxAuthoring.GetCuePath(cue);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=curated_clip_missing path={path}");
            }

            return clip;
        }

        private static void ConfigureResultPrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(ResultPrefabPath, root =>
            {
                var audioRoot = RequireNamedChild(root.transform, "PHS_NetworkRunResultAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, false, 25f);
                ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.RunClear, clips, 0.9f, 0.2f),
                    Binding(NetworkAudioCue.RunGameOver, clips, 0.9f, 0.2f),
                    Binding(NetworkAudioCue.RestartRequested, clips, 0.65f, 0.1f),
                    Binding(NetworkAudioCue.RestartSucceeded, clips, 0.8f, 0.2f),
                    Binding(NetworkAudioCue.RestartFailed, clips, 0.8f, 0.2f));
            });
        }

        private static void ConfigurePlayerPrefab(
            string path,
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips,
            bool includeTutorialCompletion)
        {
            EditPrefab(path, root =>
            {
                var record = RequireSingle<NetworkPlayerItemRecord>(root);
                var networkObject = RequireSingle<NetworkObject>(root);
                var playerController = RequireSingle<NetworkPlayerController>(root);
                var characterController = RequireSingle<CharacterController>(root);
                var playerControlInput = RequireSingle<PlayerControlInput>(root);
                var interactionScanner = RequireSingle<TempPlayerInteractionScanner>(root);
                var feedback = GetOrAddSingle<NetworkPlayerItemAudioFeedback>(root);
                var movementFeedback = GetOrAddSingle<NetworkPlayerMovementAudioFeedback>(root);

                var ownerRoot = RequireNamedChild(root.transform, "PHS_NetworkItemAudio_2D");
                var ownerSource = ConfigureAudioSource(ownerRoot.gameObject, false, 25f);
                var ownerEmitter = ConfigurePlayerOwnerEmitter(ownerRoot.gameObject, ownerSource, clips);

                var worldRoot = RequireNamedChild(root.transform, "PHS_NetworkItemAudio_3D");
                var worldSource = ConfigureAudioSource(worldRoot.gameObject, true, 20f);
                var worldEmitter = ConfigureItemEmitter(worldRoot.gameObject, worldSource, clips);

                SetObjectReference(feedback, "itemRecord", record);
                SetObjectReference(feedback, "networkObject", networkObject);
                SetObjectReference(feedback, "ownerCuePlayerSource", ownerEmitter);
                SetObjectReference(feedback, "worldCuePlayerSource", worldEmitter);
                SetObjectReference(movementFeedback, "networkObject", networkObject);
                SetObjectReference(movementFeedback, "playerController", playerController);
                SetObjectReference(movementFeedback, "characterController", characterController);
                SetObjectReference(movementFeedback, "playerControlInput", playerControlInput);
                SetObjectReference(movementFeedback, "ownerCuePlayerSource", ownerEmitter);
                SetObjectReference(movementFeedback, "worldCuePlayerSource", worldEmitter);
                SetObjectReference(interactionScanner, "interactionCuePlayerSource", ownerEmitter);

                foreach (var optionsPanel in root.GetComponentsInChildren<NetworkSharedOptionsPanelController>(true))
                {
                    SetObjectReference(optionsPanel, "saveCuePlayerSource", ownerEmitter);
                }

                if (includeTutorialCompletion)
                {
                    var completionRoot = RequireNamedChild(
                        root.transform,
                        "PHS_NetworkTutorialCompletionAudio");
                    var completionSource = ConfigureAudioSource(
                        completionRoot.gameObject,
                        false,
                        25f);
                    ConfigureEmitter(
                        completionRoot.gameObject,
                        completionSource,
                        Binding(NetworkAudioCue.TutorialComplete, clips, 0.85f, 0.2f));
                }

                var resultControllers = root.GetComponentsInChildren<NetworkRunResultPanelController>(true);
                foreach (var controller in resultControllers)
                {
                    var emitter = RequireNamedComponentInDescendants<NetworkAudioCueEmitter>(
                        controller.transform,
                        "PHS_NetworkRunResultAudio");
                    SetObjectReference(controller, "audioCuePlayerSource", emitter);
                }
            });
        }

        private static NetworkAudioCueEmitter ConfigureItemEmitter(
            GameObject gameObject,
            AudioSource source,
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            return ConfigureEmitter(
                gameObject,
                source,
                Binding(NetworkAudioCue.ItemPickup, clips, 0.65f, 0.1f),
                Binding(NetworkAudioCue.ItemSwap, clips, 0.65f, 0.12f),
                Binding(NetworkAudioCue.ItemDrop, clips, 0.6f, 0.1f),
                Binding(NetworkAudioCue.FootstepWalk, clips, 0.5f, 0.08f),
                Binding(NetworkAudioCue.FootstepRun, clips, 0.55f, 0.08f),
                Binding(NetworkAudioCue.PlayerJump, clips, 0.62f, 0.15f));
        }

        private static NetworkAudioCueEmitter ConfigurePlayerOwnerEmitter(
            GameObject gameObject,
            AudioSource source,
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            return ConfigureEmitter(
                gameObject,
                source,
                Binding(NetworkAudioCue.ItemPickup, clips, 0.65f, 0.1f),
                Binding(NetworkAudioCue.ItemSwap, clips, 0.65f, 0.12f),
                Binding(NetworkAudioCue.ItemDrop, clips, 0.6f, 0.1f),
                Binding(NetworkAudioCue.FootstepWalk, clips, 0.42f, 0.08f),
                Binding(NetworkAudioCue.FootstepRun, clips, 0.48f, 0.08f),
                Binding(NetworkAudioCue.PlayerJump, clips, 0.62f, 0.15f),
                Binding(NetworkAudioCue.VendingInteraction, clips, 0.72f, 0.12f),
                Binding(NetworkAudioCue.InteractionFocus, clips, 0.38f, 0.1f),
                Binding(NetworkAudioCue.OptionsSaved, clips, 0.68f, 0.2f));
        }

        private static void ConfigureShopPrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(ShopPrefabPath, root =>
            {
                var checkout = RequireSingle<ShopCheckoutZone>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_NetworkShopAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, true, 20f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.ShopSuccess, clips, 0.8f, 0.2f),
                    Binding(NetworkAudioCue.ShopFailure, clips, 0.75f, 0.2f));
                SetObjectReference(checkout, "audioCuePlayerSource", emitter);
            });
        }

        private static void ConfigureRunRootPrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(RunRootPrefabPath, root =>
            {
                var ledger = RequireSingle<NetworkRunIncidentLedger>(root);
                var clock = RequireSingle<NetworkRunStageClock>(root);
                var presenter = GetOrAddSingle<NetworkRunWarningAudioPresenter>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_NetworkWarningAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, false, 25f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.Warning, clips, 0.8f, 0.5f),
                    Binding(NetworkAudioCue.AccidentAppeared, clips, 0.82f, 0.4f));
                SetObjectReference(presenter, "incidentLedger", ledger);
                SetObjectReference(presenter, "stageClock", clock);
                SetObjectReference(presenter, "cuePlayerSource", emitter);

                var runFlow = RequireSingle<NetworkRunFlowCoordinator>(root);
                var warpPresenter = GetOrAddSingle<NetworkRunWarpAudioPresenter>(root);
                var warpAudioRoot = RequireNamedChild(root.transform, "PHS_NetworkWarpAudio");
                var warpSource = ConfigureAudioSource(warpAudioRoot.gameObject, false, 25f);
                var warpEmitter = ConfigureEmitter(
                    warpAudioRoot.gameObject,
                    warpSource,
                    Binding(NetworkAudioCue.WarpStart, clips, 0.5f, 0.5f),
                    Binding(NetworkAudioCue.WarpEnd, clips, 0.5f, 0.5f));
                SetObjectReference(warpPresenter, "runFlowCoordinator", runFlow);
                SetObjectReference(warpPresenter, "cuePlayerSource", warpEmitter);
            });
        }

        private static void ConfigureDebrisSellPrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(DebrisSellPrefabPath, root =>
            {
                var sellZone = RequireSingle<DebrisSellZone>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_DebrisDepositAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, true, 18f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.DebrisDeposit, clips, 0.8f, 0.15f));
                SetObjectReference(sellZone, "successCuePlayerSource", emitter);
            });
        }

        private static void ConfigurePausePrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(PausePrefabPath, root =>
            {
                var optionsPanel = RequireSingle<NetworkSharedOptionsPanelController>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_OptionsSaveAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, false, 25f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.OptionsSaved, clips, 0.68f, 0.2f));
                SetObjectReference(optionsPanel, "saveCuePlayerSource", emitter);
            });
        }

        private static void ConfigureLobbySettingsPrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(LobbyUiPrefabPath, root =>
            {
                var settings = RequireSingle<ParkHanSolGameSettingsController>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_OptionsSaveAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, false, 25f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.OptionsSaved, clips, 0.68f, 0.2f));
                SetObjectReference(settings, "saveCuePlayerSource", emitter);
            });
        }

        private static void ConfigureMiniGamePrefab(
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips)
        {
            EditPrefab(MiniGamePrefabPath, root =>
            {
                var manager = RequireSingle<PHSMiniGameManager>(root);
                var audioRoot = RequireNamedChild(root.transform, "PHS_MissionSuccessAudio");
                var source = ConfigureAudioSource(audioRoot.gameObject, false, 25f);
                var emitter = ConfigureEmitter(
                    audioRoot.gameObject,
                    source,
                    Binding(NetworkAudioCue.MissionSuccess, clips, 0.85f, 0.3f));
                SetObjectReference(manager, "successCuePlayerSource", emitter);
            });
        }

        private static void ConfigureGameplayBackgroundScene()
        {
            var loadedScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (loadedScene.IsValid() && loadedScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=target_scene_dirty path={GameplayScenePath}");
            }

            var openedForAuthoring = !loadedScene.IsValid();
            var scene = openedForAuthoring
                ? EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive)
                : loadedScene;
            try
            {
                var runtimeRoots = scene.GetRootGameObjects()
                    .Where(root => root.name == "PHS_Map_Runtime")
                    .ToArray();
                if (runtimeRoots.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=map_runtime_root_count count={runtimeRoots.Length}");
                }

                var audioParent = RequireNamedChild(runtimeRoots[0].transform, "Audio");
                var loopRoot = RequireNamedChild(audioParent, "PHS_SpaceEngineLoop");
                var source = ConfigureAudioSource(loopRoot.gameObject, false, 25f);
                source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    PHSCuratedAssetSfxAuthoring.SpaceEngineLoopPath);
                if (source.clip == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=background_clip_missing path={PHSCuratedAssetSfxAuthoring.SpaceEngineLoopPath}");
                }

                source.volume = 0.12f;
                source.loop = true;
                source.playOnAwake = true;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=map_scene_save_failed path={GameplayScenePath}");
                }
            }
            finally
            {
                if (openedForAuthoring && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureTutorialScene()
        {
            var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
            var directors = FindSceneComponents<NetworkTutorialDirector>(scene);
            if (directors.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=tutorial_director_count count={directors.Length}");
            }

            var emitters = FindSceneComponents<NetworkAudioCueEmitter>(scene)
                .Where(component => component.name == "PHS_NetworkTutorialCompletionAudio")
                .ToArray();
            if (emitters.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=tutorial_completion_emitter_count count={emitters.Length}");
            }

            SetObjectReference(directors[0], "audioCuePlayerSource", emitters[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_AUDIO_WIRING_FAILED reason=tutorial_scene_save_failed");
            }
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void EditPrefab(string path, Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=prefab_load_failed path={path}");
            }

            try
            {
                configure(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=prefab_save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform RequireNamedChild(Transform parent, string childName)
        {
            var matches = Enumerable.Range(0, parent.childCount)
                .Select(parent.GetChild)
                .Where(child => child.name == childName)
                .ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=duplicate_named_child name={childName}");
            }

            if (matches.Length == 1)
            {
                return matches[0];
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T RequireSingle<T>(GameObject root)
            where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=component_count type={typeof(T).Name} count={matches.Length} prefab={root.name}");
            }

            return matches[0];
        }

        private static T GetOrAddSingle<T>(GameObject gameObject)
            where T : Component
        {
            var matches = gameObject.GetComponents<T>();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=duplicate_component type={typeof(T).Name} object={gameObject.name}");
            }

            return matches.Length == 1 ? matches[0] : gameObject.AddComponent<T>();
        }

        private static T RequireNamedComponentInDescendants<T>(
            Transform root,
            string objectName)
            where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true)
                .Where(component => component.name == objectName)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=named_component_count type={typeof(T).Name} name={objectName} count={matches.Length}");
            }

            return matches[0];
        }

        private static AudioSource ConfigureAudioSource(
            GameObject gameObject,
            bool isSpatial,
            float maxDistance)
        {
            var source = GetOrAddSingle<AudioSource>(gameObject);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = isSpatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            return source;
        }

        private static NetworkAudioCueEmitter ConfigureEmitter(
            GameObject gameObject,
            AudioSource source,
            params CueBindingData[] bindings)
        {
            var emitter = GetOrAddSingle<NetworkAudioCueEmitter>(gameObject);
            var serialized = new SerializedObject(emitter);
            serialized.FindProperty("audioSource").objectReferenceValue = source;
            var bindingsProperty = serialized.FindProperty("cueBindings");
            bindingsProperty.arraySize = bindings.Length;
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var element = bindingsProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("cue").intValue = (int)binding.Cue;
                element.FindPropertyRelative("clip").objectReferenceValue = binding.Clip;
                element.FindPropertyRelative("volumeScale").floatValue = binding.Volume;
                element.FindPropertyRelative("cooldownSeconds").floatValue = binding.Cooldown;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return emitter;
        }

        private static CueBindingData Binding(
            NetworkAudioCue cue,
            IReadOnlyDictionary<NetworkAudioCue, AudioClip> clips,
            float volume,
            float cooldown)
        {
            return new CueBindingData(cue, clips[cue], volume, cooldown);
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=serialized_property_missing type={target.GetType().Name} property={propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireNoDirtyLoadedScenes()
        {
            var dirtyScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded && scene.isDirty)
                .Select(scene => scene.path)
                .ToArray();
            if (dirtyScenes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=dirty_scene_loaded paths={string.Join(",", dirtyScenes)}");
            }
        }

        private readonly struct CueBindingData
        {
            public CueBindingData(
                NetworkAudioCue cue,
                AudioClip clip,
                float volume,
                float cooldown)
            {
                Cue = cue;
                Clip = clip;
                Volume = volume;
                Cooldown = cooldown;
            }

            public NetworkAudioCue Cue { get; }
            public AudioClip Clip { get; }
            public float Volume { get; }
            public float Cooldown { get; }
        }
    }
}
