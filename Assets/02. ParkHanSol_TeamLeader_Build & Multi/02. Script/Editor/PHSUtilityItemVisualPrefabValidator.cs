using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityItemVisualPrefabValidator
    {
        private const string ItemRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items";

        private sealed class ItemSpec
        {
            public ItemSpec(
                string name,
                string heldGuid,
                string droppedGuid,
                string dataFile,
                string economyFile,
                string legacyPath,
                bool requiresBatteryImpact = false)
            {
                Name = name;
                HeldGuid = heldGuid;
                DroppedGuid = droppedGuid;
                HeldPath = AssetDatabase.GUIDToAssetPath(heldGuid);
                DroppedPath = AssetDatabase.GUIDToAssetPath(droppedGuid);
                VisualPath = $"{ItemRoot}/Visual/ParkHanSol_{name}_Visual.prefab";
                DataPath = $"Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/{dataFile}";
                EconomyPath = $"Assets/03. SeoBoGyeong_Game Economy/04. Data/Items/{economyFile}";
                LegacyPath = legacyPath;
                RequiresBatteryImpact = requiresBatteryImpact;
            }

            public string Name { get; }
            public string HeldGuid { get; }
            public string DroppedGuid { get; }
            public string HeldPath { get; }
            public string DroppedPath { get; }
            public string VisualPath { get; }
            public string DataPath { get; }
            public string EconomyPath { get; }
            public string LegacyPath { get; }
            public bool RequiresBatteryImpact { get; }
        }

        private static readonly ItemSpec[] Specs =
        {
            new(
                "Wrench",
                "49657a941acc42a4b89fafa997981948",
                "964a2cb98bb874445ac55c7b03ac1852",
                "ParkHanSol_WrenchItemPrefabData.asset",
                "UtilityItem_Wrench.asset",
                $"{ItemRoot}/ParkHanSol_Wrench.prefab"),
            new(
                "FireExtinguisher",
                "85891584d8e563b439d507900ea1cecc",
                "f776009898cf0a247befddee700e82d4",
                "ParkHanSol_FireExtinguisherItemPrefabData.asset",
                "UtilityItem_FireExtinguisher.asset",
                $"{ItemRoot}/ParkHanSol_FireExtinguisher.prefab"),
            new(
                "BatteryPack",
                "e58ed9298fab8a3429146939f2c16788",
                "389c335a5d6e1514380221f9fd1c1956",
                "ParkHanSol_BatteryItemPrefabData.asset",
                "UtilityItem_BatteryPack.asset",
                $"{ItemRoot}/ParkHanSol_FuturisticBatteryPack.prefab",
                true)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Canonical Utility Item Visual Prefabs")]
        public static void Validate()
        {
            var errors = new List<string>();
            foreach (var spec in Specs)
            {
                ValidateSpec(spec, errors);
            }

            ValidateScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity",
                Specs[0].HeldPath,
                errors);
            ValidateScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity",
                Specs[1].HeldPath,
                errors);
            ValidateScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity",
                Specs[2].HeldPath,
                errors);
            ValidateScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity",
                Specs[0].DroppedPath,
                errors);
            ValidateScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity",
                Specs[2].DroppedPath,
                errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UTILITY_ITEM_VISUAL_TRUTH_VALIDATION_FAILED\n" +
                    string.Join("\n", errors));
            }

            Debug.Log("PHS_UTILITY_ITEM_VISUAL_TRUTH_VALIDATION_PASSED items=3 wrappers=6 economy=3 scenes=2");
        }

        private static void ValidateSpec(ItemSpec spec, ICollection<string> errors)
        {
            Require(
                AssetDatabase.AssetPathToGUID(spec.HeldPath) == spec.HeldGuid,
                $"held_guid_changed item={spec.Name} path={spec.HeldPath}",
                errors);
            Require(
                AssetDatabase.AssetPathToGUID(spec.DroppedPath) == spec.DroppedGuid,
                $"dropped_guid_changed item={spec.Name} path={spec.DroppedPath}",
                errors);

            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(spec.VisualPath);
            Require(visual != null, $"visual_missing item={spec.Name}", errors);
            if (visual != null)
            {
                Require(
                    visual.GetComponentsInChildren<Renderer>(true).Length > 0,
                    $"visual_renderer_missing item={spec.Name}",
                    errors);
                Require(
                    visual.GetComponentsInChildren<MonoBehaviour>(true).Length == 0
                    && visual.GetComponentsInChildren<Rigidbody>(true).Length == 0
                    && visual.GetComponentsInChildren<Collider>(true).Length == 0,
                    $"visual_contains_gameplay item={spec.Name}",
                    errors);
            }

            var held = AssetDatabase.LoadAssetAtPath<GameObject>(spec.HeldPath);
            var dropped = AssetDatabase.LoadAssetAtPath<GameObject>(spec.DroppedPath);
            ValidateWrapper(held, spec.HeldPath, spec.VisualPath, true, spec.RequiresBatteryImpact, errors);
            ValidateWrapper(dropped, spec.DroppedPath, spec.VisualPath, false, spec.RequiresBatteryImpact, errors);
            ValidateData(spec, held, dropped, errors);
        }

        private static void ValidateWrapper(
            GameObject prefab,
            string wrapperPath,
            string visualPath,
            bool held,
            bool requiresBatteryImpact,
            ICollection<string> errors)
        {
            if (prefab == null)
            {
                errors.Add($"wrapper_missing path={wrapperPath}");
                return;
            }

            var visualRoots = prefab.transform.Cast<Transform>()
                .Where(child =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == visualPath)
                .ToArray();
            Require(
                visualRoots.Length == 1,
                $"visual_instance_count path={wrapperPath} actual={visualRoots.Length}",
                errors);
            if (visualRoots.Length == 1)
            {
                var visualRenderers = visualRoots[0].GetComponentsInChildren<Renderer>(true);
                Require(
                    visualRenderers.Length == prefab.GetComponentsInChildren<Renderer>(true).Length,
                    $"renderer_outside_visual path={wrapperPath}",
                    errors);
            }

            var usableCount = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Count(component => component is IUsableItem);
            if (held)
            {
                Require(usableCount == 1, $"held_usable_count path={wrapperPath} actual={usableCount}", errors);
                Require(prefab.GetComponent<NetworkObject>() == null, $"held_network_object path={wrapperPath}", errors);
                Require(prefab.GetComponent<Rigidbody>() == null, $"held_rigidbody path={wrapperPath}", errors);
                return;
            }

            Require(prefab.GetComponent<NetworkObject>() != null, $"dropped_network_object_missing path={wrapperPath}", errors);
            Require(prefab.GetComponent<NetworkTransform>() != null, $"dropped_network_transform_missing path={wrapperPath}", errors);
            Require(prefab.GetComponent<Rigidbody>() != null, $"dropped_rigidbody_missing path={wrapperPath}", errors);
            Require(prefab.GetComponent<Collider>() != null, $"dropped_collider_missing path={wrapperPath}", errors);
            if (requiresBatteryImpact)
            {
                Require(prefab.GetComponent<BatteryThrownImpact>() != null, $"battery_impact_missing path={wrapperPath}", errors);
            }
        }

        private static void ValidateData(
            ItemSpec spec,
            GameObject held,
            GameObject dropped,
            ICollection<string> errors)
        {
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemPrefabData>(spec.DataPath);
            Require(itemData != null, $"item_data_missing path={spec.DataPath}", errors);
            if (itemData != null)
            {
                Require(itemData.HeldPrefab == held, $"item_data_held_mismatch path={spec.DataPath}", errors);
                Require(itemData.DroppedPrefab == dropped, $"item_data_dropped_mismatch path={spec.DataPath}", errors);
            }

            var economyData = AssetDatabase.LoadMainAssetAtPath(spec.EconomyPath);
            Require(economyData != null, $"economy_data_missing path={spec.EconomyPath}", errors);
            if (economyData == null)
            {
                return;
            }

            var serialized = new SerializedObject(economyData);
            Require(
                serialized.FindProperty("heldPrefab")?.objectReferenceValue == held,
                $"economy_held_mismatch path={spec.EconomyPath}",
                errors);
            Require(
                serialized.FindProperty("droppedPrefab")?.objectReferenceValue == dropped,
                $"economy_dropped_mismatch path={spec.EconomyPath}",
                errors);
        }

        private static void ValidateScene(
            string scenePath,
            string expectedPrefabPath,
            ICollection<string> errors)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var sourcePaths = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))
                    .Where(root => root != null)
                    .Distinct()
                    .Select(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot)
                    .ToArray();
                Require(
                    sourcePaths.Contains(expectedPrefabPath),
                    $"scene_canonical_instance_missing scene={scenePath} prefab={expectedPrefabPath}",
                    errors);
                foreach (var legacyPath in Specs.Select(spec => spec.LegacyPath))
                {
                    Require(
                        !sourcePaths.Contains(legacyPath),
                        $"scene_legacy_instance scene={scenePath} prefab={legacyPath}",
                        errors);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }
    }
}
