using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSWarpSafeZoneAudioAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string AudioRoot = Root + "/06. Audio/NetworkGenerated";
        private const string MixerPath = Root + "/06. Audio/PHS_GameAudio.mixer";
        private const string ThrusterAudioClipPath =
            Root + "/03. Audio/PHS_ZeroGravityThruster_CC0.ogg";
        private const string PlayerPrefabPath =
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string TutorialPlayerPrefabPath =
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab";
        private const string AudioObjectName = "PHS_WarpSafeZoneAudio";
        private const string ThrusterAudioObjectName = "PHS_NetworkThrusterAudio";

        private static readonly string[] PlayerPrefabPaths =
        {
            PlayerPrefabPath,
            TutorialPlayerPrefabPath
        };

        public static void AuthorAll()
        {
            PHSNetworkGeneratedAudioAuthoring.Author();
            Author();
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Warp Safe Zone Audio")]
        public static void Author()
        {
            var enterClip = RequireClip("PHS_Warp_SafeZone_Enter.wav");
            var exitClip = RequireClip("PHS_Warp_SafeZone_Exit.wav");
            var thrusterClip = RequireClipAtPath(ThrusterAudioClipPath);
            var sfxGroup = ResolveSfxGroup();

            foreach (var path in PlayerPrefabPaths)
            {
                EditPrefab(path, root =>
                {
                    var feedback = GetOrAddSingle<NetworkWarpSafeZoneAudioFeedback>(root);
                    var audioRoot = RequireNamedDirectChild(root.transform, AudioObjectName);
                    var source = GetOrAddSingle<AudioSource>(audioRoot.gameObject);
                    source.playOnAwake = false;
                    source.loop = false;
                    source.volume = 0f;
                    source.spatialBlend = 0f;
                    source.dopplerLevel = 0f;
                    source.outputAudioMixerGroup = sfxGroup;

                    var serialized = new SerializedObject(feedback);
                    serialized.FindProperty("audioSource").objectReferenceValue = source;
                    serialized.FindProperty("enterClip").objectReferenceValue = enterClip;
                    serialized.FindProperty("exitClip").objectReferenceValue = exitClip;
                    serialized.FindProperty("enterVolume").floatValue = 0.7f;
                    serialized.FindProperty("exitVolume").floatValue = 0.72f;
                    serialized.FindProperty("exitFadeSeconds").floatValue = 0.18f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    ConfigureThrusterAudio(root, thrusterClip, sfxGroup);
                });
            }

            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log(
                "PHS_PLAYER_THRUSTER_WARP_AUDIO_AUTHORED prefabs=2 thruster=PHS_ZeroGravityThruster_CC0.ogg enter=PHS_Warp_SafeZone_Enter.wav exit=PHS_Warp_SafeZone_Exit.wav exitFade=0.18");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Warp Safe Zone Audio")]
        public static void Validate()
        {
            ValidateOrThrow();
            Debug.Log("PHS_PLAYER_THRUSTER_WARP_AUDIO_VALIDATE_OK prefabs=2 exitFade=0.18");
        }

        private static void ConfigureThrusterAudio(
            GameObject root,
            AudioClip thrusterClip,
            AudioMixerGroup sfxGroup)
        {
            var controller = RequireSingle<NetworkPlayerController>(root);
            var thrusterRoot = RequireNamedDirectChild(
                root.transform,
                ThrusterAudioObjectName);
            var source = GetOrAddSingle<AudioSource>(thrusterRoot.gameObject);
            source.clip = thrusterClip;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0.45f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.5f;
            source.maxDistance = 14f;
            source.outputAudioMixerGroup = sfxGroup;

            var feedback = GetOrAddSingle<NetworkPlayerThrusterAudio>(
                thrusterRoot.gameObject);
            var serializedFeedback = new SerializedObject(feedback);
            SetObjectReference(serializedFeedback, "audioSource", source);
            SetObjectReference(serializedFeedback, "loopClip", thrusterClip);
            SetFloat(serializedFeedback, "maximumVolume", 0.55f);
            SetFloat(serializedFeedback, "attackSpeed", 2.2f);
            SetFloat(serializedFeedback, "releaseSpeed", 1.2f);
            SetFloat(serializedFeedback, "spatialBlend", 0.45f);
            SetFloat(serializedFeedback, "minimumDistance", 0.5f);
            SetFloat(serializedFeedback, "maximumDistance", 14f);
            serializedFeedback.ApplyModifiedPropertiesWithoutUndo();

            var serializedController = new SerializedObject(controller);
            SetObjectReference(serializedController, "thrusterAudio", feedback);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateOrThrow()
        {
            foreach (var path in PlayerPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_SAFE_ZONE_AUDIO_VALIDATE_FAILED reason=prefab_missing path={path}");
                }

                var feedbacks = prefab.GetComponentsInChildren<NetworkWarpSafeZoneAudioFeedback>(true);
                var sources = prefab.GetComponentsInChildren<AudioSource>(true)
                    .Where(source => source.name == AudioObjectName)
                    .ToArray();
                if (feedbacks.Length != 1
                    || sources.Length != 1
                    || !feedbacks[0].HasRequiredReferences
                    || sources[0].playOnAwake
                    || sources[0].loop
                    || !Mathf.Approximately(sources[0].spatialBlend, 0f))
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_SAFE_ZONE_AUDIO_VALIDATE_FAILED reason=wiring_invalid path={path} feedback={feedbacks.Length} source={sources.Length}");
                }

                var serialized = new SerializedObject(feedbacks[0]);
                var fade = serialized.FindProperty("exitFadeSeconds").floatValue;
                if (!Mathf.Approximately(fade, 0.18f))
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_SAFE_ZONE_AUDIO_VALIDATE_FAILED reason=exit_fade_invalid path={path} actual={fade}");
                }

                ValidateThrusterAudio(prefab, path);
            }
        }

        private static void ValidateThrusterAudio(GameObject prefab, string path)
        {
            var feedbacks = prefab.GetComponentsInChildren<NetworkPlayerThrusterAudio>(true);
            var sources = prefab.GetComponentsInChildren<AudioSource>(true)
                .Where(source => source.name == ThrusterAudioObjectName)
                .ToArray();
            var controllers = prefab.GetComponentsInChildren<NetworkPlayerController>(true);
            if (feedbacks.Length != 1 || sources.Length != 1 || controllers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_THRUSTER_AUDIO_VALIDATE_FAILED reason=count_invalid path={path} feedback={feedbacks.Length} source={sources.Length} controller={controllers.Length}");
            }

            var source = sources[0];
            var feedback = feedbacks[0];
            var feedbackSerialized = new SerializedObject(feedback);
            var controllerSerialized = new SerializedObject(controllers[0]);
            var valid = source.clip == RequireClipAtPath(ThrusterAudioClipPath)
                && !source.playOnAwake
                && source.loop
                && Mathf.Approximately(source.volume, 0f)
                && Mathf.Approximately(source.spatialBlend, 0.45f)
                && Mathf.Approximately(source.dopplerLevel, 0f)
                && source.rolloffMode == AudioRolloffMode.Logarithmic
                && Mathf.Approximately(source.minDistance, 0.5f)
                && Mathf.Approximately(source.maxDistance, 14f)
                && feedbackSerialized.FindProperty("audioSource")?.objectReferenceValue == source
                && feedbackSerialized.FindProperty("loopClip")?.objectReferenceValue == source.clip
                && controllerSerialized.FindProperty("thrusterAudio")?.objectReferenceValue == feedback;
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"PHS_THRUSTER_AUDIO_VALIDATE_FAILED reason=wiring_invalid path={path}");
            }
        }

        private static AudioClip RequireClip(string fileName)
        {
            var path = $"{AudioRoot}/{fileName}";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_AUTHOR_FAILED reason=clip_missing path={path}");
            }

            return clip;
        }

        private static AudioClip RequireClipAtPath(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_AUDIO_AUTHOR_FAILED reason=clip_missing path={path}");
            }

            return clip;
        }

        private static AudioMixerGroup ResolveSfxGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                return null;
            }

            var groups = mixer.FindMatchingGroups("SFX")
                .Where(group => group.name == "SFX")
                .ToArray();
            return groups.Length == 1 ? groups[0] : null;
        }

        private static void EditPrefab(string path, Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_AUTHOR_FAILED reason=prefab_load_failed path={path}");
            }

            try
            {
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_SAFE_ZONE_AUDIO_AUTHOR_FAILED reason=prefab_save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform RequireNamedDirectChild(Transform parent, string childName)
        {
            var matches = Enumerable.Range(0, parent.childCount)
                .Select(parent.GetChild)
                .Where(child => child.name == childName)
                .ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_AUTHOR_FAILED reason=duplicate_child name={childName}");
            }

            if (matches.Length == 1)
            {
                return matches[0];
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAddSingle<T>(GameObject gameObject)
            where T : Component
        {
            var matches = gameObject.GetComponents<T>();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_AUTHOR_FAILED reason=duplicate_component type={typeof(T).Name} object={gameObject.name}");
            }

            return matches.Length == 1 ? matches[0] : gameObject.AddComponent<T>();
        }

        private static T RequireSingle<T>(GameObject root)
            where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_AUDIO_AUTHOR_FAILED reason=component_count_invalid type={typeof(T).Name} count={matches.Length} root={root.name}");
            }

            return matches[0];
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_AUDIO_AUTHOR_FAILED reason=property_missing type={serialized.targetObject.GetType().Name} property={propertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_AUDIO_AUTHOR_FAILED reason=property_missing type={serialized.targetObject.GetType().Name} property={propertyName}");
            }

            property.floatValue = value;
        }
    }
}
