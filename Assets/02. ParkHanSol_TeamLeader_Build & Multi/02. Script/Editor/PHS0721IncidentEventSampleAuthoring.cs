#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHS0721IncidentEventSampleAuthoring
    {
        public const string SampleFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/Props/Prefabs/EventSamples";

        private static readonly SampleDefinition[] Definitions =
        {
            new(
                "PHS_FireEventContentSample",
                "Fire",
                "FireSurface",
                "FireExtinguisher",
                new Color(1f, 0.24f, 0.04f),
                true),
            new(
                "PHS_PowerFailureEventContentSample",
                "PowerFailure",
                "Device_PowerCore",
                "WireFix_or_Repair",
                new Color(0.18f, 0.55f, 1f),
                false),
            new(
                "PHS_DeviceFailureEventContentSample",
                "DeviceFailure",
                "Device_Engine",
                "Repair",
                new Color(1f, 0.7f, 0.16f),
                false),
            new(
                "PHS_HullBreachEventContentSample",
                "HullBreach",
                "HullSurface_ExteriorImpact",
                "Repair",
                new Color(0.5f, 0.82f, 1f),
                false),
            new(
                "PHS_SteamLeakEventContentSample",
                "SteamLeak",
                "Pipe_Valve",
                "Repair",
                new Color(0.72f, 0.86f, 0.9f),
                false),
            new(
                "PHS_OxygenFailureEventContentSample",
                "OxygenFailure",
                "Pipe_LifeSupport",
                "Wrench_or_Repair",
                new Color(0.24f, 0.92f, 0.92f),
                false),
            new(
                "PHS_GravityGeneratorFailureEventContentSample",
                "GravityGeneratorFailure",
                "Device_GravityGenerator",
                "Repair",
                new Color(0.62f, 0.32f, 1f),
                false),
            new(
                "PHS_EnemyScoutEventContentSample",
                "EnemyScout",
                "EnemyIngress",
                "PowerSync",
                new Color(1f, 0.12f, 0.18f),
                false),
            new(
                "PHS_MeteorAttackEventContentSample",
                "MeteorAttack",
                "HullSurface_ExteriorImpact",
                "Cannon",
                new Color(1f, 0.42f, 0.08f),
                false),
            new(
                "PHS_EmpAttackEventContentSample",
                "EmpAttack",
                "Device_Terminal",
                "WireFix",
                new Color(0.45f, 0.18f, 1f),
                false)
        };

        [MenuItem(
            "Tools/ParkHanSol/Build 0721 Incident Event Content Samples")]
        public static void BuildSamples()
        {
            EnsureSampleFolder();
            var created = new List<string>(Definitions.Length);
            foreach (var definition in Definitions)
            {
                var prefab = CreateSample(definition);
                if (!ValidateSamplePrefab(prefab, definition, out var reason))
                {
                    throw new InvalidOperationException(
                        $"incident_sample_invalid:{definition.DisplayName}:{reason}");
                }

                created.Add(AssetDatabase.GetAssetPath(prefab));
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"PHS_0721_INCIDENT_EVENT_SAMPLES_OK " +
                $"count={created.Count} folder={SampleFolder}");
        }

        [MenuItem(
            "Tools/ParkHanSol/Validate 0721 Incident Event Content Samples")]
        public static void ValidateSamples()
        {
            var errors = new List<string>();
            foreach (var definition in Definitions)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    GetSamplePath(definition));
                if (!ValidateSamplePrefab(prefab, definition, out var reason))
                {
                    errors.Add($"{definition.DisplayName}:{reason}");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "incident_sample_validation_failed:" +
                    string.Join("|", errors));
            }

            Debug.Log(
                $"PHS_0721_INCIDENT_EVENT_SAMPLE_VALIDATE_OK " +
                $"count={Definitions.Length}");
        }

        private static GameObject CreateSample(
            SampleDefinition definition)
        {
            var root = new GameObject(definition.FileName);
            try
            {
                CreateDeliveryContract(root.transform, definition);
                var presentationRoot = CreateChild(
                    root.transform,
                    "PresentationRoot");
                CreateChild(presentationRoot, "TelegraphSocket");
                var activeSocket = CreateChild(
                    presentationRoot,
                    "ActiveSocket");
                CreateChild(presentationRoot, "ResolveSocket");
                CreateChild(presentationRoot, "FailSocket");
                CreateChild(presentationRoot, "CleanupRoot");

                if (definition.UseFirePresentation)
                {
                    CreateFireReference(activeSocket);
                }
                else
                {
                    CreatePlaceholderVfx(activeSocket, definition.Color);
                }

                var samplePath = GetSamplePath(definition);
                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    samplePath,
                    out var success);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException(
                        $"incident_sample_save_failed:{definition.DisplayName}");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateDeliveryContract(
            Transform parent,
            SampleDefinition definition)
        {
            var contract = CreateChild(parent, "DeliveryContract");
            CreateChild(
                contract,
                $"ExpectedAnchor_{definition.AnchorName}");
            CreateChild(
                contract,
                $"RequiredTool_{definition.ResponseName}");
            CreateChild(contract, "NoNetworkComponents");
        }

        private static void CreateFireReference(Transform parent)
        {
            var source =
                PHS0720FirePresentationAuthoring
                    .EnsurePresentationPrefab();
            if (source == null)
            {
                throw new InvalidOperationException(
                    "fire_presentation_source_missing");
            }

            var instance = PrefabUtility.InstantiatePrefab(
                source,
                parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "fire_presentation_reference_create_failed");
            }

            instance.name = "Reference_FirePatchPresentation";
        }

        private static void CreatePlaceholderVfx(
            Transform parent,
            Color color)
        {
            var placeholder = CreateChild(parent, "ReplaceWithOwnVfx");
            var particleSystem = placeholder.gameObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startColor = color;
            main.maxParticles = 32;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.16f;

            var light = placeholder.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 1.2f;
            light.range = 2.2f;
        }

        private static bool ValidateSamplePrefab(
            GameObject prefab,
            SampleDefinition definition,
            out string reason)
        {
            if (prefab == null)
            {
                reason = "prefab_missing";
                return false;
            }

            if (prefab.name != definition.FileName)
            {
                reason = $"root_name_invalid:{prefab.name}";
                return false;
            }

            var requiredPaths = new[]
            {
                "DeliveryContract",
                "PresentationRoot",
                "PresentationRoot/TelegraphSocket",
                "PresentationRoot/ActiveSocket",
                "PresentationRoot/ResolveSocket",
                "PresentationRoot/FailSocket",
                "PresentationRoot/CleanupRoot"
            };
            foreach (var path in requiredPaths)
            {
                if (prefab.transform.Find(path) == null)
                {
                    reason = $"socket_missing:{path}";
                    return false;
                }
            }

            foreach (var component in
                     prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    reason = "missing_script_found";
                    return false;
                }

                var type = component.GetType();
                if (type.Namespace != null
                    && type.Namespace.StartsWith(
                        "Unity.Netcode",
                        StringComparison.Ordinal))
                {
                    reason = $"network_component_found:{type.Name}";
                    return false;
                }
            }

            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Rigidbody>(true) != null)
            {
                reason = "physics_component_found";
                return false;
            }

            var activeSocket = prefab.transform.Find(
                "PresentationRoot/ActiveSocket");
            if (definition.UseFirePresentation)
            {
                var fireReference = activeSocket.Find(
                    "Reference_FirePatchPresentation");
                if (fireReference == null
                    || fireReference.GetComponentsInChildren<ParticleSystem>(
                        true).Length != 4)
                {
                    reason = "fire_reference_invalid";
                    return false;
                }
            }
            else if (activeSocket.Find("ReplaceWithOwnVfx") == null)
            {
                reason = "placeholder_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private static Transform CreateChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsureSampleFolder()
        {
            if (!AssetDatabase.IsValidFolder(SampleFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
                    "03. Prefab/Props/Prefabs",
                    "EventSamples");
            }
        }

        private static string GetSamplePath(
            SampleDefinition definition)
        {
            return $"{SampleFolder}/{definition.FileName}.prefab";
        }

        private readonly struct SampleDefinition
        {
            public SampleDefinition(
                string fileName,
                string displayName,
                string anchorName,
                string responseName,
                Color color,
                bool useFirePresentation)
            {
                FileName = fileName;
                DisplayName = displayName;
                AnchorName = anchorName;
                ResponseName = responseName;
                Color = color;
                UseFirePresentation = useFirePresentation;
            }

            public string FileName { get; }
            public string DisplayName { get; }
            public string AnchorName { get; }
            public string ResponseName { get; }
            public Color Color { get; }
            public bool UseFirePresentation { get; }
        }
    }
}
#endif
