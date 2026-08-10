using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityItemVisualPrefabAuthoring
    {
        private const string ItemRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items";
        private const string FeatureInspectionScene =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity";
        private const string NetworkTutorialScene =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string UtilityItemDataRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems";

        private sealed class ItemSpec
        {
            public ItemSpec(
                string name,
                string heldFile,
                string droppedFile,
                string legacyPath)
            {
                Name = name;
                HeldPath = $"{ItemRoot}/Imported/{heldFile}";
                DroppedPath = $"{ItemRoot}/Imported/{droppedFile}";
                VisualPath = $"{ItemRoot}/Visual/ParkHanSol_{name}_Visual.prefab";
                LegacyPath = legacyPath;
            }

            public string Name { get; }
            public string HeldPath { get; }
            public string DroppedPath { get; }
            public string VisualPath { get; }
            public string LegacyPath { get; }
        }

        private static readonly ItemSpec[] Specs =
        {
            new(
                "Wrench",
                "ParkHanSol_Wrench_Held.prefab",
                "ParkHanSol_Wrench_Dropped.prefab",
                $"{ItemRoot}/ParkHanSol_Wrench.prefab"),
            new(
                "FireExtinguisher",
                "ParkHanSol_FireExtinguisher_Held.prefab",
                "ParkHanSol_FireExtinguisher_Dropped.prefab",
                $"{ItemRoot}/ParkHanSol_FireExtinguisher.prefab"),
            new(
                "BatteryPack",
                "ParkHanSol_BatteryPack_Held.prefab",
                "ParkHanSol_BatteryPack_Dropped.prefab",
                $"{ItemRoot}/ParkHanSol_FuturisticBatteryPack.prefab")
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Build Canonical Utility Item Visual Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before authoring utility item prefabs.");
            }

            EnsureFolder($"{ItemRoot}/Visual");
            foreach (var spec in Specs)
            {
                BuildVisualPrefab(spec);
                ReplaceWrapperVisual(spec.HeldPath, spec.VisualPath);
                ReplaceWrapperVisual(spec.DroppedPath, spec.VisualPath);
            }

            ReplaceLegacySceneInstances(
                FeatureInspectionScene,
                new Dictionary<string, string>
                {
                    [Specs[0].LegacyPath] = Specs[0].HeldPath
                });
            ReplaceLegacySceneInstances(
                NetworkTutorialScene,
                new Dictionary<string, string>
                {
                    [Specs[0].LegacyPath] = Specs[0].DroppedPath,
                    [Specs[2].LegacyPath] = Specs[2].DroppedPath
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PHS_UTILITY_ITEM_VISUAL_TRUTH_BUILD_PASSED items=3 wrappers=6 scenes=2");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Reconcile Canonical Utility Item Scene Instances")]
        public static void ReconcileCanonicalSceneInstances()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before reconciling utility item scenes.");
            }

            var wrench = RequireItemData("ParkHanSol_WrenchItemPrefabData.asset");
            var battery = RequireItemData("ParkHanSol_BatteryItemPrefabData.asset");

            ReplaceLegacySceneInstances(
                FeatureInspectionScene,
                CreateReplacements((Specs[2].HeldPath, battery.HandPrefab)));
            ReplaceLegacySceneInstances(
                NetworkTutorialScene,
                CreateReplacements(
                    (Specs[0].LegacyPath, wrench.DroppedPrefab),
                    (Specs[2].LegacyPath, battery.DroppedPrefab),
                    (Specs[0].DroppedPath, wrench.DroppedPrefab),
                    (Specs[2].DroppedPath, battery.DroppedPrefab)));

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_UTILITY_ITEM_SCENE_RECONCILE_PASSED scenes=2 items=2");
        }

        private static UtilityItemDataSO RequireItemData(string fileName)
        {
            var path = $"{UtilityItemDataRoot}/{fileName}";
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(path);
            if (itemData == null || itemData.HandPrefab == null || itemData.DroppedPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Utility item data contract missing. path={path}");
            }

            return itemData;
        }

        private static Dictionary<string, string> CreateReplacements(
            params (string sourcePath, GameObject targetPrefab)[] replacements)
        {
            var result = new Dictionary<string, string>();
            foreach (var (sourcePath, targetPrefab) in replacements)
            {
                var targetPath = AssetDatabase.GetAssetPath(targetPrefab);
                if (!string.IsNullOrEmpty(sourcePath)
                    && !string.IsNullOrEmpty(targetPath)
                    && sourcePath != targetPath)
                {
                    result[sourcePath] = targetPath;
                }
            }

            return result;
        }

        private static void BuildVisualPrefab(ItemSpec spec)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.VisualPath) == null)
            {
                CreateVisualPrefab(spec);
            }

            MigrateVisualPrefab(spec);
        }

        private static void CreateVisualPrefab(ItemSpec spec)
        {
            var heldRoot = PrefabUtility.LoadPrefabContents(spec.HeldPath);
            try
            {
                var sourceModel = FindDirectVisualChild(heldRoot, spec.HeldPath);
                var visualRoot = new GameObject($"ParkHanSol_{spec.Name}_Visual");
                try
                {
                    var modelClone = UnityEngine.Object.Instantiate(sourceModel);
                    modelClone.name = "Model";
                    modelClone.transform.SetParent(visualRoot.transform, false);
                    modelClone.transform.localPosition = Vector3.zero;
                    modelClone.transform.localRotation = Quaternion.identity;
                    modelClone.transform.localScale = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(visualRoot, spec.VisualPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(visualRoot);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(heldRoot);
            }
        }

        private static void MigrateVisualPrefab(ItemSpec spec)
        {
            var visualRoot = PrefabUtility.LoadPrefabContents(spec.VisualPath);
            try
            {
                visualRoot.name = $"ParkHanSol_{spec.Name}_Visual";
                var modelRoot = EnsureDirectChild(
                    visualRoot.transform,
                    "ModelRoot");
                var vfxRoot = EnsureDirectChild(
                    visualRoot.transform,
                    "VFXRoot");
                MoveLegacyVisualChildren(
                    visualRoot.transform,
                    modelRoot,
                    vfxRoot);

                var useRoot = EnsureDirectChild(vfxRoot, "Use");
                var loopRoot = EnsureDirectChild(vfxRoot, "Loop");
                var impactRoot = EnsureDirectChild(vfxRoot, "Impact");
                var controller = EnsureSingleVfxController(visualRoot);
                AssignEffectChannel(controller, "use", useRoot);
                AssignEffectChannel(controller, "loop", loopRoot);
                AssignEffectChannel(controller, "impact", impactRoot);
                PrefabUtility.SaveAsPrefabAsset(visualRoot, spec.VisualPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(visualRoot);
            }
        }

        private static void MoveLegacyVisualChildren(
            Transform visualRoot,
            Transform modelRoot,
            Transform vfxRoot)
        {
            var legacyChildren = visualRoot.Cast<Transform>()
                .Where(child => child != modelRoot && child != vfxRoot)
                .ToArray();
            foreach (var child in legacyChildren)
            {
                child.SetParent(modelRoot, false);
            }
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string childName)
        {
            var matches = parent.Cast<Transform>()
                .Where(child => child.name == childName)
                .ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Duplicate visual hierarchy node. parent={parent.name} child={childName}");
            }

            if (matches.Length == 1)
            {
                return matches[0];
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static UtilityItemVfxController EnsureSingleVfxController(
            GameObject visualRoot)
        {
            var controllers = visualRoot.GetComponents<UtilityItemVfxController>();
            if (controllers.Length == 0)
            {
                return visualRoot.AddComponent<UtilityItemVfxController>();
            }

            for (var index = 1; index < controllers.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(controllers[index], true);
            }

            return controllers[0];
        }

        private static void AssignEffectChannel(
            UtilityItemVfxController controller,
            string channelName,
            Transform channelRoot)
        {
            var serialized = new SerializedObject(controller);
            var channel = serialized.FindProperty(channelName);
            if (channel == null)
            {
                throw new InvalidOperationException(
                    $"VFX channel property missing. channel={channelName}");
            }

            AssignObjectArray(
                channel.FindPropertyRelative("particleSystems"),
                channelRoot.GetComponentsInChildren<ParticleSystem>(true));
            AssignObjectArray(
                channel.FindPropertyRelative("audioSources"),
                channelRoot.GetComponentsInChildren<AudioSource>(true));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignObjectArray<T>(
            SerializedProperty property,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            if (property == null)
            {
                throw new InvalidOperationException(
                    "VFX serialized reference array missing.");
            }

            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static void ReplaceWrapperVisual(string wrapperPath, string visualPath)
        {
            var visualPrefab = RequirePrefab(visualPath);
            var wrapperRoot = PrefabUtility.LoadPrefabContents(wrapperPath);
            try
            {
                var oldVisual = FindDirectVisualChild(wrapperRoot, wrapperPath);
                var localPosition = oldVisual.transform.localPosition;
                var localRotation = oldVisual.transform.localRotation;
                var localScale = oldVisual.transform.localScale;
                UnityEngine.Object.DestroyImmediate(oldVisual);

                var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                    visualPrefab,
                    wrapperRoot.scene);
                visualInstance.name = visualPrefab.name;
                visualInstance.transform.SetParent(wrapperRoot.transform, false);
                visualInstance.transform.localPosition = localPosition;
                visualInstance.transform.localRotation = localRotation;
                visualInstance.transform.localScale = localScale;
                PrefabUtility.SaveAsPrefabAsset(wrapperRoot, wrapperPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wrapperRoot);
            }
        }

        private static GameObject FindDirectVisualChild(GameObject root, string ownerPath)
        {
            var candidates = root.transform.Cast<Transform>()
                .Select(child => child.gameObject)
                .Where(child => child.GetComponentInChildren<Renderer>(true) != null)
                .ToArray();
            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one direct visual child. path={ownerPath} actual={candidates.Length}");
            }

            return candidates[0];
        }

        private static void ReplaceLegacySceneInstances(
            string scenePath,
            IReadOnlyDictionary<string, string> replacements)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            var wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasAlreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                var instanceRoots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))
                    .Where(root => root != null)
                    .Distinct()
                    .ToArray();

                foreach (var replacement in replacements)
                {
                    var legacyRoots = instanceRoots
                        .Where(instanceRoot =>
                            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) == replacement.Key)
                        .ToArray();
                    if (legacyRoots.Length == 0)
                    {
                        var alreadyCanonical = instanceRoots.Any(instanceRoot =>
                            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) == replacement.Value);
                        if (!alreadyCanonical)
                        {
                            throw new InvalidOperationException(
                                $"Scene item instance missing. scene={scenePath} legacy={replacement.Key} canonical={replacement.Value}");
                        }

                        continue;
                    }

                    foreach (var legacyRoot in legacyRoots)
                    {
                        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                            legacyRoot,
                            RequirePrefab(replacement.Value),
                            InteractionMode.AutomatedAction);
                    }
                }

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasAlreadyLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject RequirePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Prefab missing. path={path}");
            }

            return prefab;
        }

        private static void EnsureFolder(string assetPath)
        {
            var current = "Assets";
            foreach (var segment in assetPath.Split('/').Skip(1))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }
    }
}
