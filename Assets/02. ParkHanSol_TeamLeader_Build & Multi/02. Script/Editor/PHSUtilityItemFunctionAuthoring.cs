using System;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
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

            ConfigureItemData(
                "ParkHanSol_AutoRepairKitItemPrefabData.asset",
                true,
                1,
                UtilityItemUpgradeEffect.None,
                0f,
                Profile(UtilityItemActionKind.DeviceRepair, 1, 1),
                Profile(UtilityItemActionKind.HullBreachRepair, 1, 1),
                Profile(UtilityItemActionKind.SteamLeakRepair, 1, 1),
                Profile(UtilityItemActionKind.OxygenLeakRepair, 1, 1),
                Profile(UtilityItemActionKind.OxygenGeneratorRepair, 1, 1),
                Profile(UtilityItemActionKind.GravityGeneratorRepair, 1, 1));
            ConfigureItemData(
                "ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset",
                true,
                150,
                UtilityItemUpgradeEffect.None,
                0f,
                Profile(UtilityItemActionKind.DeviceRepair, 40, 1),
                Profile(UtilityItemActionKind.HullBreachRepair, 40, 1),
                Profile(UtilityItemActionKind.SteamLeakRepair, 40, 1),
                Profile(UtilityItemActionKind.OxygenLeakRepair, 40, 1),
                Profile(UtilityItemActionKind.OxygenGeneratorRepair, 40, 1),
                Profile(UtilityItemActionKind.GravityGeneratorRepair, 40, 1));
            ConfigureItemData(
                "ParkHanSol_TripoFireExtinguisherItemPrefabData.asset",
                true,
                150,
                UtilityItemUpgradeEffect.None,
                0f,
                Profile(UtilityItemActionKind.FireSuppression, 70, 1));

            ConfigureDurabilityPair(
                $"{ItemPrefabRoot}/ParkHanSol_AutoRepairKit.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_AutoRepairKit_Held.prefab");
            ConfigureDurabilityPair(
                $"{ItemPrefabRoot}/ParkHanSol_FuturisticAdjustableWrench.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab");
            ConfigureDurabilityPair(
                $"{ItemPrefabRoot}/ParkHanSol_TripoFireExtinguisher.prefab",
                $"{ItemPrefabRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab");

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_UTILITY_ITEM_FUNCTION_AUTHORING_COMPLETE items=3 durable=3 online_only=true");
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

        private static void ConfigureItemData(
            string fileName,
            bool hasDurability,
            int maxDurability,
            UtilityItemUpgradeEffect upgradeEffect,
            float upgradeAmount,
            params ActionProfileData[] profiles)
        {
            var path = $"{ItemDataRoot}/{fileName}";
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemPrefabData>(path);
            if (itemData == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UTILITY_ITEM_FUNCTION_AUTHORING_FAILED reason=item_data_missing path={path}");
            }

            var serialized = new SerializedObject(itemData);
            serialized.FindProperty("hasDurability").boolValue = hasDurability;
            serialized.FindProperty("maxDurability").intValue = maxDurability;
            serialized.FindProperty("upgradeEffect").intValue = (int)upgradeEffect;
            serialized.FindProperty("upgradeAmount").floatValue = upgradeAmount;
            var actionProfiles = serialized.FindProperty("actionProfiles");
            actionProfiles.arraySize = profiles.Length;
            for (var index = 0; index < profiles.Length; index++)
            {
                var profile = actionProfiles.GetArrayElementAtIndex(index);
                profile.FindPropertyRelative("actionKind").intValue =
                    (int)profiles[index].ActionKind;
                profile.FindPropertyRelative("amount").intValue =
                    profiles[index].Amount;
                profile.FindPropertyRelative("durabilityCost").intValue =
                    profiles[index].DurabilityCost;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemData);
        }

        private static void ConfigureDurabilityPair(
            string droppedPrefabPath,
            string heldPrefabPath)
        {
            EditPrefab(droppedPrefabPath, root =>
            {
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

                var state = states.Length == 1
                    ? states[0]
                    : root.AddComponent<NetworkUtilityItemDurabilityState>();
                var serialized = new SerializedObject(state);
                serialized.FindProperty("itemObject").objectReferenceValue =
                    itemObjects[0];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(heldPrefabPath, root =>
            {
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

        private static ActionProfileData Profile(
            UtilityItemActionKind actionKind,
            int amount,
            int durabilityCost)
        {
            return new ActionProfileData(
                actionKind,
                amount,
                durabilityCost);
        }

        private readonly struct ActionProfileData
        {
            public ActionProfileData(
                UtilityItemActionKind actionKind,
                int amount,
                int durabilityCost)
            {
                ActionKind = actionKind;
                Amount = amount;
                DurabilityCost = durabilityCost;
            }

            public UtilityItemActionKind ActionKind { get; }
            public int Amount { get; }
            public int DurabilityCost { get; }
        }
    }
}
