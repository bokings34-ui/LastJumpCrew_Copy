#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHS0720FirePresentationAuthoring
    {
        public const string PresentationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/Props/Prefabs/IncidentFire/" +
            "PHS_FirePatchPresentation.prefab";
        public const string TeamPresentationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/Props/Prefabs/IncidentFire/" +
            "PHS_TeamFirePatchPresentation.prefab";

        private const string AssetFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/Props/Prefabs/IncidentFire";
        private const string ParticleTexturePath =
            AssetFolder + "/PHS_FireSoftParticle.asset";
        private const string AdditiveMaterialPath =
            AssetFolder + "/PHS_FireAdditive.mat";
        private const string AlphaMaterialPath =
            AssetFolder + "/PHS_FireAlpha.mat";

        [MenuItem(
            "Tools/ParkHanSol/Build 0720 Fire Patch Presentation")]
        public static void BuildPresentation()
        {
            var prefab = EnsurePresentationPrefab();
            Debug.Log(
                $"PHS_0720_FIRE_PRESENTATION_OK " +
                $"path={AssetDatabase.GetAssetPath(prefab)}");
        }

        public static GameObject EnsurePresentationPrefab()
        {
            EnsureAssetFolder();
            var texture = EnsureParticleTexture();
            var additiveMaterial = EnsureParticleMaterial(
                AdditiveMaterialPath,
                texture,
                true);
            var alphaMaterial = EnsureParticleMaterial(
                AlphaMaterialPath,
                texture,
                false);

            var root = new GameObject(
                "PHS_FirePatchPresentation");
            try
            {
                CreateFlameSystem(
                    root.transform,
                    "FlameCore",
                    additiveMaterial,
                    new Color(1f, 0.78f, 0.12f, 0.95f),
                    new Color(1f, 0.12f, 0.01f, 0.05f),
                    22f,
                    0.45f,
                    0.9f,
                    0.22f,
                    0.52f,
                    1.3f);
                CreateFlameSystem(
                    root.transform,
                    "FlameOuter",
                    additiveMaterial,
                    new Color(1f, 0.22f, 0.015f, 0.7f),
                    new Color(0.32f, 0.015f, 0.005f, 0f),
                    12f,
                    0.7f,
                    1.25f,
                    0.42f,
                    0.88f,
                    0.8f);
                CreateSmokeSystem(
                    root.transform,
                    alphaMaterial);
                CreateEmberSystem(
                    root.transform,
                    additiveMaterial);

                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PresentationPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "fire_presentation_prefab_save_failed");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    PresentationPrefabPath,
                    ImportAssetOptions.ForceUpdate);
                var loadedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                    PresentationPrefabPath);
                if (loadedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "fire_presentation_prefab_reload_failed");
                }

                return loadedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static bool ValidatePresentationPrefab(
            GameObject prefab,
            out string reason)
        {
            if (prefab == null)
            {
                reason = "prefab_missing";
                return false;
            }

            if (string.Equals(
                    AssetDatabase.GetAssetPath(prefab),
                    TeamPresentationPrefabPath,
                    StringComparison.Ordinal))
            {
                return ValidateTeamPresentationPrefab(prefab, out reason);
            }

            if (!string.Equals(
                    AssetDatabase.GetAssetPath(prefab),
                    PresentationPrefabPath,
                    StringComparison.Ordinal))
            {
                reason = "prefab_path_invalid";
                return false;
            }

            var expectedParticleNames = new HashSet<string>(
                new[]
                {
                    "FlameCore",
                    "FlameOuter",
                    "Smoke",
                    "Embers"
                },
                StringComparer.Ordinal);
            var particleSystems =
                prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems.Length != expectedParticleNames.Count)
            {
                reason =
                    $"particle_layer_count_invalid:" +
                    $"{particleSystems.Length}";
                return false;
            }

            foreach (var particleSystem in particleSystems)
            {
                if (!expectedParticleNames.Remove(
                        particleSystem.name))
                {
                    reason =
                        $"particle_layer_name_invalid:" +
                        $"{particleSystem.name}";
                    return false;
                }

                var renderer =
                    particleSystem.GetComponent<
                        ParticleSystemRenderer>();
                if (renderer == null
                    || renderer.sharedMaterial == null)
                {
                    reason =
                        $"particle_material_missing:" +
                        $"{particleSystem.name}";
                    return false;
                }
            }

            if (expectedParticleNames.Count != 0)
            {
                reason =
                    $"particle_layer_missing:" +
                    $"{string.Join(",", expectedParticleNames)}";
                return false;
            }

            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<
                    Unity.Netcode.NetworkObject>(true) != null
                || prefab.GetComponentInChildren<
                    Unity.Netcode.NetworkBehaviour>(true) != null)
            {
                reason = "gameplay_or_network_component_found";
                return false;
            }

            foreach (var child in
                     prefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            child.gameObject) > 0)
                {
                    reason =
                        $"missing_script_found:{child.name}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public static GameObject LoadTeamPresentationPrefab()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                TeamPresentationPrefabPath);
        }

        private static bool ValidateTeamPresentationPrefab(
            GameObject prefab,
            out string reason)
        {
            var adapters = prefab.GetComponentsInChildren<
                PHSTeamFirePatchPresentationAdapter>(true);
            if (adapters.Length != 1)
            {
                reason = $"team_adapter_count_invalid:{adapters.Length}";
                return false;
            }

            if (!adapters[0].TryValidate(out var adapterReason))
            {
                reason = $"team_adapter_invalid:{adapterReason}";
                return false;
            }

            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<
                    Unity.Netcode.NetworkObject>(true) != null
                || prefab.GetComponentInChildren<
                    Unity.Netcode.NetworkBehaviour>(true) != null)
            {
                reason = "gameplay_or_network_component_found";
                return false;
            }

            foreach (var child in
                     prefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            child.gameObject) > 0)
                {
                    reason = $"missing_script_found:{child.name}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static void CreateFlameSystem(
            Transform parent,
            string name,
            Material material,
            Color startColor,
            Color endColor,
            float emissionRate,
            float minimumLifetime,
            float maximumLifetime,
            float minimumSize,
            float maximumSize,
            float upwardVelocity)
        {
            var particleSystem = CreateParticleSystem(
                parent,
                name,
                material,
                emissionRate,
                minimumLifetime,
                maximumLifetime,
                minimumSize,
                maximumSize,
                startColor,
                96);
            var main = particleSystem.main;
            main.startSize3D = true;
            main.startSizeX =
                new ParticleSystem.MinMaxCurve(
                    minimumSize * 0.72f,
                    maximumSize * 0.72f);
            main.startSizeY =
                new ParticleSystem.MinMaxCurve(
                    minimumSize * 1.35f,
                    maximumSize * 1.35f);
            main.startSizeZ =
                new ParticleSystem.MinMaxCurve(
                    minimumSize * 0.72f,
                    maximumSize * 0.72f);
            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.28f;
            shape.radiusThickness = 1f;
            shape.angle = 9f;
            shape.length = 0.08f;

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(
                upwardVelocity * 0.75f,
                upwardVelocity * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                CreateGradient(startColor, endColor));

            var size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 0.55f),
                    new Keyframe(1f, 0f)));
        }

        private static void CreateSmokeSystem(
            Transform parent,
            Material material)
        {
            var particleSystem = CreateParticleSystem(
                parent,
                "Smoke",
                material,
                5.5f,
                1.8f,
                3.2f,
                0.42f,
                0.9f,
                new Color(0.22f, 0.19f, 0.18f, 0.34f),
                48);
            particleSystem.transform.localPosition =
                new Vector3(0f, 0.28f, 0f);

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.24f;
            shape.radiusThickness = 1f;
            shape.angle = 14f;
            shape.length = 0.12f;

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.38f, 0.72f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);

            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                CreateGradient(
                    new Color(0.18f, 0.15f, 0.14f, 0.3f),
                    new Color(0.08f, 0.075f, 0.07f, 0f)));

            var size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.35f, 0.72f),
                    new Keyframe(1f, 1.5f)));
        }

        private static void CreateEmberSystem(
            Transform parent,
            Material material)
        {
            var particleSystem = CreateParticleSystem(
                parent,
                "Embers",
                material,
                4f,
                0.8f,
                1.8f,
                0.025f,
                0.07f,
                new Color(1f, 0.5f, 0.04f, 1f),
                40);
            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.32f;
            shape.radiusThickness = 1f;
            shape.angle = 24f;
            shape.length = 0.08f;

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                CreateGradient(
                    new Color(1f, 0.78f, 0.12f, 1f),
                    new Color(1f, 0.08f, 0.01f, 0f)));
        }

        private static ParticleSystem CreateParticleSystem(
            Transform parent,
            string name,
            Material material,
            float emissionRate,
            float minimumLifetime,
            float maximumLifetime,
            float minimumSize,
            float maximumSize,
            Color startColor,
            int maximumParticles)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var particleSystem =
                child.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace =
                ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                minimumLifetime,
                maximumLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                minimumSize,
                maximumSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                -Mathf.PI,
                Mathf.PI);
            main.startColor = startColor;
            main.maxParticles = maximumParticles;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.2f;
            noise.frequency = 0.65f;
            noise.scrollSpeed = 0.22f;

            var renderer =
                child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode =
                ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingFudge = 0.8f;
            return particleSystem;
        }

        private static Gradient CreateGradient(
            Color start,
            Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(
                        Color.Lerp(start, end, 0.45f),
                        0.55f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(
                        Mathf.Max(start.a, end.a),
                        0.35f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
        }

        private static Texture2D EnsureParticleTexture()
        {
            var texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    ParticleTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(
                    64,
                    64,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "PHS_FireSoftParticle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(
                    texture,
                    ParticleTexturePath);
            }
            else if (texture.width != 64 || texture.height != 64)
            {
                texture.Reinitialize(
                    64,
                    64,
                    TextureFormat.RGBA32,
                    false);
            }

            var pixels = new Color32[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var normalizedX =
                        ((x + 0.5f) / 64f - 0.5f) * 2f;
                    var normalizedY =
                        ((y + 0.5f) / 64f - 0.5f) * 2f;
                    var radius = Mathf.Sqrt(
                        normalizedX * normalizedX
                        + normalizedY * normalizedY * 1.28f);
                    var alpha = Mathf.Clamp01(1f - radius);
                    alpha = alpha * alpha
                        * (3f - 2f * alpha);
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
            Texture2D texture,
            bool additive)
        {
            var shader =
                Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "particle_shader_missing");
            }

            var material =
                AssetDatabase.LoadAssetAtPath<Material>(path);
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
            material.SetOverrideTag("RenderType", "Transparent");
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
            SetFloatIfPresent(
                material,
                "_SrcBlendAlpha",
                (float)BlendMode.One);
            SetFloatIfPresent(
                material,
                "_DstBlendAlpha",
                additive
                    ? (float)BlendMode.One
                    : (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureAssetFolder()
        {
            var current = "Assets";
            var segments = AssetFolder
                .Substring("Assets/".Length)
                .Split('/');
            foreach (var segment in segments)
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }

        private static void SetTextureIfPresent(
            Material material,
            string property,
            Texture texture)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetColorIfPresent(
            Material material,
            string property,
            Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
#endif
