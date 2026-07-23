using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
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
        private const string AudioRoot = Root + "/06. Audio/NetworkGenerated";
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

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Audio Wiring")]
        public static void Author()
        {
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
                ConfigureTutorialScene();
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

        private static Dictionary<NetworkAudioCue, AudioClip> LoadRequiredClips()
        {
            var clips = new Dictionary<NetworkAudioCue, AudioClip>
            {
                { NetworkAudioCue.ItemPickup, RequireClip("PHS_Network_Item_Pickup.wav") },
                { NetworkAudioCue.ItemDrop, RequireClip("PHS_Network_Item_Drop.wav") },
                { NetworkAudioCue.ItemSwap, RequireClip("PHS_Network_Item_Swap.wav") },
                { NetworkAudioCue.ShopSuccess, RequireClip("PHS_Network_Shop_Success.wav") },
                { NetworkAudioCue.ShopFailure, RequireClip("PHS_Network_Shop_Fail.wav") },
                { NetworkAudioCue.Warning, RequireClip("PHS_Network_Warning.wav") },
                { NetworkAudioCue.RunClear, RequireClip("PHS_Network_Clear.wav") },
                { NetworkAudioCue.RunGameOver, RequireClip("PHS_Network_GameOver.wav") },
                { NetworkAudioCue.RestartRequested, RequireClip("PHS_Network_UI_Click.wav") },
                { NetworkAudioCue.RestartSucceeded, RequireClip("PHS_Network_Restart_Success.wav") },
                { NetworkAudioCue.RestartFailed, RequireClip("PHS_Network_Restart_Fail.wav") },
                { NetworkAudioCue.TutorialComplete, RequireClip("PHS_Network_TutorialComplete.wav") },
            };

            return clips;
        }

        private static AudioClip RequireClip(string fileName)
        {
            var path = $"{AudioRoot}/{fileName}";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_AUDIO_WIRING_FAILED reason=generated_clip_missing path={path}");
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
                var feedback = GetOrAddSingle<NetworkPlayerItemAudioFeedback>(root);

                var ownerRoot = RequireNamedChild(root.transform, "PHS_NetworkItemAudio_2D");
                var ownerSource = ConfigureAudioSource(ownerRoot.gameObject, false, 25f);
                var ownerEmitter = ConfigureItemEmitter(ownerRoot.gameObject, ownerSource, clips);

                var worldRoot = RequireNamedChild(root.transform, "PHS_NetworkItemAudio_3D");
                var worldSource = ConfigureAudioSource(worldRoot.gameObject, true, 20f);
                var worldEmitter = ConfigureItemEmitter(worldRoot.gameObject, worldSource, clips);

                SetObjectReference(feedback, "itemRecord", record);
                SetObjectReference(feedback, "networkObject", networkObject);
                SetObjectReference(feedback, "ownerCuePlayerSource", ownerEmitter);
                SetObjectReference(feedback, "worldCuePlayerSource", worldEmitter);

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
                Binding(NetworkAudioCue.ItemDrop, clips, 0.6f, 0.1f));
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
                    Binding(NetworkAudioCue.Warning, clips, 0.8f, 0.5f));
                SetObjectReference(presenter, "incidentLedger", ledger);
                SetObjectReference(presenter, "stageClock", clock);
                SetObjectReference(presenter, "cuePlayerSource", emitter);
            });
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
