using System;
using System.Collections.Generic;
using System.IO;
using LastJumpCrew.ParkHanSol.Items;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUtilityItemFeedbackValidator
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string FeedbackSource =
            Root + "/02. Script/Items/PHSNetworkItemUseFeedbackController.cs";

        private static readonly string[] PlayerPrefabPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Utility Item Feedback")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidatePlayerPrefabs(errors);
            ValidateFeedbackMapping(errors);
            ValidateFeedbackPalette(errors);
            ValidateSuccessRoutes(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UTILITY_ITEM_FEEDBACK_VALIDATE_FAILED "
                    + string.Join(" | ", errors));
            }

            Debug.Log(
                "PHS_UTILITY_ITEM_FEEDBACK_VALIDATE_OK "
                + "prefabs=2 routes=event,ship,fire,battery cleanup=timed");
        }

        private static void ValidatePlayerPrefabs(ICollection<string> errors)
        {
            foreach (var path in PlayerPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    errors.Add($"player_prefab_missing:{path}");
                    continue;
                }

                try
                {
                    var feedbacks =
                        root.GetComponentsInChildren<PHSNetworkItemUseFeedbackController>(true);
                    if (feedbacks.Length != 1)
                    {
                        errors.Add(
                            $"feedback_component_count:{path}:{feedbacks.Length}");
                        continue;
                    }

                    var serialized = new SerializedObject(feedbacks[0]);
                    RequireObject(serialized, "sphereRangePrefab", path, errors);
                    RequireObject(serialized, "castRangePrefab", path, errors);
                    RequireObject(serialized, "targetFeedbackPrefab", path, errors);
                    RequirePositive(serialized, "rangeLifetimeSeconds", path, errors);
                    RequirePositive(serialized, "targetLifetimeSeconds", path, errors);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateFeedbackMapping(ICollection<string> errors)
        {
            var source = ReadSource(FeedbackSource, errors);
            RequireContains(
                source,
                "UtilityItemActionKind.FireSuppression =>\n                    PHSItemUseFeedbackKind.FireExtinguisher",
                "mapping_extinguisher_missing",
                errors);
            RequireContains(
                source,
                "UtilityItemActionKind.PowerRestore or\n                UtilityItemActionKind.BatteryDischarge =>\n                    PHSItemUseFeedbackKind.Battery",
                "mapping_battery_missing",
                errors);
            RequireContains(
                source,
                "UtilityItemActionKind.DeviceRepair or",
                "mapping_wrench_actions_missing",
                errors);
            RequireContains(
                source,
                "PHSItemUseFeedbackKind.Wrench",
                "mapping_wrench_kind_missing",
                errors);
            RequireContains(
                source,
                "Destroy(targetInstance, targetLifetimeSeconds);",
                "target_cleanup_missing",
                errors);
            RequireNoForbiddenSharedRoute(source, FeedbackSource, errors);
        }

        private static void ValidateSuccessRoutes(ICollection<string> errors)
        {
            ValidateRoute(
                Root + "/02. Script/Multiplayer/Events/NetworkEventCoordinator.cs",
                "private bool TryApplyEffectRepairServer(",
                "private bool RejectRepair(",
                1,
                new[]
                {
                    "item_profile_mismatch",
                    "duplicate_sequence",
                    "distance",
                    "TryCommitHeldItemActionServer("
                },
                errors);
            ValidateRoute(
                Root + "/02. Script/Multiplayer/ShipAccidents/PHSNetworkShipAccidentCoordinator.cs",
                "private bool CompleteRepairRequest(",
                "private void ApplyPeriodicDamage(",
                2,
                new[]
                {
                    "item_profile_mismatch",
                    "sequence_replayed",
                    "reason=distance",
                    "TryCommitHeldItemActionServer("
                },
                errors);
            ValidateRoute(
                Root + "/02. Script/Multiplayer/Incidents/Fire/PHSNetworkFireCoordinator.cs",
                "public bool TrySuppressPatchServer(",
                "private int FindPatchSnapshotIndex(",
                1,
                new[]
                {
                    "server_item_profile_mismatch",
                    "suppression_sequence_replayed",
                    "suppression_distance_exceeded",
                    "TryCommitHeldItemActionServer("
                },
                errors);
            ValidateRoute(
                Root + "/02. Script/Interaction/BatteryInsertPowerStationSocket.cs",
                "private bool TryInstallBatteryOnServer(",
                "private void SendResult(",
                1,
                new[]
                {
                    "player_too_far",
                    "item_record_mismatch",
                    "duplicate_request",
                    "TryConsumeHeldItemServer(",
                    "shipState.TryRestorePowerWithBattery(out reason)"
                },
                errors);
        }

        private static void ValidateFeedbackPalette(ICollection<string> errors)
        {
            var source = ReadSource(FeedbackSource, errors);
            RequireContains(
                source,
                "TeamRepairToolVisualPalette.GetFeedbackRangeColor(kind)",
                "feedback_range_palette_missing",
                errors);
            RequireContains(
                source,
                "TeamRepairToolVisualPalette.GetFeedbackTargetColor(kind)",
                "feedback_target_palette_missing",
                errors);
            var wrench = TeamRepairToolVisualPalette.Wrench;
            if (!Mathf.Approximately(wrench.r, 0.68f)
                || !Mathf.Approximately(wrench.g, 0.32f)
                || !Mathf.Approximately(wrench.b, 1f)
                || !Mathf.Approximately(wrench.a, 1f))
            {
                errors.Add("feedback_wrench_palette_not_purple");
            }
        }

        private static void ValidateRoute(
            string path,
            string startMarker,
            string endMarker,
            int expectedPublishCount,
            IReadOnlyList<string> requiredBeforePublish,
            ICollection<string> errors)
        {
            var source = ReadSource(path, errors);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = start < 0
                ? -1
                : source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (start < 0 || end <= start)
            {
                errors.Add($"route_slice_missing:{path}");
                return;
            }

            var route = source.Substring(start, end - start);
            const string publishMarker = "PublishConfirmedTargetImpactServer(";
            var firstPublish = route.IndexOf(publishMarker, StringComparison.Ordinal);
            var publishCount = CountOccurrences(route, publishMarker);
            if (publishCount != expectedPublishCount)
            {
                errors.Add(
                    $"publish_count:{path}:expected={expectedPublishCount}:actual={publishCount}");
                return;
            }

            foreach (var marker in requiredBeforePublish)
            {
                var markerIndex = route.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0 || markerIndex >= firstPublish)
                {
                    errors.Add($"validation_order:{path}:{marker}");
                }
            }

            RequireNoForbiddenSharedRoute(route, path, errors);
        }

        private static string ReadSource(string assetPath, ICollection<string> errors)
        {
            var absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                errors.Add($"source_missing:{assetPath}");
                return string.Empty;
            }

            return File.ReadAllText(absolutePath).Replace("\r\n", "\n");
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static void RequireObject(
            SerializedObject serialized,
            string propertyName,
            string path,
            ICollection<string> errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                errors.Add($"feedback_reference_missing:{path}:{propertyName}");
            }
        }

        private static void RequirePositive(
            SerializedObject serialized,
            string propertyName,
            string path,
            ICollection<string> errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.floatValue <= 0f)
            {
                errors.Add($"feedback_lifetime_invalid:{path}:{propertyName}");
            }
        }

        private static void RequireContains(
            string source,
            string expected,
            string reason,
            ICollection<string> errors)
        {
            if (!source.Contains(expected, StringComparison.Ordinal))
            {
                errors.Add(reason);
            }
        }

        private static void RequireNoForbiddenSharedRoute(
            string source,
            string path,
            ICollection<string> errors)
        {
            if (source.Contains("Assets/06.", StringComparison.Ordinal)
                || source.Contains("NetworkPlayerCombatController", StringComparison.Ordinal)
                || source.Contains("CombatHitResolver", StringComparison.Ordinal))
            {
                errors.Add($"assets06_route_reference:{path}");
            }
        }
    }
}
