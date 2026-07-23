using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSItemInteractionAudioAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string AudioRoot = Root + "/06. Audio/NetworkGenerated";
        private static readonly string[] PlayerPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };

        private readonly struct Binding
        {
            public Binding(NetworkAudioCue cue, string file, float volume, float cooldown)
            {
                Cue = cue;
                File = file;
                Volume = volume;
                Cooldown = cooldown;
            }

            public NetworkAudioCue Cue { get; }
            public string File { get; }
            public float Volume { get; }
            public float Cooldown { get; }
        }

        private static readonly Binding[] OwnerBindings =
        {
            new(NetworkAudioCue.ExtinguisherSpray, "PHS_Item_Extinguisher_Spray.wav", 0.55f, 0.12f),
            new(NetworkAudioCue.FoamShot, "PHS_Item_Foam_Shot.wav", 0.65f, 0.08f)
        };

        private static readonly Binding[] WorldBindings =
        {
            new(NetworkAudioCue.WrenchImpact, "PHS_Item_Wrench_Impact.wav", 0.75f, 0.08f),
            new(NetworkAudioCue.RepairComplete, "PHS_Item_Repair_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.ExtinguishComplete, "PHS_Item_Extinguish_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.BatteryInstall, "PHS_Item_Battery_Install.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.FoamAttach, "PHS_Item_Foam_Attach.wav", 0.65f, 0.06f),
            new(NetworkAudioCue.FoamHarden, "PHS_Item_Foam_Harden.wav", 0.65f, 0.12f),
            new(NetworkAudioCue.FoamSealComplete, "PHS_Item_Foam_Seal_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.FoamFireComplete, "PHS_Item_Foam_Fire_Complete.wav", 0.8f, 0.20f)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Item Interaction Audio")]
        public static void Author()
        {
            RequireClips(OwnerBindings);
            RequireClips(WorldBindings);
            foreach (var path in PlayerPaths)
            {
                EditPrefab(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_ITEM_INTERACTION_AUDIO_AUTHORED players=2 owner2D=2 world3D=8");
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
            source.rolloffMode = AudioRolloffMode.Linear;
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
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{binding.File}");
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
                var path = $"{AudioRoot}/{binding.File}";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_ITEM_INTERACTION_AUDIO_AUTHORING_FAILED reason=clip_missing path={path}");
                }
            }
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
