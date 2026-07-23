using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSGameAudioFoundationAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string AudioFolder = Root + "/06. Audio";
        private const string GeneratedAudioFolder = AudioFolder + "/NetworkGenerated";
        private const string MixerPath = AudioFolder + "/PHS_GameAudio.mixer";
        private const string FirePresentationPrefabPath = Root +
            "/03. Prefab/Props/Prefabs/IncidentFire/PHS_TeamFirePatchPresentation.prefab";
        private const string FireLoopClipPath =
            "Assets/04. NohSeokMin_Game Event/99_Resource/Sound_Fire.mp3";
        private const float PcmDurationThreshold = 0.5f;
        private const float FireMinimumDistance = 1.5f;
        private const float FireMaximumDistance = 18f;
        private const int FirePriority = 96;

        private static readonly string[] MixerGroupNames =
        {
            "Master",
            "UI",
            "SFX",
            "Ambient"
        };

        private static readonly string[] MixerParameterNames =
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

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Game Audio Foundation")]
        public static void Author()
        {
            ConfigureGeneratedAudioImporters();
            var mixer = EnsureMixer();
            var groups = MixerGroupNames.ToDictionary(
                name => name,
                name => RequireSingleGroup(mixer, name));
            ConfigureAudioPrefabs(groups);
            ConfigureFirePresentationPrefab(groups["SFX"]);
            ConfigureSettingsPrefabs(mixer);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "PHS_GAME_AUDIO_FOUNDATION_AUTHORED clips=23 mixer=Master,UI,SFX,Ambient prefabs=8 fire=team_loop_3d");
        }

        private static void ConfigureGeneratedAudioImporters()
        {
            var guids = AssetDatabase.FindAssets(
                "t:AudioClip",
                new[] { GeneratedAudioFolder });
            var paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length != 23)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=wave_count actual={paths.Length} expected=23");
            }

            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (clip == null || importer == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=audio_importer_missing path={path}");
                }

                var settings = importer.defaultSampleSettings;
                var compressionFormat = clip.length <= PcmDurationThreshold
                    ? AudioCompressionFormat.PCM
                    : AudioCompressionFormat.ADPCM;
                var requiresReimport =
                    settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                    settings.compressionFormat != compressionFormat ||
                    settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                    !settings.preloadAudioData ||
                    !importer.forceToMono ||
                    importer.loadInBackground;
                if (!requiresReimport)
                {
                    continue;
                }

                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = compressionFormat;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }

        private static AudioMixer EnsureMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                var controllerType = RequireEditorType(
                    "UnityEditor.Audio.AudioMixerController");
                var createMethod = controllerType.GetMethod(
                    "CreateMixerControllerAtPath",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                mixer = createMethod?.Invoke(null, new object[] { MixerPath }) as AudioMixer;
                if (mixer == null)
                {
                    throw new InvalidOperationException(
                        "PHS_GAME_AUDIO_FOUNDATION_FAILED reason=mixer_create_failed");
                }
            }

            EnsureGroups(mixer);
            ConfigureExposedVolumeParameters(mixer);
            EditorUtility.SetDirty(mixer);
            return mixer;
        }

        private static void EnsureGroups(AudioMixer mixer)
        {
            var controller = (object)mixer;
            var controllerType = controller.GetType();
            var master = RequireSingleGroup(mixer, "Master");
            var createMethod = controllerType.GetMethod(
                "CreateNewGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var addMethod = controllerType.GetMethod(
                "AddChildToParent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var name in MixerGroupNames.Skip(1))
            {
                if (FindExactGroups(mixer, name).Length == 1)
                {
                    continue;
                }

                var group = createMethod?.Invoke(controller, new object[] { name, false });
                if (group == null || addMethod == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=mixer_group_create_failed group={name}");
                }

                addMethod.Invoke(controller, new[] { group, master });
            }
        }

        private static void ConfigureExposedVolumeParameters(AudioMixer mixer)
        {
            var controller = (object)mixer;
            var controllerType = controller.GetType();
            var exposedType = RequireEditorType(
                "UnityEditor.Audio.ExposedAudioParameter");
            var guidField = exposedType.GetField(
                "guid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var nameField = exposedType.GetField(
                "name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var exposedProperty = controllerType.GetProperty(
                "exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var parameterArray = Array.CreateInstance(exposedType, MixerGroupNames.Length);

            for (var index = 0; index < MixerGroupNames.Length; index++)
            {
                var group = RequireSingleGroup(mixer, MixerGroupNames[index]);
                var getGuidMethod = group.GetType().GetMethod(
                    "GetGUIDForVolume",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var parameter = Activator.CreateInstance(exposedType);
                guidField?.SetValue(parameter, getGuidMethod?.Invoke(group, null));
                nameField?.SetValue(parameter, MixerParameterNames[index]);
                parameterArray.SetValue(parameter, index);
            }

            if (exposedProperty?.CanWrite != true)
            {
                throw new InvalidOperationException(
                    "PHS_GAME_AUDIO_FOUNDATION_FAILED reason=exposed_parameter_api_missing");
            }

            exposedProperty.SetValue(controller, parameterArray);
        }

        private static void ConfigureAudioPrefabs(
            IReadOnlyDictionary<string, AudioMixerGroup> groups)
        {
            foreach (var path in AudioPrefabPaths)
            {
                EditPrefab(path, root =>
                {
                    foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                    {
                        source.playOnAwake = false;
                        source.dopplerLevel = 0f;
                        source.rolloffMode = AudioRolloffMode.Logarithmic;
                        source.minDistance = 1f;
                        source.maxDistance = ResolveMaxDistance(source);
                        source.outputAudioMixerGroup = groups[ResolveGroupName(source)];
                        source.priority = ResolvePriority(source);
                    }
                });
            }
        }

        private static void ConfigureFirePresentationPrefab(
            AudioMixerGroup sfxGroup)
        {
            // Reuse NohSeokMin team's authored looping fire bed. The PHS-owned
            // wrapper only restores its reference and owns online mix/spatial settings.
            var fireLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                FireLoopClipPath);
            if (fireLoopClip == null)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=fire_loop_clip_missing path={FireLoopClipPath}");
            }

            EditPrefab(FirePresentationPrefabPath, root =>
            {
                var sources = root.GetComponentsInChildren<AudioSource>(true);
                if (sources.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=fire_audio_source_count actual={sources.Length} expected=1");
                }

                var source = sources[0];
                source.clip = fireLoopClip;
                source.outputAudioMixerGroup = sfxGroup;
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = FireMinimumDistance;
                source.maxDistance = FireMaximumDistance;
                source.priority = FirePriority;
            });
        }

        private static void ConfigureSettingsPrefabs(AudioMixer mixer)
        {
            foreach (var path in SettingsPrefabPaths)
            {
                EditPrefab(path, root =>
                {
                    var controllers = root
                        .GetComponentsInChildren<ParkHanSolGameSettingsController>(true);
                    if (controllers.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=settings_controller_missing path={path}");
                    }

                    foreach (var controller in controllers)
                    {
                        var serialized = new SerializedObject(controller);
                        serialized.FindProperty("gameAudioMixer").objectReferenceValue = mixer;
                        var masterSlider = serialized.FindProperty("masterVolumeSlider")
                            .objectReferenceValue as Slider;
                        if (masterSlider == null)
                        {
                            throw new InvalidOperationException(
                                $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=master_slider_missing path={path}");
                        }

                        if (masterSlider.transform.parent is RectTransform masterRow)
                        {
                            masterRow.anchorMin = new Vector2(masterRow.anchorMin.x, 0.68f);
                            masterRow.anchorMax = new Vector2(masterRow.anchorMax.x, 0.68f);
                            masterRow.anchoredPosition = new Vector2(
                                masterRow.anchoredPosition.x,
                                0f);
                        }

                        EnsureVolumeSlider(
                            serialized,
                            masterSlider,
                            "environmentVolumeSlider",
                            "ENVIRONMENT",
                            "ENVIRONMENT VOLUME",
                            "Environment Volume Slider",
                            0.5f,
                            1);
                        EnsureVolumeSlider(
                            serialized,
                            masterSlider,
                            "effectsVolumeSlider",
                            "EFFECTS",
                            "EFFECTS VOLUME",
                            "Effects Volume Slider",
                            0.32f,
                            2);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                });
            }
        }

        private static void EnsureVolumeSlider(
            SerializedObject controller,
            Slider masterSlider,
            string propertyName,
            string rowName,
            string label,
            string sliderName,
            float anchorY,
            int siblingOffset)
        {
            var property = controller.FindProperty(propertyName);
            if (property.objectReferenceValue is Slider)
            {
                return;
            }

            var masterRow = masterSlider.transform.parent as RectTransform;
            var rowsParent = masterRow != null ? masterRow.parent : null;
            if (masterRow == null || rowsParent == null)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=volume_row_missing property={propertyName}");
            }

            RectTransform row = null;
            for (var index = 0; index < rowsParent.childCount; index++)
            {
                var candidate = rowsParent.GetChild(index) as RectTransform;
                if (candidate != null && candidate.name == rowName)
                {
                    row = candidate;
                    break;
                }
            }

            if (row == null)
            {
                row = UnityEngine.Object.Instantiate(
                    masterRow.gameObject,
                    rowsParent,
                    false).GetComponent<RectTransform>();
                row.name = rowName;
                row.SetSiblingIndex(Mathf.Min(
                    masterRow.GetSiblingIndex() + siblingOffset,
                    rowsParent.childCount - 1));
            }

            row.anchorMin = new Vector2(masterRow.anchorMin.x, anchorY);
            row.anchorMax = new Vector2(masterRow.anchorMax.x, anchorY);
            row.anchoredPosition = new Vector2(masterRow.anchoredPosition.x, 0f);
            row.sizeDelta = masterRow.sizeDelta;

            var slider = row.GetComponentInChildren<Slider>(true);
            if (slider == null)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=cloned_slider_missing property={propertyName}");
            }

            slider.name = sliderName;
            var text = row.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.name = label + " Label";
            }

            property.objectReferenceValue = slider;
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

        private static float ResolveMaxDistance(AudioSource source)
        {
            if (Mathf.Approximately(source.spatialBlend, 0f))
            {
                return 20f;
            }

            if (ContainsAny(source.name, "ElectricShock"))
            {
                return 15f;
            }

            return ResolveGroupName(source) == "Ambient" ? 30f : 20f;
        }

        private static int ResolvePriority(AudioSource source)
        {
            var name = source.name;
            if (ContainsAny(name, "Warning", "RunResult"))
            {
                return 32;
            }

            if (ContainsAny(name, "TutorialCompletion", "UI"))
            {
                return 48;
            }

            if (ContainsAny(name, "InteractionAudio_3D"))
            {
                return 64;
            }

            if (ContainsAny(name, "ElectricShock"))
            {
                return 80;
            }

            if (ContainsAny(name, "InteractionAudio_2D", "NetworkItemAudio"))
            {
                return 96;
            }

            if (ContainsAny(name, "Thruster", "Ambient", "Environment"))
            {
                return 192;
            }

            return 112;
        }

        private static bool ContainsAny(string value, params string[] markers)
        {
            return markers.Any(marker =>
                value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static AudioMixerGroup RequireSingleGroup(
            AudioMixer mixer,
            string name)
        {
            var groups = FindExactGroups(mixer, name);
            if (groups.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=mixer_group_count group={name} count={groups.Length}");
            }

            return groups[0];
        }

        private static AudioMixerGroup[] FindExactGroups(
            AudioMixer mixer,
            string name)
        {
            return mixer.FindMatchingGroups(string.Empty)
                .Where(group => group.name == name)
                .ToArray();
        }

        private static Type RequireEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            return type ?? throw new InvalidOperationException(
                $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=editor_type_missing type={fullName}");
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=prefab_missing path={path}");
            }

            try
            {
                edit(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_GAME_AUDIO_FOUNDATION_FAILED reason=prefab_save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
