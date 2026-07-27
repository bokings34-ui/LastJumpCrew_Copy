using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSGameAudioFoundationValidator
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string GeneratedAudioFolder = Root + "/06. Audio/NetworkGenerated";
        private const string MixerPath = Root + "/06. Audio/PHS_GameAudio.mixer";
        private const float PcmDurationThreshold = 0.5f;

        private static readonly string[] GroupNames =
        {
            "Master",
            "UI",
            "SFX",
            "Ambient"
        };

        private static readonly string[] ParameterNames =
        {
            "PHS_MasterVolumeDb",
            "PHS_UIVolumeDb",
            "PHS_SFXVolumeDb",
            "PHS_AmbientVolumeDb"
        };

        private static readonly string[] AudioPrefabPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab",
            Root + "/03. Prefab/Shop/PHS_NetworkShopCheckoutCounter.prefab",
            Root + "/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkRunResultPanel.prefab"
        };

        private static readonly string[] SettingsPrefabPaths =
        {
            Root + "/03. Prefab/UI/ParkHanSol_StartLobbyUI.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Game Audio Foundation")]
        public static void Validate()
        {
            var errors = new List<string>();
            var mixer = ValidateMixer(errors);
            ValidateImporters(errors);
            ValidateAudioPrefabs(mixer, errors);
            ValidateSettingsPrefabs(mixer, errors);
            ValidateSettingsRuntimeContract(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_GAME_AUDIO_FOUNDATION_VALIDATION_FAILED\n" +
                    string.Join("\n", errors));
            }

            Debug.Log(
                "PHS_GAME_AUDIO_FOUNDATION_VALIDATION_PASSED clips=23 pcm_adpcm=true preload=true mixer=4groups sources=routed settings=2");
        }

        private static AudioMixer ValidateMixer(ICollection<string> errors)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                errors.Add($"mixer_missing path={MixerPath}");
                return null;
            }

            foreach (var groupName in GroupNames)
            {
                var groups = mixer.FindMatchingGroups(string.Empty)
                    .Where(group => group.name == groupName)
                    .ToArray();
                if (groups.Length != 1 || groups[0].name != groupName)
                {
                    errors.Add(
                        $"mixer_group_contract group={groupName} count={groups.Length}");
                }
            }

            var serializedMixer = new SerializedObject(mixer);
            var exposedParameters = serializedMixer.FindProperty("m_ExposedParameters");
            var observedParameters = new HashSet<string>(StringComparer.Ordinal);
            if (exposedParameters != null)
            {
                for (var index = 0; index < exposedParameters.arraySize; index++)
                {
                    observedParameters.Add(
                        exposedParameters.GetArrayElementAtIndex(index)
                            .FindPropertyRelative("name").stringValue);
                }
            }

            foreach (var parameterName in ParameterNames)
            {
                if (!observedParameters.Contains(parameterName))
                {
                    errors.Add($"mixer_parameter_missing name={parameterName}");
                }
            }

            return mixer;
        }

        private static void ValidateImporters(ICollection<string> errors)
        {
            var paths = AssetDatabase.FindAssets(
                    "t:AudioClip",
                    new[] { GeneratedAudioFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (paths.Length != 23)
            {
                errors.Add($"wave_count actual={paths.Length} expected=23");
            }

            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (clip == null || importer == null)
                {
                    errors.Add($"audio_importer_missing path={path}");
                    continue;
                }

                var settings = importer.defaultSampleSettings;
                var expectedCompression = clip.length <= PcmDurationThreshold
                    ? AudioCompressionFormat.PCM
                    : AudioCompressionFormat.ADPCM;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad
                    || settings.compressionFormat != expectedCompression
                    || !settings.preloadAudioData
                    || !importer.forceToMono
                    || importer.loadInBackground)
                {
                    errors.Add(
                        $"audio_import_contract path={path} compression={settings.compressionFormat} expected={expectedCompression} preload={settings.preloadAudioData}");
                }
            }
        }

        private static void ValidateAudioPrefabs(
            AudioMixer mixer,
            ICollection<string> errors)
        {
            foreach (var path in AudioPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"audio_prefab_missing path={path}");
                    continue;
                }

                foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
                {
                    var expectedGroup = ResolveGroupName(source);
                    if (source.outputAudioMixerGroup == null
                        || source.outputAudioMixerGroup.audioMixer != mixer
                        || source.outputAudioMixerGroup.name != expectedGroup
                        || source.playOnAwake
                        || !Mathf.Approximately(source.dopplerLevel, 0f)
                        || source.rolloffMode != AudioRolloffMode.Logarithmic
                        || !Mathf.Approximately(source.minDistance, 1f)
                        || !Mathf.Approximately(
                            source.maxDistance,
                            expectedGroup == "Ambient" && source.spatialBlend > 0f
                                ? 30f
                                : 20f))
                    {
                        errors.Add(
                            $"audio_source_contract path={path} source={source.name} group={source.outputAudioMixerGroup?.name ?? "null"}");
                    }
                }
            }
        }

        private static void ValidateSettingsPrefabs(
            AudioMixer mixer,
            ICollection<string> errors)
        {
            foreach (var path in SettingsPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var controllers = prefab == null
                    ? Array.Empty<ParkHanSolGameSettingsController>()
                    : prefab.GetComponentsInChildren<ParkHanSolGameSettingsController>(true);
                if (controllers.Length == 0)
                {
                    errors.Add($"settings_controller_missing path={path}");
                    continue;
                }

                foreach (var controller in controllers)
                {
                    var serialized = new SerializedObject(controller);
                    if (serialized.FindProperty("gameAudioMixer").objectReferenceValue
                        != mixer)
                    {
                        errors.Add($"settings_mixer_reference path={path}");
                    }

                    foreach (var sliderProperty in new[]
                             {
                                 "masterVolumeSlider",
                                 "environmentVolumeSlider",
                                 "effectsVolumeSlider"
                             })
                    {
                        if (serialized.FindProperty(sliderProperty)
                                .objectReferenceValue == null)
                        {
                            errors.Add(
                                $"settings_slider_reference path={path} property={sliderProperty}");
                        }
                    }
                }
            }
        }

        private static void ValidateSettingsRuntimeContract(
            ICollection<string> errors)
        {
            var path = Root +
                "/02. Script/Multiplayer/ParkHanSolGameSettingsController.cs";
            if (!File.Exists(path))
            {
                errors.Add($"settings_source_missing path={path}");
                return;
            }

            var source = File.ReadAllText(path);
            foreach (var marker in new[]
                     {
                         "environmentVolumeSlider.onValueChanged.AddListener(SetEnvironmentVolume)",
                         "effectsVolumeSlider.onValueChanged.AddListener(SetEffectsVolume)",
                         "ApplyMixerVolume(EnvironmentMixerParameter, volume)",
                         "ApplyMixerVolume(EffectsMixerParameter, volume)",
                         "ApplyMixerVolume(UiMixerParameter, volume)",
                         "gameAudioMixer.SetFloat(parameterName, decibels)"
                     })
            {
                if (!source.Contains(marker, StringComparison.Ordinal))
                {
                    errors.Add($"settings_runtime_contract marker={marker}");
                }
            }
        }

        private static string ResolveGroupName(AudioSource source)
        {
            var name = source.name;
            if (ContainsAny(name, "Result", "Warning", "TutorialCompletion", "UI"))
            {
                return "UI";
            }

            if (ContainsAny(name, "Thruster", "Ambient", "Environment", "FireLoop"))
            {
                return "Ambient";
            }

            return "SFX";
        }

        private static bool ContainsAny(string value, params string[] markers)
        {
            return markers.Any(marker =>
                value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
