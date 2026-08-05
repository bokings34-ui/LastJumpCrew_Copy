using System;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityFamilyAuthoring
    {
        private const string ItemRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items";
        private static readonly string[] PlayerPaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Utility Family Wiring")]
        public static void Author()
        {
            RequireAssets();
            ConfigureFamilyData(
                "ParkHanSol_WrenchItemPrefabData.asset",
                PHSUtilityFamilyActionKind.Wrench);
            ConfigureFamilyData(
                "ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset",
                PHSUtilityFamilyActionKind.Wrench);
            ConfigureFamilyData(
                "ParkHanSol_FireExtinguisherItemPrefabData.asset",
                PHSUtilityFamilyActionKind.FireExtinguisher);
            ConfigureFamilyData(
                "ParkHanSol_TripoFireExtinguisherItemPrefabData.asset",
                PHSUtilityFamilyActionKind.FireExtinguisher);
            ConfigureFamilyData(
                "ParkHanSol_BatteryItemPrefabData.asset",
                PHSUtilityFamilyActionKind.Battery);

            ConfigureHeld<PHSWrenchFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_Wrench_Held.prefab",
                typeof(PHSAnimatedWrenchItemUse),
                typeof(WrenchUsableItem));
            ConfigureHeld<PHSWrenchFamilyUsableItem>(
                $"{ItemRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab",
                typeof(FuturisticAdjustableWrenchUsableItem));
            ConfigureHeld<PHSFireExtinguisherFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_FireExtinguisher_Held.prefab",
                typeof(PHSAnimatedFireExtinguisherItemUse),
                typeof(FireExtinguisherUsableItem));
            ConfigureHeld<PHSFireExtinguisherFamilyUsableItem>(
                $"{ItemRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab",
                typeof(TripoFireExtinguisherUsableItem));
            ConfigureHeld<PHSBatteryFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_BatteryPack_Held.prefab",
                typeof(PHSAnimatedBatteryItemUse));

            foreach (var playerPath in PlayerPaths)
            {
                EditPrefab(playerPath, root =>
                {
                    var controllers = root.GetComponents<
                        PHSNetworkUtilityFamilyActionController>();
                    if (controllers.Length > 1)
                    {
                        throw new InvalidOperationException(
                            $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=controller_duplicate path={playerPath}");
                    }

                    if (controllers.Length == 0)
                    {
                        root.AddComponent<
                            PHSNetworkUtilityFamilyActionController>();
                    }
                });
            }

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_UTILITY_FAMILY_AUTHORING_COMPLETE held=5 players=2");
        }

        private static void RequireAssets()
        {
            var paths = new[]
            {
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_WrenchItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FireExtinguisherItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_TripoFireExtinguisherItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_BatteryItemPrefabData.asset",
                $"{ItemRoot}/Imported/ParkHanSol_Wrench_Held.prefab",
                $"{ItemRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab",
                $"{ItemRoot}/Imported/ParkHanSol_FireExtinguisher_Held.prefab",
                $"{ItemRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab",
                $"{ItemRoot}/Imported/ParkHanSol_BatteryPack_Held.prefab",
                PlayerPaths[0],
                PlayerPaths[1]
            };
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=asset_missing path={path}");
                }
            }
        }

        private static void ConfigureFamilyData(
            string fileName,
            PHSUtilityFamilyActionKind familyKind)
        {
            var path =
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/" +
                fileName;
            var itemData = AssetDatabase.LoadAssetAtPath<
                UtilityItemPrefabData>(path);
            if (itemData == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=data_missing path={path}");
            }

            var serialized = new SerializedObject(itemData);
            serialized.FindProperty("utilityFamily").intValue =
                (int)familyKind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemData);
        }

        private static void ConfigureHeld<TFamily>(
            string path,
            params Type[] legacyTypes)
            where TFamily : Component
        {
            EditPrefab(path, root =>
            {
                foreach (var legacyType in legacyTypes)
                {
                    foreach (var component in root.GetComponentsInChildren(
                                 legacyType,
                                 true))
                    {
                        UnityEngine.Object.DestroyImmediate(component, true);
                    }
                }

                var familyComponents = root.GetComponentsInChildren<TFamily>(true);
                if (familyComponents.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=family_duplicate path={path}");
                }

                if (familyComponents.Length == 0)
                {
                    root.AddComponent<TFamily>();
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
                    $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=prefab_missing path={path}");
            }

            try
            {
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_UTILITY_FAMILY_AUTHORING_FAILED reason=save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
