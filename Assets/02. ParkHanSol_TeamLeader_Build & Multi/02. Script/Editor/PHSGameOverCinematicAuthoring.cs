#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSGameOverCinematicAuthoring
    {
        private const string MenuRoot = "Tools/ParkHanSol/Game Over/";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string PrefabFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/GameOver";
        private const string PrefabPath = PrefabFolder + "/PHS_GameOverCinematicPresentation.prefab";
        private const string MaterialFolder = PrefabFolder + "/Materials";
        private const string RunSessionRootPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";

        private const string CarrierPath =
            "Assets/SpecialSkillsEffectsPack/Models/AircraftCarrier_01.fbx";
        private const string HeroShipPath =
            "Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/Spaceship_SpaceCrew_Outside.prefab";
        private const string JetPath =
            "Assets/SpecialSkillsEffectsPack/Models/Jet_04.fbx";
        private const string BomberPath =
            "Assets/SpecialSkillsEffectsPack/Models/Bomber_02.fbx";
        private const string FleetArrivalPath =
            "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_46_CyberAirTroopInvader/Effect_46_Parts/Effect_46_CyberBomber.prefab";
        private const string HitEffectPath =
            "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_45_MultipleLaserCanon/Effect_45_Parts/Effect_45_HitEffect.prefab";
        private const string ImpactEffectPath =
            "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_45_MultipleLaserCanon/Effect_45_Parts/Effect_45_Impact.prefab";
        private const string ExplosionPath =
            "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_14_RuinExplosion/Effect_14_RuinExplosion.prefab";

        [MenuItem(MenuRoot + "Author Presentation")]
        public static void Author()
        {
            EnsureFolders();
            ValidateSourceAssets();
            var heroMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/PHS_GameOver_HeroShip.mat",
                new Color(0.08f, 0.3f, 0.58f, 1f),
                new Color(0.04f, 0.24f, 0.75f, 1f));
            var enemyMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/PHS_GameOver_EnemyFleet.mat",
                new Color(0.12f, 0.025f, 0.035f, 1f),
                new Color(0.85f, 0.015f, 0.01f, 1f));
            var barrageMaterial = CreateOrUpdateBeamMaterial(
                MaterialFolder + "/PHS_GameOver_BarrageBeam.mat");

            BuildPresentationPrefab(heroMaterial, enemyMaterial, barrageMaterial);
            WireRunSessionRoot();
            PlaceInMapScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOrThrow();
            Debug.Log($"PHS_GAME_OVER_AUTHOR_OK prefab={PrefabPath} scene={MapScenePath}");
        }

        [MenuItem(MenuRoot + "Validate Presentation")]
        public static void Validate()
        {
            ValidateOrThrow();
            Debug.Log($"PHS_GAME_OVER_VALIDATE_OK prefab={PrefabPath} scene={MapScenePath}");
        }

        [MenuItem(MenuRoot + "Repair Presentation Materials")]
        public static void RepairPresentationMaterials()
        {
            var heroMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/PHS_GameOver_HeroShip.mat",
                new Color(0.08f, 0.3f, 0.58f, 1f),
                new Color(0.04f, 0.24f, 0.75f, 1f));
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var heroRoot = prefabRoot.transform.Find("Presentation/HeroShipVisualRoot")
                    ?? throw new InvalidOperationException("Game over hero ship root missing.");
                ReplaceMaterials(heroRoot.gameObject, heroMaterial);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log($"PHS_GAME_OVER_MATERIAL_REPAIR_OK prefab={PrefabPath}");
        }

        private static void BuildPresentationPrefab(
            Material heroMaterial,
            Material enemyMaterial,
            Material barrageMaterial)
        {
            var root = new GameObject("PHS_GameOverPresentationRoot");
            try
            {
                var presenter = root.AddComponent<NetworkGameOverSequencePresenter>();
                var visualRoot = CreateChild(root.transform, "Presentation", Vector3.zero);
                var cameraRig = CreateChild(visualRoot.transform, "CameraRig", Vector3.zero);
                var cameraObject = CreateChild(cameraRig.transform, "CinematicCamera", new Vector3(9f, 6.5f, -28f));
                cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 0f));
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 48f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 220f;
                camera.depth = 100f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.enabled = false;

                var lightObject = CreateChild(visualRoot.transform, "KeyLight", new Vector3(-12f, 16f, -10f));
                lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                var keyLight = lightObject.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.color = new Color(0.48f, 0.63f, 1f);
                keyLight.intensity = 2.2f;

                var rimObject = CreateChild(visualRoot.transform, "EnemyRimLight", new Vector3(9f, 7f, 15f));
                rimObject.transform.rotation = Quaternion.Euler(24f, 160f, 0f);
                var rimLight = rimObject.AddComponent<Light>();
                rimLight.type = LightType.Directional;
                rimLight.color = new Color(1f, 0.08f, 0.03f);
                rimLight.intensity = 1.6f;

                var heroRoot = CreateChild(visualRoot.transform, "HeroShipVisualRoot", Vector3.zero);
                var hero = InstantiateAsset(HeroShipPath, heroRoot.transform, "HeroShip_SpaceCrew_Outside");
                hero.transform.localRotation = Quaternion.Euler(-4f, 155f, -3f);
                hero.transform.localScale = Vector3.one;
                ReplaceMaterials(hero, heroMaterial);

                var enemyRoot = CreateChild(visualRoot.transform, "EnemyFleetRoot", new Vector3(0f, 3f, 52f));
                CreateFleetMember(CarrierPath, enemyRoot.transform, "EnemyCarrier", new Vector3(0f, 4f, 0f), 0.55f, enemyMaterial);
                CreateFleetMember(BomberPath, enemyRoot.transform, "EnemyBomber_Left", new Vector3(-9f, 1f, -4f), 0.65f, enemyMaterial);
                CreateFleetMember(BomberPath, enemyRoot.transform, "EnemyBomber_Right", new Vector3(9f, 1f, -4f), 0.65f, enemyMaterial);
                CreateFleetMember(JetPath, enemyRoot.transform, "EnemyJet_Left", new Vector3(-14f, -2f, -9f), 0.7f, enemyMaterial);
                CreateFleetMember(JetPath, enemyRoot.transform, "EnemyJet_Right", new Vector3(14f, -2f, -9f), 0.7f, enemyMaterial);

                var fleetArrivalRoot = CreateChild(visualRoot.transform, "FleetArrivalEffects", new Vector3(0f, 2f, 30f));
                CreateEffect(FleetArrivalPath, fleetArrivalRoot.transform, "CyberBomberArrival_Left", new Vector3(-8f, 0f, 0f), 1.4f);
                CreateEffect(FleetArrivalPath, fleetArrivalRoot.transform, "CyberBomberArrival_Right", new Vector3(8f, 1f, 4f), 1.4f);

                var barrageRoot = CreateChild(visualRoot.transform, "ConcentratedBarrageEffects", Vector3.zero);
                var impactPositions = new[]
                {
                    new Vector3(-4.5f, 1.4f, 0f),
                    new Vector3(4.2f, 0.6f, 1.5f),
                    new Vector3(-1.6f, 2.4f, -1.4f),
                    new Vector3(2.2f, -1.1f, -2f),
                    new Vector3(0f, 0.2f, 3.2f),
                };
                for (var index = 0; index < impactPositions.Length; index++)
                {
                    var path = index % 2 == 0 ? HitEffectPath : ImpactEffectPath;
                    CreateEffect(path, barrageRoot.transform, $"HullImpact_{index + 1:00}", impactPositions[index], 1.5f);
                }
                CreateConcentratedBarrageBeams(barrageRoot.transform, barrageMaterial);

                var explosionRoot = CreateChild(visualRoot.transform, "FinalShipExplosionEffects", Vector3.zero);
                CreateEffect(ExplosionPath, explosionRoot.transform, "RuinExplosion_Main", Vector3.zero, 2.2f);
                CreateEffect(ExplosionPath, explosionRoot.transform, "RuinExplosion_Secondary", new Vector3(2.6f, 0.8f, -1.4f), 1.15f);

                SetSerializedReference(presenter, "visualRoot", visualRoot);
                SetSerializedReference(presenter, "cinematicCamera", camera);
                SetSerializedReference(presenter, "playerShipRoot", heroRoot.transform);
                SetSerializedReference(presenter, "enemyFleetRoot", enemyRoot.transform);
                SetSerializedReference(presenter, "fleetArrivalEffectRoot", fleetArrivalRoot);
                SetSerializedReference(presenter, "barrageEffectRoot", barrageRoot);
                SetSerializedReference(presenter, "explosionEffectRoot", explosionRoot);
                SetSerializedVector(presenter, "enemyFleetApproach", new Vector3(0f, 0f, -38f));

                fleetArrivalRoot.SetActive(false);
                barrageRoot.SetActive(false);
                explosionRoot.SetActive(false);
                visualRoot.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireRunSessionRoot()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(RunSessionRootPath);
            try
            {
                if (prefabRoot.GetComponent<NetworkGameOverSequenceCoordinator>() == null)
                {
                    prefabRoot.AddComponent<NetworkGameOverSequenceCoordinator>();
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, RunSessionRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void PlaceInMapScene()
        {
            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Active scene has unsaved changes: {currentScene.path}. Save or revert before authoring.");
            }

            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var existing = scene.GetRootGameObjects()
                .FirstOrDefault(item => item.name == "PHS_GameOverPresentationRoot");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "PHS_GameOverPresentationRoot";
            instance.transform.position = new Vector3(10000f, 10000f, 10000f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidateOrThrow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Game over presentation prefab missing: {PrefabPath}");
            }

            var presenter = prefab.GetComponent<NetworkGameOverSequencePresenter>();
            if (presenter == null)
            {
                throw new InvalidOperationException("Game over presenter component missing.");
            }

            var heroMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "/PHS_GameOver_HeroShip.mat");
            var heroRoot = prefab.transform.Find("Presentation/HeroShipVisualRoot");
            if (heroMaterial == null
                || heroRoot == null
                || heroRoot.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    renderer.sharedMaterials.Length == 0
                    || renderer.sharedMaterials.Any(material => material != heroMaterial)))
            {
                throw new InvalidOperationException("Game over hero ship material is not fully applied.");
            }

            ValidateExplosionAssets();

            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPath);
            if (rootPrefab == null
                || rootPrefab.GetComponent<NetworkGameOverSequenceCoordinator>() == null)
            {
                throw new InvalidOperationException("Run session root game over coordinator missing.");
            }

            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            if (!scene.GetRootGameObjects().Any(item => item.name == "PHS_GameOverPresentationRoot"))
            {
                throw new InvalidOperationException("Game over presentation scene instance missing.");
            }
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static GameObject InstantiateAsset(
            string assetPath,
            Transform parent,
            string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required game over asset missing: {assetPath}");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void ValidateExplosionAssets()
        {
            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
            var renderers = explosion != null
                ? explosion.GetComponentsInChildren<ParticleSystemRenderer>(true)
                : Array.Empty<ParticleSystemRenderer>();
            var invalidRenderers = renderers
                .Where(renderer => renderer.sharedMaterial == null
                    || renderer.sharedMaterial.shader == null
                    || (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh == null))
                .Select(renderer => $"{renderer.name}:material={renderer.sharedMaterial?.name ?? "null"},"
                    + $"shader={renderer.sharedMaterial?.shader?.name ?? "null"},mesh={renderer.mesh?.name ?? "null"}")
                .ToArray();
            if (renderers.Length == 0 || invalidRenderers.Length > 0)
            {
                throw new InvalidOperationException(
                    "Game over ruin explosion has a missing material, shader, or mesh reference: "
                    + string.Join("; ", invalidRenderers));
            }
        }

        private static void CreateFleetMember(
            string assetPath,
            Transform parent,
            string name,
            Vector3 localPosition,
            float scale,
            Material material)
        {
            var member = InstantiateAsset(assetPath, parent, name);
            member.transform.localPosition = localPosition;
            member.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            member.transform.localScale = Vector3.one * scale;
            ReplaceMaterials(member, material);
        }

        private static void CreateEffect(
            string assetPath,
            Transform parent,
            string name,
            Vector3 localPosition,
            float scale)
        {
            var effect = InstantiateAsset(assetPath, parent, name);
            effect.transform.localPosition = localPosition;
            effect.transform.localScale = Vector3.one * scale;
        }

        private static void ReplaceMaterials(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Color baseColor,
            Color emissionColor)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader unavailable.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor * 2.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateBeamMaterial(string path)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Unlit shader unavailable.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", new Color(1f, 0.035f, 0.01f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateConcentratedBarrageBeams(Transform parent, Material material)
        {
            var origins = new[]
            {
                new Vector3(-16f, 6f, 24f),
                new Vector3(-10f, -2f, 22f),
                new Vector3(-5f, 8f, 26f),
                new Vector3(5f, 7f, 25f),
                new Vector3(10f, -1f, 22f),
                new Vector3(16f, 5f, 24f),
            };
            var targets = new[]
            {
                new Vector3(-3.8f, 1.1f, 0.4f),
                new Vector3(-2f, -0.7f, 1.4f),
                new Vector3(-0.8f, 2.1f, -0.8f),
                new Vector3(1.2f, 1.6f, 0.6f),
                new Vector3(2.8f, -0.5f, -1.2f),
                new Vector3(4f, 0.7f, 1.1f),
            };

            for (var index = 0; index < origins.Length; index++)
            {
                var beamObject = CreateChild(parent, $"EnemyFocusBeam_{index + 1:00}", Vector3.zero);
                var beam = beamObject.AddComponent<LineRenderer>();
                beam.useWorldSpace = false;
                beam.positionCount = 2;
                beam.SetPosition(0, origins[index]);
                beam.SetPosition(1, targets[index]);
                beam.startWidth = 0.22f;
                beam.endWidth = 0.08f;
                beam.numCapVertices = 4;
                beam.material = material;
                beam.startColor = new Color(1f, 0.12f, 0.03f, 1f);
                beam.endColor = new Color(1f, 0.72f, 0.2f, 1f);
            }
        }

        private static void SetSerializedReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"Serialized property missing: {propertyName}");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedVector(
            UnityEngine.Object target,
            string propertyName,
            Vector3 value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"Serialized property missing: {propertyName}");
            property.vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateSourceAssets()
        {
            var requiredPaths = new[]
            {
                CarrierPath,
                HeroShipPath,
                JetPath,
                BomberPath,
                FleetArrivalPath,
                HitEffectPath,
                ImpactEffectPath,
                ExplosionPath,
            };

            var missing = requiredPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "Required downloaded game over assets missing: " + string.Join(", ", missing));
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab", "GameOver");
            EnsureFolder(PrefabFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
