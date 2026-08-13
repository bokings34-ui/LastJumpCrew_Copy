#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSHullBreachTeamSiteAuthoring
    {
        private const string SitePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "Props/Prefabs/EventPresentation/PHS_HullBreachTeamSite.prefab";
        private const string TeamBreachSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "ShipAccidents/Presentation/PHS_HullBreach_Presentation.prefab";
        private const string TeamSupportedParticleMaterialPath =
            "Assets/04. NohSeokMin_Game Event/07_UseAssets/FireEffect/Fire.mat";
        internal const string HullParticleMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "ShipAccidents/Presentation/PHS_HullBreachPurpleURP.mat";
        private const string RepairPointName = "PHS_HullRepairPoint";
        private const string PresentationRootName = "PHS_HullBreachVisual";
        internal const float SuctionPullRadius = 15f;
        internal const float SuctionStopDistance = 0.85f;
        internal const float SuctionPullAcceleration = 36f;
        internal const float SuctionMaximumPullSpeed = 3f;
        internal const float SuctionActiveDuration = 5f;
        internal static readonly int SuctionPlayerLayers =
            1 << LayerMask.NameToLayer("Player");
        internal const int SuctionObstructionLayers = 512;

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Team Hull Breach Site")]
        public static void Author()
        {
            var hullSource = AssetDatabase.LoadAssetAtPath<GameObject>(TeamBreachSourcePrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_HULL_SITE_AUTHOR_FAILED reason=team_hull_prefab_missing");
            var hullParticleMaterial = GetOrCreateHullParticleMaterial();
            var root = new GameObject("PHS_HullBreachTeamSite");
            try
            {
                var bounds = root.AddComponent<BoxCollider>();
                bounds.isTrigger = true;
                bounds.size = new Vector3(3.5f, 2.5f, 4f);
                bounds.center = new Vector3(0f, 1f, 1f);

                var repairPoint = new GameObject(RepairPointName).transform;
                repairPoint.SetParent(root.transform, false);
                repairPoint.localPosition = Vector3.zero;

                var presentation = new GameObject(PresentationRootName).transform;
                presentation.SetParent(root.transform, false);
                presentation.localPosition = Vector3.zero;
                var sourceRoot = PrefabUtility.InstantiatePrefab(hullSource, presentation)
                    as GameObject;
                if (sourceRoot == null)
                {
                    throw new InvalidOperationException(
                        "PHS_HULL_SITE_AUTHOR_FAILED reason=team_breach_instantiate_failed");
                }

                PrefabUtility.UnpackPrefabInstance(sourceRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var spray = sourceRoot;
                spray.name = "PHS_TeamHullBreachVisual";
                spray.transform.localPosition = Vector3.up * 0.22f;
                spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                RemoveGameplayComponents(spray);
                foreach (var particle in spray.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.loop = true;
                    main.prewarm = true;
                    main.playOnAwake = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.startColor = new Color(0.73f, 0.23f, 1f, 0.86f);
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = hullParticleMaterial;
                        renderer.enabled = true;
                    }
                }

                presentation.gameObject.SetActive(false);
                var site = root.AddComponent<PHSHullBreachRepairSite>();
                var serialized = new SerializedObject(site);
                serialized.FindProperty("siteId").stringValue = "hull_breach_site";
                serialized.FindProperty("repairPoint").objectReferenceValue = repairPoint;
                serialized.FindProperty("repairBounds").objectReferenceValue = bounds;
                serialized.FindProperty("presentationRoot").objectReferenceValue = presentation;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var suction = root.AddComponent<PHSHullBreachSuctionVolume>();
                ConfigureSuction(suction, repairPoint);

                if (PrefabUtility.SaveAsPrefabAsset(root, SitePrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_HULL_SITE_AUTHOR_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                "PHS_HULL_SITE_AUTHOR_OK owner=team_event " +
                "repair_point=shared_vfx_root marker=shared source=TeamBreachNode");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Team Hull Breach Site")]
        public static void Validate()
        {
            var root = PrefabUtility.LoadPrefabContents(SitePrefabPath);
            try
            {
                var site = root.GetComponent<PHSHullBreachRepairSite>();
                var repairPoint = root.transform.Find(RepairPointName);
                var presentation = root.transform.Find(PresentationRootName);
                var particles = presentation == null
                    ? Array.Empty<ParticleSystem>()
                    : presentation.GetComponentsInChildren<ParticleSystem>(true);
                var suction = root.GetComponent<PHSHullBreachSuctionVolume>();
                var valid = site != null
                    && site.TryValidate(out _)
                    && repairPoint != null
                    && presentation != null
                    && !presentation.gameObject.activeSelf
                    && particles.Length > 0
                    && Array.TrueForAll(
                        presentation.GetComponentsInChildren<ParticleSystemRenderer>(true),
                        renderer => renderer.sharedMaterial != null
                            && renderer.sharedMaterial.shader != null
                            && renderer.sharedMaterial.shader.isSupported
                            && AssetDatabase.GetAssetPath(renderer.sharedMaterial) == HullParticleMaterialPath)
                    && presentation.Find("PHS_TeamHullBreachVisual") != null
                    && HasExactSuctionContract(root, repairPoint, presentation, suction);
                if (!valid)
                {
                    throw new InvalidOperationException(
                        $"PHS_HULL_SITE_VALIDATE_FAILED site={site != null} " +
                        $"repairPoint={repairPoint != null} presentation={presentation != null} " +
                        $"particles={particles.Length} suction={suction != null}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                "PHS_HULL_SITE_VALIDATED owner=team_event repair_point=true " +
                "vfx=team_hull_breach marker=repair_point suction=server_site_single");
        }

        internal static bool HasExactSuctionContract(
            GameObject root,
            Transform repairPoint,
            Transform presentation,
            PHSHullBreachSuctionVolume suction)
        {
            if (root == null
                || repairPoint == null
                || presentation == null
                || suction == null
                || root.GetComponentsInChildren<PHSHullBreachSuctionVolume>(true).Length != 1
                || presentation.GetComponentsInChildren<PHSHullBreachSuctionVolume>(true).Length != 0)
            {
                return false;
            }

            var serialized = new SerializedObject(suction);
            return serialized.FindProperty("suctionCenter")?.objectReferenceValue == repairPoint
                && Mathf.Approximately(serialized.FindProperty("pullRadius").floatValue, SuctionPullRadius)
                && Mathf.Approximately(serialized.FindProperty("stopDistance").floatValue, SuctionStopDistance)
                && Mathf.Approximately(serialized.FindProperty("pullAcceleration").floatValue, SuctionPullAcceleration)
                && Mathf.Approximately(serialized.FindProperty("maximumPullSpeed").floatValue, SuctionMaximumPullSpeed)
                && Mathf.Approximately(serialized.FindProperty("pullActiveDuration").floatValue, SuctionActiveDuration)
                && serialized.FindProperty("playerLayers").intValue == SuctionPlayerLayers
                && serialized.FindProperty("obstructionLayers").intValue == SuctionObstructionLayers;
        }

        private static void ConfigureSuction(
            PHSHullBreachSuctionVolume suction,
            Transform repairPoint)
        {
            var serialized = new SerializedObject(suction);
            serialized.FindProperty("suctionCenter").objectReferenceValue = repairPoint;
            serialized.FindProperty("pullRadius").floatValue = SuctionPullRadius;
            serialized.FindProperty("stopDistance").floatValue = SuctionStopDistance;
            serialized.FindProperty("pullAcceleration").floatValue = SuctionPullAcceleration;
            serialized.FindProperty("maximumPullSpeed").floatValue = SuctionMaximumPullSpeed;
            serialized.FindProperty("pullActiveDuration").floatValue = SuctionActiveDuration;
            serialized.FindProperty("playerLayers").intValue = SuctionPlayerLayers;
            serialized.FindProperty("obstructionLayers").intValue = SuctionObstructionLayers;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void RemoveGameplayComponents(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        internal static Material GetOrCreateHullParticleMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(TeamSupportedParticleMaterialPath)
                ?? throw new InvalidOperationException(
                    "PHS_HULL_SITE_AUTHOR_FAILED reason=team_supported_particle_material_missing");
            if (AssetDatabase.LoadAssetAtPath<Material>(HullParticleMaterialPath) == null)
            {
                if (!AssetDatabase.CopyAsset(TeamSupportedParticleMaterialPath, HullParticleMaterialPath))
                {
                    throw new InvalidOperationException(
                        "PHS_HULL_SITE_AUTHOR_FAILED reason=hull_particle_material_copy_failed");
                }
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(HullParticleMaterialPath)
                ?? throw new InvalidOperationException(
                    "PHS_HULL_SITE_AUTHOR_FAILED reason=hull_particle_material_missing");
            material.name = "PHS_HullBreachPurpleURP";
            material.SetColor("_ColorIn", new Color(2.45f, 0.18f, 4.8f, 1f));
            material.SetColor("_ColorIn1", new Color(1.1f, 0.06f, 2.7f, 0f));
            material.SetColor("_ColorOut", new Color(0.34f, 0.015f, 0.8f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
