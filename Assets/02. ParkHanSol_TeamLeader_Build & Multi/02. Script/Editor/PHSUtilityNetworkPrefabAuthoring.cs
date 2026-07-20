using System;
using System.Linq;
using System.Reflection;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityNetworkPrefabAuthoring
    {
        private const string UtilityItemCatalogPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/PHS_UtilityItemCatalog_0717.asset";
        private static readonly string[] HeldPrefabFolders =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Held",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Debris/Held"
        };

        [MenuItem("Tools/ParkHanSol/Repair Utility Held Prefab References")]
        public static void RepairHeldPrefabReferences()
        {
            var catalog = LoadCatalog();
            var repairedCount = 0;
            foreach (var itemData in catalog.Items)
            {
                var droppedPrefab = itemData?.DroppedPrefab;
                if (itemData == null || itemData.HeldPrefab != null || droppedPrefab == null)
                {
                    continue;
                }

                var expectedName = $"{droppedPrefab.name}_Held";
                var matches = AssetDatabase
                    .FindAssets($"{expectedName} t:Prefab", HeldPrefabFolders)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Where(candidate => candidate != null && candidate.name == expectedName)
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new MissingReferenceException(
                        $"PHS_HELD_ITEM_REFERENCE_REPAIR_FAILED item={itemData.ItemId} expected={expectedName} matches={matches.Length}");
                }

                var serializedItemData = new SerializedObject(itemData);
                serializedItemData.FindProperty("heldPrefab").objectReferenceValue = matches[0];
                serializedItemData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(itemData);
                repairedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"PHS_HELD_ITEM_REFERENCE_REPAIR_OK repaired={repairedCount}");
        }

        [MenuItem("Tools/ParkHanSol/Repair Utility Network Prefab Hashes")]
        public static void RepairNetworkPrefabHashes()
        {
            var catalog = LoadCatalog();
            var onValidate = GetNetworkObjectOnValidate();

            var repairedCount = 0;
            foreach (var itemData in catalog.Items)
            {
                var networkObject = itemData?.DroppedPrefab?.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    continue;
                }

                onValidate.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
                repairedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"PHS_NETWORK_ITEM_HASH_REPAIR_OK repaired={repairedCount}");
        }

        private static UtilityItemCatalogSO LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(UtilityItemCatalogPath);
            if (catalog == null)
            {
                throw new MissingReferenceException(
                    $"PHS_NETWORK_ITEM_AUTHORING_FAILED reason=catalog_missing path={UtilityItemCatalogPath}");
            }

            return catalog;
        }

        private static MethodInfo GetNetworkObjectOnValidate()
        {
            return typeof(NetworkObject).GetMethod(
                       "OnValidate",
                       BindingFlags.Instance | BindingFlags.NonPublic)
                   ?? throw new MissingMethodException(
                       typeof(NetworkObject).FullName,
                       "OnValidate");
        }
    }
}
