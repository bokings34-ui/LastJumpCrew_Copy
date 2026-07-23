#if UNITY_EDITOR
using System;
using System.IO;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.External;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSExternalSolarFlarePresentationAuthoring
    {
        private const string RootFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/Events/External";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string AudioFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "06. Audio/External/CC0/SolarFlare";
        private const string MixerPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "06. Audio/PHS_GameAudio.mixer";
        private const string PrefabPath =
            RootFolder + "/PHS_ExternalSolarFlarePresentation.prefab";
        private const string SoftParticlePath =
            MaterialFolder + "/PHS_SolarFlareSoftParticle.asset";
        private const string AdditiveMaterialPath =
            MaterialFolder + "/PHS_SolarFlare_Additive.mat";
        private const string AlphaMaterialPath =
            MaterialFolder + "/PHS_SolarFlare_Alpha.mat";
        private const string TelegraphVolumeProfilePath =
            MaterialFolder + "/PHS_SolarFlare_TelegraphVolume.asset";
        private const string ActiveVolumeProfilePath =
            MaterialFolder + "/PHS_SolarFlare_ActiveVolume.asset";
        private const string TelegraphAudioPath =
            AudioFolder + "/PHS_SolarFlare_Telegraph_Alarm_CC0.ogg";
        private const string ActiveAudioPath =
            AudioFolder + "/PHS_SolarFlare_Active_Urgent_CC0.mp3";
        private const string ResolveAudioPath =
            AudioFolder + "/PHS_SolarFlare_Resolve_PowerOff_CC0.ogg";
        private const string FailAudioPath =
            AudioFolder + "/PHS_SolarFlare_Fail_MetalImpact_CC0.wav";

        private static readonly Color SolarOrange =
            new(1f, 0.28f, 0.035f, 1f);
        private static readonly Color SolarGold =
            new(1f, 0.78f, 0.18f, 1f);
        private static readonly Color WarningRed =
            new(1f, 0.055f, 0.015f, 1f);

        [MenuItem(
            "Tools/ParkHanSol/BEAVER/Author External Solar Flare Presentation")]
        public static void Author()
        {
            EnsureFolders();
            var texture = EnsureSoftParticleTexture();
            var additive = EnsureParticleMaterial(
                AdditiveMaterialPath,
                texture,
                true);
            var alpha = EnsureParticleMaterial(
                AlphaMaterialPath,
                texture,
                false);
            var telegraphVolumeProfile = EnsureVolumeProfile(
                TelegraphVolumeProfilePath,
                0.18f,
                1.45f,
                0.16f,
                new Color(1f, 0.82f, 0.68f, 1f));
            var activeVolumeProfile = EnsureVolumeProfile(
                ActiveVolumeProfilePath,
                0.38f,
                2.4f,
                0.26f,
                new Color(1f, 0.7f, 0.48f, 1f));
            ConfigureAudioImporter(TelegraphAudioPath, false);
            ConfigureAudioImporter(ActiveAudioPath, true);
            ConfigureAudioImporter(ResolveAudioPath, false);
            ConfigureAudioImporter(FailAudioPath, false);
            var telegraphAudioClip = RequireAudioClip(
                TelegraphAudioPath);
            var activeAudioClip = RequireAudioClip(ActiveAudioPath);
            var resolveAudioClip = RequireAudioClip(ResolveAudioPath);
            var failAudioClip = RequireAudioClip(FailAudioPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var uiMixerGroup = RequireMixerGroup(mixer, "UI");
            var sfxMixerGroup = RequireMixerGroup(mixer, "SFX");
            var ambientMixerGroup = RequireMixerGroup(mixer, "Ambient");

            var root = BuildRoot(
                additive,
                alpha,
                telegraphVolumeProfile,
                activeVolumeProfile,
                telegraphAudioClip,
                activeAudioClip,
                resolveAudioClip,
                failAudioClip,
                uiMixerGroup,
                sfxMixerGroup,
                ambientMixerGroup);
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath,
                    out var success);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException(
                        "solar_flare_prefab_save_failed");
                }

                if (!ValidatePrefab(prefab, out var reason))
                {
                    throw new InvalidOperationException(
                        $"solar_flare_prefab_invalid:{reason}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"PHS_EXTERNAL_SOLAR_FLARE_AUTHOR_OK path={PrefabPath} " +
                    "network_gameplay_components=0 " +
                    "online_binding=blocked_event_id_unassigned");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem(
            "Tools/ParkHanSol/BEAVER/Validate External Solar Flare Presentation")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!ValidatePrefab(prefab, out var reason))
            {
                throw new InvalidOperationException(
                    $"solar_flare_prefab_validation_failed:{reason}");
            }

            Debug.Log(
                "PHS_EXTERNAL_SOLAR_FLARE_VALIDATE_OK " +
                $"path={PrefabPath} network_gameplay_components=0 " +
                "online_binding=blocked_event_id_unassigned");
        }

        private static GameObject BuildRoot(
            Material additive,
            Material alpha,
            VolumeProfile telegraphVolumeProfile,
            VolumeProfile activeVolumeProfile,
            AudioClip telegraphAudioClip,
            AudioClip activeAudioClip,
            AudioClip resolveAudioClip,
            AudioClip failAudioClip,
            AudioMixerGroup uiMixerGroup,
            AudioMixerGroup sfxMixerGroup,
            AudioMixerGroup ambientMixerGroup)
        {
            var root = new GameObject("PHS_ExternalSolarFlarePresentation");
            var presentationRoot = CreateChild(
                root.transform,
                "PresentationRoot");

            var telegraphSocket = CreateChild(
                presentationRoot,
                "TelegraphSocket");
            CreateChild(telegraphSocket, "SolarOriginAnchor");
            CreateCoronaRibbon(
                CreateChild(telegraphSocket, "CoronaRibbonVfx"),
                additive);
            CreateDirectionMarker(
                CreateChild(telegraphSocket, "DirectionMarkerVfx"),
                additive);
            CreateWarningLight(
                CreateChild(
                    telegraphSocket,
                    "InteriorWarningLightRoot"),
                SolarOrange,
                2.5f,
                12f);
            CreateVolume(
                CreateChild(telegraphSocket, "TelegraphVolume"),
                telegraphVolumeProfile,
                0.55f,
                140f);

            var activeSocket = CreateChild(
                presentationRoot,
                "ActiveSocket");
            CreateSolarWave(
                CreateChild(activeSocket, "DirectionalSolarWaveVfx"),
                additive);
            CreateHeatShimmer(
                CreateChild(activeSocket, "HullHeatShimmerVfx"),
                alpha);
            CreateWarningLight(
                CreateChild(activeSocket, "ActiveExposureLight"),
                SolarGold,
                3.2f,
                18f);
            CreateVolume(
                CreateChild(activeSocket, "ActiveExposureVolume"),
                activeVolumeProfile,
                0.72f,
                141f);

            var resolveSocket = CreateChild(
                presentationRoot,
                "ResolveSocket");
            CreateBurst(
                CreateChild(resolveSocket, "CoolDownBurstVfx"),
                alpha,
                new Color(0.28f, 0.78f, 1f, 0.75f),
                26,
                2.2f);

            var failSocket = CreateChild(
                presentationRoot,
                "FailSocket");
            CreateWarningLight(
                CreateChild(failSocket, "HullFlashVfx"),
                WarningRed,
                5.5f,
                22f);
            CreateBurst(
                CreateChild(failSocket, "SparkBurstVfx"),
                additive,
                SolarOrange,
                48,
                4.5f);

            var audioRoot = CreateChild(root.transform, "AudioRoot");
            var telegraphAudio = CreateAudioSource(
                audioRoot,
                "TelegraphAudio",
                telegraphAudioClip,
                uiMixerGroup,
                false,
                0.72f);
            var activeAudio = CreateAudioSource(
                audioRoot,
                "ActiveAudio",
                activeAudioClip,
                ambientMixerGroup,
                true,
                0.62f);
            var resolveAudio = CreateAudioSource(
                audioRoot,
                "ResolveAudio",
                resolveAudioClip,
                sfxMixerGroup,
                false,
                0.68f);
            var failAudio = CreateAudioSource(
                audioRoot,
                "FailAudio",
                failAudioClip,
                sfxMixerGroup,
                false,
                0.82f);

            CreateChild(root.transform, "HudAnchor");
            var cleanupRoot = CreateChild(root.transform, "CleanupRoot");

            telegraphSocket.gameObject.SetActive(false);
            activeSocket.gameObject.SetActive(false);
            resolveSocket.gameObject.SetActive(false);
            failSocket.gameObject.SetActive(false);

            var view = root.AddComponent<
                PHSExternalSolarFlarePresentationView>();
            var serializedView = new SerializedObject(view);
            SetReference(
                serializedView,
                "telegraphSocket",
                telegraphSocket.gameObject);
            SetReference(
                serializedView,
                "activeSocket",
                activeSocket.gameObject);
            SetReference(
                serializedView,
                "resolveSocket",
                resolveSocket.gameObject);
            SetReference(
                serializedView,
                "failSocket",
                failSocket.gameObject);
            SetReference(serializedView, "cleanupRoot", cleanupRoot);
            SetReference(
                serializedView,
                "telegraphAudioSource",
                telegraphAudio);
            SetReference(
                serializedView,
                "activeAudioSource",
                activeAudio);
            SetReference(
                serializedView,
                "resolveAudioSource",
                resolveAudio);
            SetReference(
                serializedView,
                "failAudioSource",
                failAudio);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var binder = root.AddComponent<
                PHSExternalEventPresentationBinder>();
            var serializedBinder = new SerializedObject(binder);
            SetReference(
                serializedBinder,
                "presentationViewBehaviour",
                view);
            serializedBinder.FindProperty("eventIdValue").intValue = 0;
            serializedBinder.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void CreateCoronaRibbon(
            Transform target,
            Material material)
        {
            var particles = target.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                SolarOrange,
                SolarGold);
            main.maxParticles = 96;

            var emission = particles.emission;
            emission.rateOverTime = 34f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 2.6f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.8f;
        }

        private static void CreateDirectionMarker(
            Transform target,
            Material material)
        {
            target.localPosition = new Vector3(0f, 0f, 2.5f);
            var particles = target.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.8f;
            main.startSpeed = 6.5f;
            main.startSize = 0.32f;
            main.startColor = SolarGold;
            main.maxParticles = 24;

            var emission = particles.emission;
            emission.rateOverTime = 12f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.1f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
        }

        private static void CreateSolarWave(
            Transform target,
            Material material)
        {
            target.localPosition = new Vector3(0f, 0f, -4f);
            var particles = target.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                SolarOrange,
                new Color(1f, 0.92f, 0.46f, 0.8f));
            main.maxParticles = 180;

            var emission = particles.emission;
            emission.rateOverTime = 72f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 8f, 0.25f);

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(SolarGold);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 5.5f;
        }

        private static void CreateHeatShimmer(
            Transform target,
            Material material)
        {
            var particles = target.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
            main.startColor = new Color(1f, 0.28f, 0.06f, 0.18f);
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.rateOverTime = 18f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 5f, 4f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
        }

        private static void CreateBurst(
            Transform target,
            Material material,
            Color color,
            short count,
            float speed)
        {
            var particles = target.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                speed * 0.45f,
                speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.55f);
            main.startColor = color;
            main.maxParticles = count;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(
                new[] { new ParticleSystem.Burst(0f, count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeGradient(color);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
        }

        private static void CreateWarningLight(
            Transform target,
            Color color,
            float intensity,
            float range)
        {
            var light = target.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreateVolume(
            Transform target,
            VolumeProfile profile,
            float weight,
            float priority)
        {
            var volume = target.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.sharedProfile = profile;
        }

        private static AudioSource CreateAudioSource(
            Transform parent,
            string name,
            AudioClip clip,
            AudioMixerGroup mixerGroup,
            bool loop,
            float volume)
        {
            var target = CreateChild(parent, name);
            var source = target.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.outputAudioMixerGroup = mixerGroup;
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = loop ? 96 : 72;
            return source;
        }

        private static Gradient CreateFadeGradient(Color color)
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.12f),
                    new GradientAlphaKey(color.a, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }

        private static bool ValidatePrefab(
            GameObject prefab,
            out string reason)
        {
            if (prefab == null)
            {
                reason = "prefab_missing";
                return false;
            }

            var requiredPaths = new[]
            {
                "PresentationRoot/TelegraphSocket/SolarOriginAnchor",
                "PresentationRoot/TelegraphSocket/CoronaRibbonVfx",
                "PresentationRoot/TelegraphSocket/DirectionMarkerVfx",
                "PresentationRoot/TelegraphSocket/InteriorWarningLightRoot",
                "PresentationRoot/TelegraphSocket/TelegraphVolume",
                "PresentationRoot/ActiveSocket/DirectionalSolarWaveVfx",
                "PresentationRoot/ActiveSocket/HullHeatShimmerVfx",
                "PresentationRoot/ActiveSocket/ActiveExposureLight",
                "PresentationRoot/ActiveSocket/ActiveExposureVolume",
                "PresentationRoot/ResolveSocket/CoolDownBurstVfx",
                "PresentationRoot/FailSocket/HullFlashVfx",
                "PresentationRoot/FailSocket/SparkBurstVfx",
                "AudioRoot/TelegraphAudio",
                "AudioRoot/ActiveAudio",
                "AudioRoot/ResolveAudio",
                "AudioRoot/FailAudio",
                "HudAnchor",
                "CleanupRoot"
            };
            foreach (var path in requiredPaths)
            {
                if (prefab.transform.Find(path) == null)
                {
                    reason = $"required_path_missing:{path}";
                    return false;
                }
            }

            var view = prefab.GetComponent<
                PHSExternalSolarFlarePresentationView>();
            if (view == null || !view.HasCompleteWiring)
            {
                reason = "presentation_view_wiring_invalid";
                return false;
            }

            var binder = prefab.GetComponent<
                PHSExternalEventPresentationBinder>();
            if (binder == null
                || !binder.HasViewWiring
                || binder.EventIdValue != 0)
            {
                reason = "unassigned_event_binder_safety_contract_invalid";
                return false;
            }

            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length < 6
                || prefab.GetComponentsInChildren<Light>(true).Length < 3
                || prefab.GetComponentsInChildren<Volume>(true).Length != 2
                || prefab.GetComponentsInChildren<AudioSource>(true).Length != 4)
            {
                reason = "presentation_payload_incomplete";
                return false;
            }

            var approvedMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
                MixerPath);
            foreach (var audioSource in
                     prefab.GetComponentsInChildren<AudioSource>(true))
            {
                if (audioSource.clip == null)
                {
                    reason = $"audio_clip_missing:{audioSource.name}";
                    return false;
                }

                var audioPath = AssetDatabase.GetAssetPath(audioSource.clip);
                if (!audioPath.StartsWith(
                        AudioFolder + "/",
                        StringComparison.Ordinal))
                {
                    reason = $"audio_clip_outside_approved_folder:{audioPath}";
                    return false;
                }

                if (audioSource.clip.frequency < 44100
                    || audioSource.clip.channels < 1
                    || audioSource.clip.channels > 2
                    || audioSource.clip.length < 0.2f)
                {
                    reason =
                        $"audio_clip_technical_contract:{audioSource.name}:" +
                        $"length={audioSource.clip.length}:" +
                        $"frequency={audioSource.clip.frequency}:" +
                        $"channels={audioSource.clip.channels}";
                    return false;
                }

                var expectedGroup = audioSource.name switch
                {
                    "TelegraphAudio" => "UI",
                    "ActiveAudio" => "Ambient",
                    _ => "SFX"
                };
                if (audioSource.outputAudioMixerGroup == null
                    || audioSource.outputAudioMixerGroup.audioMixer
                    != approvedMixer
                    || audioSource.outputAudioMixerGroup.name != expectedGroup)
                {
                    reason =
                        $"audio_mixer_group_invalid:{audioSource.name}:" +
                        $"expected={expectedGroup}:" +
                        $"actual={audioSource.outputAudioMixerGroup?.name ?? "null"}";
                    return false;
                }

                var importer = AssetImporter.GetAtPath(audioPath)
                    as AudioImporter;
                if (importer == null)
                {
                    reason = $"audio_importer_missing:{audioPath}";
                    return false;
                }

                var importerSettings = importer.defaultSampleSettings;
                var expectedLongLoop = audioSource.name == "ActiveAudio";
                var expectedLoadType = expectedLongLoop
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
                var expectedCompression = expectedLongLoop
                    ? AudioCompressionFormat.Vorbis
                    : AudioCompressionFormat.PCM;
                if (importer.forceToMono
                    || importer.loadInBackground
                    || !importerSettings.preloadAudioData
                    || importerSettings.loadType != expectedLoadType
                    || importerSettings.compressionFormat
                    != expectedCompression
                    || importerSettings.sampleRateSetting
                    != AudioSampleRateSetting.PreserveSampleRate)
                {
                    reason =
                        $"audio_import_contract_invalid:{audioSource.name}:" +
                        $"path={audioPath}";
                    return false;
                }
            }

            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<NetworkObject>(true) != null)
            {
                reason = "gameplay_or_network_component_found";
                return false;
            }

            foreach (var component in
                     prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    reason = "missing_script_found";
                    return false;
                }

                var typeNamespace = component.GetType().Namespace;
                if (!string.IsNullOrEmpty(typeNamespace)
                    && typeNamespace.StartsWith(
                        "Unity.Netcode",
                        StringComparison.Ordinal))
                {
                    reason = $"netcode_component_found:{component.GetType().Name}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static VolumeProfile EnsureVolumeProfile(
            string path,
            float postExposure,
            float bloomIntensity,
            float vignetteIntensity,
            Color colorFilter)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }

            bloom.active = true;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.62f);

            if (!profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(postExposure);
            colorAdjustments.colorFilter.Override(colorFilter);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.active = true;
            vignette.color.Override(new Color(0.24f, 0.02f, 0f, 1f));
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.62f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static AudioClip RequireAudioClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"solar_flare_audio_clip_missing:path={path}");
            }

            return clip;
        }

        private static AudioMixerGroup RequireMixerGroup(
            AudioMixer mixer,
            string groupName)
        {
            if (mixer == null)
            {
                throw new InvalidOperationException(
                    $"solar_flare_audio_mixer_missing:path={MixerPath}");
            }

            var groups = mixer.FindMatchingGroups(groupName);
            foreach (var group in groups)
            {
                if (group.name == groupName)
                {
                    return group;
                }
            }

            throw new InvalidOperationException(
                $"solar_flare_audio_mixer_group_missing:group={groupName}");
        }

        private static void ConfigureAudioImporter(
            string path,
            bool longLoop)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"solar_flare_audio_importer_missing:path={path}");
            }

            var expectedSettings = importer.defaultSampleSettings;
            expectedSettings.loadType = longLoop
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            expectedSettings.compressionFormat = longLoop
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.PCM;
            expectedSettings.quality = longLoop ? 0.72f : 1f;
            expectedSettings.sampleRateSetting =
                AudioSampleRateSetting.PreserveSampleRate;
            expectedSettings.preloadAudioData = true;

            var currentSettings = importer.defaultSampleSettings;
            var changed = importer.forceToMono
                || importer.loadInBackground
                || !currentSettings.preloadAudioData
                || currentSettings.loadType != expectedSettings.loadType
                || currentSettings.compressionFormat
                != expectedSettings.compressionFormat
                || !Mathf.Approximately(
                    currentSettings.quality,
                    expectedSettings.quality)
                || currentSettings.sampleRateSetting
                != expectedSettings.sampleRateSetting;
            if (!changed)
            {
                return;
            }

            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = expectedSettings;
            importer.SaveAndReimport();
        }

        private static Texture2D EnsureSoftParticleTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                SoftParticlePath);
            if (texture == null)
            {
                texture = new Texture2D(
                    64,
                    64,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "PHS_SolarFlareSoftParticle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, SoftParticlePath);
            }

            var pixels = new Color32[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var normalizedX = ((x + 0.5f) / 64f - 0.5f) * 2f;
                    var normalizedY = ((y + 0.5f) / 64f - 0.5f) * 2f;
                    var radius = Mathf.Sqrt(
                        normalizedX * normalizedX
                        + normalizedY * normalizedY);
                    var alpha = Mathf.Clamp01(1f - radius);
                    alpha *= alpha;
                    pixels[y * 64 + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material EnsureParticleMaterial(
            string path,
            Texture texture,
            bool additive)
        {
            var shader = Shader.Find(
                             "Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "solar_flare_particle_shader_missing");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path)
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfPresent(material, "_BaseMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
            SetFloatIfPresent(
                material,
                "_SrcBlend",
                (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(
                material,
                "_DstBlend",
                additive
                    ? (float)BlendMode.One
                    : (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            var current = "Assets";
            foreach (var segment in RootFolder.Substring(7).Split('/'))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(RootFolder, "Materials");
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void SetReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"serialized_property_missing:{propertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static void SetTextureIfPresent(
            Material material,
            string propertyName,
            Texture value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private static void SetColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
#endif
