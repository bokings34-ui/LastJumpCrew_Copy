using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityFamilyValidator
    {
        private const string ItemRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Utility Family Wiring")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateHeld<PHSWrenchFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_Wrench_Held.prefab",
                errors);
            ValidateHeld<PHSWrenchFamilyUsableItem>(
                $"{ItemRoot}/Held/ParkHanSol_FuturisticAdjustableWrench_Held.prefab",
                errors);
            ValidateHeld<PHSFireExtinguisherFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_FireExtinguisher_Held.prefab",
                errors);
            ValidateHeld<PHSFireExtinguisherFamilyUsableItem>(
                $"{ItemRoot}/Held/ParkHanSol_TripoFireExtinguisher_Held.prefab",
                errors);
            ValidateHeld<PHSBatteryFamilyUsableItem>(
                $"{ItemRoot}/Imported/ParkHanSol_BatteryPack_Held.prefab",
                errors);

            ValidatePlayer(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
                errors);
            ValidatePlayer(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab",
                errors);

            ValidateData("wrench", true, 100, new[]
            {
                (UtilityItemActionKind.HullBreachRepair, 20, 1),
                (UtilityItemActionKind.SteamLeakRepair, 20, 1),
                (UtilityItemActionKind.OxygenLeakRepair, 3, 5),
                (UtilityItemActionKind.OxygenGeneratorRepair, 20, 1),
                (UtilityItemActionKind.GravityGeneratorRepair, 20, 1)
            }, errors);
            ValidateData("futuristic_adjustable_wrench", true, 150, new[]
            {
                (UtilityItemActionKind.DeviceRepair, 40, 1),
                (UtilityItemActionKind.HullBreachRepair, 40, 1),
                (UtilityItemActionKind.SteamLeakRepair, 40, 1),
                (UtilityItemActionKind.OxygenLeakRepair, 40, 1),
                (UtilityItemActionKind.OxygenGeneratorRepair, 40, 1),
                (UtilityItemActionKind.GravityGeneratorRepair, 40, 1)
            }, errors);
            ValidateData("fire_extinguisher", true, 100,
                new[] { (UtilityItemActionKind.FireSuppression, 2, 5) }, errors);
            ValidateData("tripo_fire_extinguisher", true, 150,
                new[] { (UtilityItemActionKind.FireSuppression, 70, 1) }, errors);
            ValidateData("battery_pack", true, 100, new[]
            {
                (UtilityItemActionKind.PowerRestore, 100, 100),
                (UtilityItemActionKind.BatteryDischarge, 20, 100)
            }, errors);
            ValidateData("auto_repair_kit", false, 100, new[]
            {
                (UtilityItemActionKind.DeviceRepair, 100, 0),
                (UtilityItemActionKind.SteamLeakRepair, 100, 0),
                (UtilityItemActionKind.OxygenLeakRepair, 100, 0),
                (UtilityItemActionKind.OxygenGeneratorRepair, 100, 0),
                (UtilityItemActionKind.GravityGeneratorRepair, 100, 0)
            }, errors);
            ValidateData("foam_sealant_gun", false, 100, new[]
            {
                (UtilityItemActionKind.FireSuppression, 100, 0),
                (UtilityItemActionKind.HullBreachRepair, 100, 0)
            }, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UTILITY_FAMILY_VALIDATION_FAILED\n" +
                    string.Join("\n", errors));
            }

            Debug.Log("PHS_UTILITY_FAMILY_VALIDATION_PASSED held=5 players=2 data=7");
        }

        private static void ValidateHeld<TFamily>(
            string path,
            ICollection<string> errors)
            where TFamily : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"held_missing path={path}");
                return;
            }

            var familyComponents =
                prefab.GetComponentsInChildren<TFamily>(true);
            if (familyComponents.Length != 1
                || familyComponents[0].GetType() != typeof(TFamily))
            {
                errors.Add(
                    $"family_component_exact path={path} expected={typeof(TFamily).Name} actual={familyComponents.Length}");
            }

            var usableCount = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Count(component => component is IUsableItem);
            if (usableCount != 1)
            {
                errors.Add($"held_usable_count path={path} actual={usableCount}");
            }
        }

        private static void ValidatePlayer(
            string path,
            ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var count = prefab == null
                ? 0
                : prefab.GetComponents<
                    PHSNetworkUtilityFamilyActionController>().Length;
            if (count != 1)
            {
                errors.Add($"player_controller_count path={path} actual={count}");
            }


            if (prefab == null
                || prefab.GetComponent<NetworkPlayerItemRecord>() == null
                || prefab.GetComponent<NetworkPlayerItemLifecycle>() == null
                || prefab.GetComponent<NetworkPlayerLifeState>() == null
                || prefab.GetComponent<TempPlayerItemHolder>() == null
                || prefab.GetComponent<PHSNetworkItemUseActionController>() == null
                || prefab.GetComponent<PHSNetworkItemUseFeedbackController>() == null)
            {
                errors.Add($"player_family_dependencies path={path}");
            }
        }

        private static void ValidateData(
            string itemId,
            bool usesDurability,
            int maxDurability,
            (UtilityItemActionKind Action, int Amount, int Cost)[] expectedProfiles,
            ICollection<string> errors)
        {
            var itemData = FindItemData(itemId);
            if (itemData == null
                || itemData.UsesDurability != usesDurability
                || itemData.MaxDurability != maxDurability
                || itemData.ActionProfiles.Count != expectedProfiles.Length)
            {
                errors.Add($"data_contract item={itemId}");
                return;
            }

            if (expectedProfiles.Any(expected =>
                    !itemData.TryGetActionProfile(expected.Action, out var actual)
                    || actual.Amount != expected.Amount
                    || actual.DurabilityCost != expected.Cost))
            {
                errors.Add($"data_profile_contract item={itemId}");
            }
        }

        private static UtilityItemDataSO FindItemData(string itemId)
        {
            return AssetDatabase.FindAssets(
                    "t:UtilityItemDataSO",
                    new[]
                    {
                        "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems"
                    })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<
                    UtilityItemDataSO>(path))
                .FirstOrDefault(candidate => candidate != null
                    && candidate.ItemId == itemId);
        }
    }
}
