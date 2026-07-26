using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSAmbientMusicAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string PrefabPath =
            Root + "/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string MixerPath = Root + "/06. Audio/PHS_GameAudio.mixer";
        private const string ClipPath =
            Root + "/06. Audio/Music/CC0_SRG774_DarkSciFi/PHS_BGM_Airy_CC0.ogg";
        private const string MusicRootName = "PHS_AmbientMusic_Airy";
        private const float MaximumVolume = 0.08f;
        private const float CrossfadeSeconds = 1.5f;

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Ambient Music")]
        public static void Author()
        {
            ConfigureImporter();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var ambientGroup = mixer == null
                ? null
                : mixer.FindMatchingGroups(string.Empty)
                    .SingleOrDefault(group => group.name == "Ambient");
            if (clip == null || ambientGroup == null)
            {
                throw new InvalidOperationException(
                    $"PHS_AMBIENT_MUSIC_FAILED reason=asset_missing clip={clip != null} ambient={ambientGroup != null}");
            }

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RemoveExistingMusicRoots(prefab.transform);
                var musicRoot = new GameObject(MusicRootName);
                musicRoot.transform.SetParent(prefab.transform, false);

                var primary = CreateSource(musicRoot.transform, "Primary", clip, ambientGroup);
                var secondary = CreateSource(musicRoot.transform, "Secondary", clip, ambientGroup);
                var loop = musicRoot.AddComponent<PHSAmbientMusicLoop>();
                var serialized = new SerializedObject(loop);
                serialized.FindProperty("musicClip").objectReferenceValue = clip;
                serialized.FindProperty("primarySource").objectReferenceValue = primary;
                serialized.FindProperty("secondarySource").objectReferenceValue = secondary;
                serialized.FindProperty("maximumVolume").floatValue = MaximumVolume;
                serialized.FindProperty("crossfadeSeconds").floatValue = CrossfadeSeconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_AMBIENT_MUSIC_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                $"PHS_AMBIENT_MUSIC_AUTHORED clip={clip.name} volume={MaximumVolume:F2} crossfade={CrossfadeSeconds:F1}");
        }

        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var loops = prefab == null
                ? Array.Empty<PHSAmbientMusicLoop>()
                : prefab.GetComponentsInChildren<PHSAmbientMusicLoop>(true);
            var sources = prefab == null
                ? Array.Empty<AudioSource>()
                : prefab.GetComponentsInChildren<AudioSource>(true)
                    .Where(source => source.transform.parent != null
                        && source.transform.parent.name == MusicRootName)
                    .ToArray();
            if (loops.Length != 1
                || !loops[0].HasRequiredReferences
                || sources.Length != 2
                || sources.Any(source => source.outputAudioMixerGroup == null
                    || source.outputAudioMixerGroup.name != "Ambient"
                    || source.spatialBlend != 0f
                    || source.playOnAwake
                    || source.loop))
            {
                throw new InvalidOperationException(
                    $"PHS_AMBIENT_MUSIC_VALIDATE_FAILED loops={loops.Length} sources={sources.Length}");
            }

            Debug.Log(
                $"PHS_AMBIENT_MUSIC_VALIDATE_OK loops={loops.Length} sources={sources.Length} volume={MaximumVolume:F2} crossfade={CrossfadeSeconds:F1}");
        }

        private static void ConfigureImporter()
        {
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "PHS_AMBIENT_MUSIC_FAILED reason=importer_missing");
            }

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        private static AudioSource CreateSource(
            Transform parent,
            string name,
            AudioClip clip,
            AudioMixerGroup ambientGroup)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.outputAudioMixerGroup = ambientGroup;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 192;
            return source;
        }

        private static void RemoveExistingMusicRoots(Transform prefabRoot)
        {
            for (var index = prefabRoot.childCount - 1; index >= 0; index--)
            {
                var child = prefabRoot.GetChild(index);
                if (child.name == MusicRootName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
