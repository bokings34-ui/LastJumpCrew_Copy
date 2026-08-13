using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSItemInteractionAudioAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private static readonly string[] PlayerPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };

        private readonly struct Binding
        {
            public Binding(NetworkAudioCue cue, float volume, float cooldown)
            {
                Cue = cue;
                Volume = volume;
                Cooldown = cooldown;
            }

            public NetworkAudioCue Cue { get; }
            public float Volume { get; }
            public float Cooldown { get; }
        }

        private static readonly Binding[] OwnerBindings =
        {
            new(NetworkAudioCue.WrenchImpact, 0.6f, 0.08f),
            new(NetworkAudioCue.ExtinguisherSpray, 0.55f, 0.12f),
            new(NetworkAudioCue.FoamShot, 0.65f, 0.08f)
        };

        private static readonly Binding[] WorldBindings =
        {
            new(NetworkAudioCue.WrenchImpact, 0.75f, 0.08f),
            new(NetworkAudioCue.ExtinguisherSpray, 0.7f, 0.12f),
            new(NetworkAudioCue.RepairComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.ExtinguishComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.BatteryInstall, 0.8f, 0.20f),
            new(NetworkAudioCue.FoamAttach, 0.65f, 0.06f),
            new(NetworkAudioCue.FoamHarden, 0.65f, 0.12f),
            new(NetworkAudioCue.FoamSealComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.FoamFireComplete, 0.8f, 0.20f)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Item Interaction Audio")]
        public static void Author()
        {
            PHSCuratedAssetSfxAuthoring.Author();
            RequireClips(OwnerBindings);
            RequireClips(WorldBindings);
            foreach (var path in PlayerPaths)
            {
                EditPrefab(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_ITEM_INTERACTION_AUDIO_AUTHORED players=2 owner2D=3 world3D=9");
        }

        private static void EditPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=prefab_missing path={path}");
            }

            try
            {
                var relay = GetOrAddSingle<PHSNetworkItemInteractionAudioRelay>(root);
                var ownerEmitter = ConfigureEmitter(
                    RequireChild(root.transform, "PHS_ItemInteractionAudio_2D"),
                    false,
                    OwnerBindings);
                var worldEmitter = ConfigureEmitter(
                    RequireChild(root.transform, "PHS_ItemInteractionAudio_3D"),
                    true,
                    WorldBindings);
                var relayObject = new SerializedObject(relay);
                relayObject.FindProperty("ownerCuePlayerSource").objectReferenceValue = ownerEmitter;
                relayObject.FindProperty("worldCuePlayerSource").objectReferenceValue = worldEmitter;
                relayObject.ApplyModifiedPropertiesWithoutUndo();
                ConfigureBatteryShockAudio(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static NetworkAudioCueEmitter ConfigureEmitter(
            GameObject gameObject,
            bool spatial,
            IReadOnlyList<Binding> bindings)
        {
            var source = GetOrAddSingle<AudioSource>(gameObject);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 20f;

            var emitter = GetOrAddSingle<NetworkAudioCueEmitter>(gameObject);
            var serialized = new SerializedObject(emitter);
            serialized.FindProperty("audioSource").objectReferenceValue = source;
            var array = serialized.FindProperty("cueBindings");
            array.arraySize = bindings.Count;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var element = array.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("cue").enumValueIndex = (int)binding.Cue;
                element.FindPropertyRelative("clip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        PHSCuratedAssetSfxAuthoring.GetCuePath(binding.Cue));
                element.FindPropertyRelative("volumeScale").floatValue = binding.Volume;
                element.FindPropertyRelative("cooldownSeconds").floatValue = binding.Cooldown;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return emitter;
        }

        private static void RequireClips(IEnumerable<Binding> bindings)
        {
            foreach (var binding in bindings)
            {
                var path = PHSCuratedAssetSfxAuthoring.GetCuePath(binding.Cue);
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=clip_missing path={path}");
                }
            }
        }

        private static void ConfigureBatteryShockAudio(GameObject root)
        {
            var status = root.GetComponent<StatusEffectController>();
            var effectRoot = status == null
                ? null
                : new SerializedObject(status)
                    .FindProperty("electricShockEffectRoot")
                    ?.objectReferenceValue as GameObject;
            var sources = effectRoot == null
                ? Array.Empty<AudioSource>()
                : effectRoot.GetComponents<AudioSource>();
            if (status == null || effectRoot == null || sources.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=electric_shock_contract_invalid prefab={root.name} sources={sources.Length}");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                PHSCuratedAssetSfxAuthoring.BatteryShockPath);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=battery_shock_clip_missing path={PHSCuratedAssetSfxAuthoring.BatteryShockPath}");
            }

            var source = sources[0];
            source.enabled = true;
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.65f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 15f;
        }

        private static GameObject RequireChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAddSingle<T>(GameObject gameObject)
            where T : Component
        {
            var components = gameObject.GetComponents<T>();
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=duplicate type={typeof(T).Name} object={gameObject.name}");
            }

            return components.Length == 1
                ? components[0]
                : gameObject.AddComponent<T>();
        }
    }
}
