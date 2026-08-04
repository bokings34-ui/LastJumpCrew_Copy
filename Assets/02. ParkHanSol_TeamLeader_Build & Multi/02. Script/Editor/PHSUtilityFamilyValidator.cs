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

            ValidateData("wrench", PHSUtilityFamilyActionKind.Wrench, 100, UtilityItemActionKind.DeviceRepair, 20, 1, errors);
            ValidateData("futuristic_adjustable_wrench", PHSUtilityFamilyActionKind.Wrench, 150, UtilityItemActionKind.DeviceRepair, 40, 1, errors);
            ValidateData("fire_extinguisher", PHSUtilityFamilyActionKind.FireExtinguisher, 100, UtilityItemActionKind.FireSuppression, 35, 1, errors);
            ValidateData("tripo_fire_extinguisher", PHSUtilityFamilyActionKind.FireExtinguisher, 150, UtilityItemActionKind.FireSuppression, 70, 1, errors);
            ValidateData("battery_pack", PHSUtilityFamilyActionKind.Battery, 100, UtilityItemActionKind.PowerRestore, 100, 100, errors);
            ValidateWrenchProfiles("wrench", 20, 1, errors);
            ValidateWrenchProfiles("futuristic_adjustable_wrench", 40, 40, errors);
            ValidateBatteryProfiles("battery_pack", errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UTILITY_FAMILY_VALIDATION_FAILED\n" +
                    string.Join("\n", errors));
            }

            Debug.Log("PHS_UTILITY_FAMILY_VALIDATION_PASSED held=5 players=2 data=5");
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
            PHSUtilityFamilyActionKind familyKind,
            int maxDurability,
            UtilityItemActionKind sampleAction,
            int sampleAmount,
            int sampleCost,
            ICollection<string> errors)
        {
            var itemData = FindItemData(itemId);
            if (itemData == null
                || itemData.UtilityFamily != familyKind
                || !itemData.HasDurability
                || itemData.MaxDurability != maxDurability
                || !itemData.TryGetActionProfile(sampleAction, out var profile)
                || profile.Amount != sampleAmount
                || profile.DurabilityCost != sampleCost)
            {
                errors.Add($"data_contract item={itemId}");
                return;
            }

            var expectedActions = familyKind switch
            {
                PHSUtilityFamilyActionKind.Wrench =>
                    new[]
                    {
                        UtilityItemActionKind.DeviceRepair,
                        UtilityItemActionKind.HullBreachRepair,
                        UtilityItemActionKind.SteamLeakRepair,
                        UtilityItemActionKind.OxygenLeakRepair,
                        UtilityItemActionKind.OxygenGeneratorRepair,
                        UtilityItemActionKind.GravityGeneratorRepair
                    },
                PHSUtilityFamilyActionKind.FireExtinguisher =>
                    new[]
                    {
                        UtilityItemActionKind.FireSuppression
                    },
                PHSUtilityFamilyActionKind.Battery =>
                    new[]
                    {
                        UtilityItemActionKind.PowerRestore,
                        UtilityItemActionKind.BatteryDischarge
                    },
                _ => Array.Empty<UtilityItemActionKind>()
            };
            var actualActions = itemData.ActionProfiles
                .Select(profile => profile.ActionKind)
                .OrderBy(action => action)
                .ToArray();
            if (!actualActions.SequenceEqual(
                    expectedActions.OrderBy(action => action)))
            {
                errors.Add($"data_family_actions item={itemId}");
            }
        }

        private static void ValidateWrenchProfiles(
            string itemId,
            int regularAmount,
            int oxygenLeakAmount,
            ICollection<string> errors)
        {
            var itemData = FindItemData(itemId);
            var expectations = new[]
            {
                (UtilityItemActionKind.DeviceRepair, regularAmount),
                (UtilityItemActionKind.HullBreachRepair, regularAmount),
                (UtilityItemActionKind.SteamLeakRepair, regularAmount),
                (UtilityItemActionKind.OxygenLeakRepair, oxygenLeakAmount),
                (UtilityItemActionKind.OxygenGeneratorRepair, regularAmount),
                (UtilityItemActionKind.GravityGeneratorRepair, regularAmount)
            };
            if (itemData == null
                || expectations.Any(expectation =>
                    !itemData.TryGetActionProfile(
                        expectation.Item1,
                        out var profile)
                    || profile.Amount != expectation.Item2
                    || profile.DurabilityCost != 1))
            {
                errors.Add($"wrench_profile_exact item={itemId}");
            }
        }

        private static UtilityItemPrefabData FindItemData(string itemId)
        {
            return AssetDatabase.FindAssets(
                    "t:UtilityItemPrefabData",
                    new[]
                    {
                        "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems"
                    })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<
                    UtilityItemPrefabData>(path))
                .FirstOrDefault(candidate => candidate != null
                    && candidate.ItemId == itemId);
        }

        private static void ValidateBatteryProfiles(
            string itemId,
            ICollection<string> errors)
        {
            var itemData = FindItemData(itemId);
            if (itemData == null
                || !itemData.TryGetActionProfile(
                    UtilityItemActionKind.PowerRestore,
                    out var restore)
                || restore.Amount != 100
                || restore.DurabilityCost != 100
                || !itemData.TryGetActionProfile(
                    UtilityItemActionKind.BatteryDischarge,
                    out var discharge)
                || discharge.Amount != 20
                || discharge.DurabilityCost != 100)
            {
                errors.Add($"battery_profile_exact item={itemId}");
            }
        }
    }
}
