#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    // Active fire presentation intentionally contains flame. Extinguisher smoke is
    // owned by the held-item repair feedback prefab, never by the incident visual.
    public static class PHSFireSmokeColumnAuthoring
    {
        private const string FirePresentationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "Props/Prefabs/EventPresentation/PHS_FireEventPresentation.prefab";
        private const string TeamFirePresentationPrefabPath =
            "Assets/04. NohSeokMin_Game Event/07_UseAssets/Modular Dark Wizard/" +
            "Prefabs/Fire (+ Torso)/FX Fire Orange.prefab";
        private const string ActiveFlameName = "PHS_TeamFireVisual";
        private const float FloorLift = 0.12f;
        // Preserve the source fire silhouette at ship-scale.  The prior 0.55
        // override reduced both team particle systems to a marker-sized flame.
        private const float FlameStartSize = 1.35f;

        [MenuItem("Tools/ParkHanSol/BEAVER/Restore Active Fire Flame")]
        public static void Author()
        {
            var flamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TeamFirePresentationPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_ACTIVE_FIRE_AUTHOR_FAILED reason=team_fire_prefab_missing");
            var root = PrefabUtility.LoadPrefabContents(FirePresentationPrefabPath);
            try
            {
                var view = root.GetComponent<EventEffectPresentationView>()
                    ?? throw new InvalidOperationException(
                        "PHS_ACTIVE_FIRE_AUTHOR_FAILED reason=presentation_view_missing");
                foreach (Transform child in root.transform)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var flame = PrefabUtility.InstantiatePrefab(
                    flamePrefab, root.transform) as GameObject;
                if (flame == null)
                {
                    throw new InvalidOperationException(
                        "PHS_ACTIVE_FIRE_AUTHOR_FAILED reason=flame_instantiate_failed");
                }

                flame.name = ActiveFlameName;
                flame.transform.localPosition = Vector3.up * FloorLift;
                flame.transform.localRotation = Quaternion.identity;
                flame.transform.localScale = Vector3.one;
                foreach (var behaviour in flame.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }

                foreach (var collider in flame.GetComponentsInChildren<Collider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
                foreach (var particle in flame.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.loop = true;
                    main.prewarm = true;
                    main.playOnAwake = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.startLifetime = 1.1f;
                    main.startSpeed = 1.2f;
                    main.startSize = FlameStartSize;
                    var emission = particle.emission;
                    emission.enabled = true;
                    emission.rateOverTime = 26f;
                }
                var trigger = root.GetComponent<BoxCollider>();
                if (trigger != null)
                {
                    trigger.size = new Vector3(2.6f, 2.6f, 2.6f);
                    trigger.center = new Vector3(0f, 1.3f, 0f);
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, FirePresentationPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_ACTIVE_FIRE_AUTHOR_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_ACTIVE_FIRE_AUTHOR_OK source=team_fire_presentation floor_lift=0.12");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Active Fire Flame")]
        public static void Validate()
        {
            var root = PrefabUtility.LoadPrefabContents(FirePresentationPrefabPath);
            try
            {
                var view = root.GetComponent<EventEffectPresentationView>();
                var flame = root.transform.Find(ActiveFlameName);
                var renderers = flame == null
                    ? Array.Empty<Renderer>()
                    : flame.GetComponentsInChildren<Renderer>(true);
                var particles = flame == null
                    ? Array.Empty<ParticleSystem>()
                    : flame.GetComponentsInChildren<ParticleSystem>(true);
                var trigger = root.GetComponent<BoxCollider>();
                var materialsValid = renderers.All(renderer => renderer.sharedMaterial != null
                    && renderer.sharedMaterial.shader != null
                    && renderer.sharedMaterial.shader.isSupported);
                var particleSizeValid = particles.Length > 0
                    && particles.All(particle =>
                        Mathf.Abs(particle.main.startSize.constant - FlameStartSize) <= 0.001f);
                if (view == null || flame == null || renderers.Length == 0
                    || Mathf.Abs(flame.localPosition.y - FloorLift) > 0.001f
                    || trigger == null || !trigger.isTrigger || !materialsValid
                    || !particleSizeValid)
                {
                    throw new InvalidOperationException(
                        $"PHS_ACTIVE_FIRE_VALIDATE_FAILED view={view != null} flame={flame != null} " +
                        $"renderers={renderers.Length} particles={particles.Length} " +
                        $"materials={materialsValid} particleSize={particleSizeValid} " +
                        $"expectedStartSize={FlameStartSize:F2} " +
                        $"floorLift={(flame == null ? -1f : flame.localPosition.y)} " +
                        $"trigger={trigger != null && trigger.isTrigger}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("PHS_ACTIVE_FIRE_VALIDATED source=team_fire_presentation floor_lift=0.12");
        }
    }
}
#endif
