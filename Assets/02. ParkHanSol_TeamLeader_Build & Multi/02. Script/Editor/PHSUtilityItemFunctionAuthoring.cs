using System;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityItemFunctionAuthoring
    {
        private const string ItemDataRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems";
        private const string ItemPrefabRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Utility Item Functions")]
        public static void Author()
        {
            RequireAssets();

            ConfigureDurabilityPair(
                $"{ItemDataRoot}/ParkHanSol_AutoRepairKitItemPrefabData.asset",
                $"{ItemPrefabRoot}/ParkHanSol_AutoRepairKit.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_AutoRepairKit_Held.prefab");
            ConfigureDurabilityPair(
                $"{ItemDataRoot}/ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset",
                $"{ItemPrefabRoot}/ParkHanSol_FuturisticAdjustableWrench.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab");
            ConfigureDurabilityPair(
                $"{ItemDataRoot}/ParkHanSol_TripoFireExtinguisherItemPrefabData.asset",
                $"{ItemPrefabRoot}/ParkHanSol_TripoFireExtinguisher.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab");

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_UTILITY_ITEM_FUNCTION_AUTHORING_COMPLETE items=3 durable=2 online_only=true");
        }

        private static void RequireAssets()
        {
            var paths = new[]
            {
                $"{ItemDataRoot}/ParkHanSol_AutoRepairKitItemPrefabData.asset",
                $"{ItemDataRoot}/ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset",
                $"{ItemDataRoot}/ParkHanSol_TripoFireExtinguisherItemPrefabData.asset",
                $"{ItemPrefabRoot}/ParkHanSol_AutoRepairKit.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_AutoRepairKit_Held.prefab",
                $"{ItemPrefabRoot}/ParkHanSol_FuturisticAdjustableWrench.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab",
                $"{ItemPrefabRoot}/ParkHanSol_TripoFireExtinguisher.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab"
            };
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=asset_missing path={path}");
                }
            }
        }

        private static void ConfigureDurabilityPair(
            string itemDataPath,
            string droppedPrefabPath,
            string heldPrefabPath)
        {
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(
                itemDataPath);
            if (itemData == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=item_data_missing path={itemDataPath}");
            }

            EditPrefab(droppedPrefabPath, root =>
            {
                if (!itemData.UsesDurability)
                {
                    foreach (var state in root.GetComponentsInChildren<
                                 NetworkUtilityItemDurabilityState>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(state, true);
                    }

                    return;
                }

                var itemObjects = root.GetComponents<UtilityItemObject>();
                if (itemObjects.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=item_object_count path={droppedPrefabPath} count={itemObjects.Length}");
                }

                var states = root.GetComponents<NetworkUtilityItemDurabilityState>();
                if (states.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=durability_state_duplicate path={droppedPrefabPath} count={states.Length}");
                }

                var configuredState = states.Length == 1
                    ? states[0]
                    : root.AddComponent<NetworkUtilityItemDurabilityState>();
                var serialized = new SerializedObject(configuredState);
                serialized.FindProperty("itemObject").objectReferenceValue =
                    itemObjects[0];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(heldPrefabPath, root =>
            {
                foreach (var behaviour in root.GetComponentsInChildren<NetworkBehaviour>(true))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour, true);
                }

                foreach (var networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    UnityEngine.Object.DestroyImmediate(networkObject, true);
                }

                var states = root.GetComponentsInChildren<
                    NetworkUtilityItemDurabilityState>(true);
                if (states.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=held_durability_state_duplicate path={heldPrefabPath} count={states.Length}");
                }

                if (states.Length == 1)
                {
                    UnityEngine.Object.DestroyImmediate(states[0], true);
                }
            });
        }

        private static void EditPrefab(
            string path,
            Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=prefab_load_failed path={path}");
            }

            try
            {
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=prefab_save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

    }
}
