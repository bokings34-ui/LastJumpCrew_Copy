#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSOxygenContinuousSprayAuthoring
    {
        private const string OxygenPresentationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "Props/Prefabs/EventPresentation/PHS_OxygenLeakPipePresentation.prefab";
        // Event team's original leak VFX.  The project has no team-owned
        // extinguisher particle prefab; do not borrow a PHS presentation here.
        private const string TeamOxygenSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "Items/Feedback/PHS_ExtinguisherFoamSpray.prefab";
        private const string SprayRootName = "PHS_OxygenContinuousBlueSpray";
        private const string SprayMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/05. Material/Items/Feedback/" +
            "PHS_OxygenExtinguisherBlue.mat";
        private const string SprayTexturePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/_ThirdParty/" +
            "PilotoStudio1_RuntimeSubset/Textures/Flare_SoftCross.png";
        private static readonly Color SprayBlue = new(0.12f, 0.78f, 1f, 0.8f);

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Oxygen Continuous Blue Spray")]
        public static void Author()
        {
            var oxygenSource = AssetDatabase.LoadAssetAtPath<GameObject>(TeamOxygenSourcePrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=team_oxygen_source_missing");
            var sprayMaterial = GetOrCreateBlueSprayMaterial();
            var root = PrefabUtility.LoadPrefabContents(OxygenPresentationPrefabPath);
            try
            {
                if (root.GetComponent<EventEffectPresentationView>() == null)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=presentation_view_missing");
                }

                if (root.GetComponent<Collider>() == null)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=repair_target_missing");
                }

                foreach (Transform child in root.transform)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var sourceRoot = PrefabUtility.InstantiatePrefab(oxygenSource, root.transform)
                    as GameObject;
                if (sourceRoot == null)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=team_oxygen_instantiate_failed");
                }

                PrefabUtility.UnpackPrefabInstance(
                    sourceRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                // The team effect is a composite: its flare, fuzz and trail nodes
                // are siblings.  Keeping only CenterMuzzle loses the render graph.
                var sprayRoot = sourceRoot;
                sprayRoot.name = SprayRootName;
                // Keep the repair/marker root on the socket. Lift only the copied
                // spray child so it is visible above the floor and direct its cone
                // upward through the particle shape below.
                sprayRoot.transform.localPosition = Vector3.up * 0.35f;
                sprayRoot.transform.localRotation = Quaternion.identity;
                sprayRoot.transform.localScale = Vector3.one;

                foreach (var behaviour in sprayRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
                foreach (var collider in sprayRoot.GetComponentsInChildren<Collider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                var particles = sprayRoot.GetComponentsInChildren<ParticleSystem>(true);
                if (particles.Length == 0)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=team_oxygen_particles_missing");
                }

                foreach (var particle in particles)
                {
                    var main = particle.main;
                    main.loop = true;
                    main.prewarm = true;
                    main.playOnAwake = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.startColor = SprayBlue;

                    var shape = particle.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.radius = 0.08f;
                    shape.angle = 14f;
                    // Aim the selected team jet away from its repair socket.
                    shape.rotation = new Vector3(-90f, 0f, 0f);

                    var emission = particle.emission;
                    emission.enabled = true;
                    emission.rateOverTime = particle.name.Contains("FoamMist")
                        ? 16f
                        : 40f;

                    var fade = particle.colorOverLifetime;
                    fade.enabled = true;
                    var gradient = new Gradient();
                    gradient.SetKeys(
                        new[]
                        {
                            new GradientColorKey(SprayBlue, 0f),
                            new GradientColorKey(SprayBlue, 0.65f),
                            new GradientColorKey(SprayBlue, 1f)
                        },
                        new[]
                        {
                            new GradientAlphaKey(0.15f, 0f),
                            new GradientAlphaKey(0.72f, 0.2f),
                            new GradientAlphaKey(0f, 1f)
                        });
                    fade.color = gradient;

                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        // The copied extinguisher VFX references a removed shader.
                        // Keep its particle layout, but use a valid URP particle material.
                        renderer.sharedMaterial = sprayMaterial;
                        renderer.enabled = true;
                        renderer.renderMode = particle.name.Contains("FoamMist")
                            ? ParticleSystemRenderMode.Billboard
                            : ParticleSystemRenderMode.Stretch;
                        renderer.velocityScale = 0.3f;
                        renderer.lengthScale = 2.2f;
                    }
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, OxygenPresentationPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                "PHS_OXYGEN_SPRAY_AUTHOR_OK source=ExtinguisherFoam " +
                "mode=continuous_blue_outward root=repair_target_marker");
        }

        private static Material GetOrCreateBlueSprayMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SprayMaterialPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SprayTexturePath)
                ?? throw new InvalidOperationException(
                    "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=soft_particle_texture_missing");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null || !shader.isSupported)
            {
                throw new InvalidOperationException(
                    "PHS_OXYGEN_SPRAY_AUTHOR_FAILED reason=urp_particle_shader_missing");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "PHS_OxygenExtinguisherBlue" };
                AssetDatabase.CreateAsset(material, SprayMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", SprayBlue);
            material.SetColor("_Color", SprayBlue);
            material.SetColor("_EmissionColor", SprayBlue * 1.5f);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", 5f); // SrcAlpha
            material.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Oxygen Continuous Blue Spray")]
        public static void Validate()
        {
            var root = PrefabUtility.LoadPrefabContents(OxygenPresentationPrefabPath);
            try
            {
                var spray = root.transform.Find(SprayRootName);
                var particles = spray == null
                    ? Array.Empty<ParticleSystem>()
                    : spray.GetComponentsInChildren<ParticleSystem>(true);
                var valid = root.GetComponent<EventEffectPresentationView>() != null
                    && root.GetComponent<Collider>() is { isTrigger: true }
                    && root.transform.childCount == 1
                    && spray != null
                    && particles.Length > 0
                    && particles.All(particle =>
                        particle.main.loop
                        && particle.main.prewarm
                        && particle.main.playOnAwake
                        && particle.main.simulationSpace
                            == ParticleSystemSimulationSpace.Local
                        && particle.emission.enabled
                        && particle.colorOverLifetime.enabled);
                if (!valid)
                {
                    throw new InvalidOperationException(
                        $"PHS_OXYGEN_SPRAY_VALIDATE_FAILED " +
                        $"view={root.GetComponent<EventEffectPresentationView>() != null} " +
                        $"repairTarget={root.GetComponent<Collider>() is { isTrigger: true }} " +
                        $"children={root.transform.childCount} particles={particles.Length}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                "PHS_OXYGEN_SPRAY_VALIDATED source=ExtinguisherFoam " +
                "continuous=true tint=blue root=repair_target_marker");
        }

    }
}
#endif
