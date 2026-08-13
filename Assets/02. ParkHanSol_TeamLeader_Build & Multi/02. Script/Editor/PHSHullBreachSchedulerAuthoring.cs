#if UNITY_EDITOR
using System;
using System.IO;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSHullBreachSchedulerAuthoring
    {
        private const string RootPrefabPath = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string SitePrefabPath = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_HullBreachTeamSite.prefab";
        // No dedicated Hull prefab exists in the team repository.  Reuse the
        // event team's original breach-hole particle node, never the old PHS
        // accident presentation wrapper.
        private const string SourcePrefabPath = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/ShipAccidents/Presentation/PHS_HullBreach_Presentation.prefab";
        private const string PresentationPrefabPath = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_HullBreach_Presentation.prefab";
        private static readonly Color HullPurple = new(0.73f, 0.23f, 1f, 0.86f);

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Hull Breach Scheduler Runtime")]
        public static void Author()
        {
            PHSHullBreachTeamSiteAuthoring.Author();
            var site = AssetDatabase.LoadAssetAtPath<PHSHullBreachRepairSite>(SitePrefabPath)
                ?? throw new InvalidOperationException("PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=site_missing");
            var presentation = CreatePresentation();
            var prefab = PrefabUtility.LoadPrefabContents(RootPrefabPath);
            try
            {
                var coordinator = prefab.GetComponent<NetworkEventCoordinator>()
                    ?? throw new InvalidOperationException("PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=coordinator_missing");
                var presenter = prefab.GetComponentInChildren<NetworkEventEffectMirrorPresenter>(true)
                    ?? throw new InvalidOperationException("PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=presenter_missing");
                var runtime = prefab.GetComponent<PHSHullBreachRuntime>()
                    ?? prefab.AddComponent<PHSHullBreachRuntime>();
                Set(runtime, "coordinator", coordinator);
                Set(runtime, "sitePrefab", site);
                Set(coordinator, "hullBreachRuntime", runtime);
                Set(presenter, "hullBreachPresentationPrefab", presentation);
                PrefabUtility.SaveAsPrefabAsset(prefab, RootPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_HULL_SCHEDULER_AUTHOR_OK owner=team_event event=7107 vfx=effect_snapshot repair=foam_sealant");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Inspect Hull Breach Presentation Materials")]
        public static void InspectPresentationMaterials()
        {
            var presentation = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath)
                ?? throw new InvalidOperationException("PHS_HULL_MATERIAL_INSPECT_FAILED reason=presentation_missing");
            var invalidCount = 0;
            foreach (var renderer in presentation.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                var shader = material == null ? null : material.shader;
                var invalid = renderer.enabled && (material == null
                    || shader == null
                    || !shader.isSupported
                    || shader.name == "Hidden/InternalErrorShader");
                if (invalid)
                {
                    invalidCount++;
                }

                Debug.Log(
                    $"PHS_HULL_MATERIAL_INSPECT node={renderer.name} enabled={renderer.enabled} " +
                    $"material={(material == null ? "missing" : AssetDatabase.GetAssetPath(material))} " +
                    $"shader={(shader == null ? "missing" : shader.name)} " +
                    $"supported={(shader != null && shader.isSupported)} invalid={invalid}");
            }

            if (invalidCount > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_HULL_MATERIAL_INSPECT_FAILED reason=active_invalid_materials count={invalidCount}");
            }

            Debug.Log("PHS_HULL_MATERIAL_INSPECT_OK presentation=team_source active_invalid_materials=0");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Capture Hull Breach Material Proof")]
        public static void CaptureMaterialProof()
        {
            const string screenshotPath = "Assets/Screenshots/Visual_HullBreach_MaterialFixed.png";
            var presentation = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath)
                ?? throw new InvalidOperationException("PHS_HULL_CAPTURE_FAILED reason=presentation_missing");
            var preview = new PreviewRenderUtility();
            GameObject instance = null;
            var previousRenderTarget = RenderTexture.active;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(presentation) as GameObject
                    ?? throw new InvalidOperationException("PHS_HULL_CAPTURE_FAILED reason=presentation_instantiate_failed");
                instance.transform.localScale = Vector3.one * 0.22f;
                foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Simulate(1.15f, true, true, true);
                    particle.Play(true);
                }

                preview.AddSingleGO(instance);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.015f, 0.018f, 0.035f, 1f);
                preview.camera.transform.position = new Vector3(0f, 1.3f, -5.5f);
                preview.camera.transform.LookAt(new Vector3(0f, 0.35f, 0f));
                preview.camera.fieldOfView = 38f;
                preview.BeginPreview(new Rect(0f, 0f, 960f, 540f), GUIStyle.none);
                preview.camera.Render();

                var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
                RenderTexture.active = preview.camera.targetTexture;
                texture.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(screenshotPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                preview.EndPreview();
            }
            finally
            {
                RenderTexture.active = previousRenderTarget;
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                preview.Cleanup();
            }

            AssetDatabase.ImportAsset(screenshotPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"PHS_HULL_CAPTURE_OK path={screenshotPath} material=team_supported_purple");
        }

        public static void Validate()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath)
                ?? throw new InvalidOperationException("PHS_HULL_SCHEDULER_VALIDATE_FAILED reason=root_missing");
            var runtime = root.GetComponent<PHSHullBreachRuntime>();
            var coordinator = root.GetComponent<NetworkEventCoordinator>();
            var presenter = root.GetComponentInChildren<NetworkEventEffectMirrorPresenter>(true);
            var sitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SitePrefabPath);
            var siteRepairPoint = sitePrefab == null
                ? null
                : sitePrefab.transform.Find("PHS_HullRepairPoint");
            var sitePresentation = sitePrefab == null
                ? null
                : sitePrefab.transform.Find("PHS_HullBreachVisual");
            var siteSuction = sitePrefab == null
                ? null
                : sitePrefab.GetComponent<PHSHullBreachSuctionVolume>();
            var mirrorPresentation = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath);
            string reason = null;
            if (runtime == null || coordinator == null || presenter == null || !runtime.TryValidate(out reason)
                || sitePrefab == null
                || new SerializedObject(runtime).FindProperty("sitePrefab")?.objectReferenceValue
                    != sitePrefab.GetComponent<PHSHullBreachRepairSite>()
                || !PHSHullBreachTeamSiteAuthoring.HasExactSuctionContract(
                    sitePrefab,
                    siteRepairPoint,
                    sitePresentation,
                    siteSuction)
                || mirrorPresentation == null
                || mirrorPresentation.GetComponentsInChildren<PHSHullBreachSuctionVolume>(true).Length != 0
                || new SerializedObject(coordinator).FindProperty("hullBreachRuntime")?.objectReferenceValue != runtime
                || new SerializedObject(presenter).FindProperty("hullBreachPresentationPrefab")?.objectReferenceValue == null)
            {
                throw new InvalidOperationException($"PHS_HULL_SCHEDULER_VALIDATE_FAILED reason={reason ?? "inspector_reference_invalid"}");
            }

            Debug.Log("PHS_HULL_SCHEDULER_VALIDATE_OK event=7107 lifecycle=true effect_snapshot=true repair_target=true suction=server_site_single");
        }

        private static EventEffectPresentationView CreatePresentation()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath)
                ?? throw new InvalidOperationException("PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=team_breach_source_missing");
            var root = new GameObject("PHS_HullBreach_Presentation");
            try
            {
                var hullParticleMaterial = PHSHullBreachTeamSiteAuthoring.GetOrCreateHullParticleMaterial();
                var view = root.AddComponent<EventEffectPresentationView>();
                var sourceRoot = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
                if (sourceRoot == null) throw new InvalidOperationException("PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=presentation_instance_failed");
                PrefabUtility.UnpackPrefabInstance(sourceRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var spray = sourceRoot;
                spray.name = "PHS_TeamHullBreachVisual";
                spray.transform.localPosition = Vector3.up * 0.22f;
                spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                PHSHullBreachTeamSiteAuthoring.RemoveGameplayComponents(spray);
                foreach (var particle in spray.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.loop = true;
                    main.prewarm = true;
                    main.playOnAwake = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.startColor = HullPurple;
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = hullParticleMaterial;
                        renderer.enabled = true;
                    }
                    var shape = particle.shape;
                    shape.enabled = true;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                }
                var asset = PrefabUtility.SaveAsPrefabAsset(root, PresentationPrefabPath);
                return asset.GetComponent<EventEffectPresentationView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
        {
            var property = new SerializedObject(owner).FindProperty(name)
                ?? throw new InvalidOperationException($"PHS_HULL_SCHEDULER_AUTHOR_FAILED reason=property_missing:{name}");
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }
    }
}
#endif
