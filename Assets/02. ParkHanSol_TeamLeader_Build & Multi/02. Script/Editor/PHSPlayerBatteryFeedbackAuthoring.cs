using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSPlayerBatteryFeedbackAuthoring
    {
        private const string SourcePlayerPath =
            "Assets/03. SeoBoGyeong_Game Economy/03. Prefab/Test/PHS_CuteWhiteGhost_Player.prefab";
        private const string TargetPlayerPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string BatteryOriginName = "BatteryThrowOrigin";
        private const string FeedbackName = "PHS_BatteryUseFeedback";

        [MenuItem("Tools/ParkHanSol/BEAVER/Recover Player Battery Feedback")]
        public static void Author()
        {
            var sourceRoot = PrefabUtility.LoadPrefabContents(SourcePlayerPath);
            var targetRoot = PrefabUtility.LoadPrefabContents(TargetPlayerPath);
            try
            {
                var sourceEffect = RequireSingleNamedParticle(sourceRoot.transform, FeedbackName);
                var targetOrigin = RequireSingleNamedTransform(targetRoot.transform, BatteryOriginName);
                var targetEffect = RequireOrCopyEffect(sourceEffect, targetOrigin);
                var combat = targetRoot.GetComponentInChildren<NetworkPlayerCombatController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=combat_controller_missing");
                SetReference(combat, "batteryUseEffect", targetEffect);
                if (PrefabUtility.SaveAsPrefabAsset(targetRoot, TargetPlayerPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TargetPlayerPath, ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            Debug.Log(
                "PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_OK effect=PHS_BatteryUseFeedback " +
                "origin=BatteryThrowOrigin source=team_prefab");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Player Battery Feedback")]
        public static void ValidateOrThrow()
        {
            var targetRoot = PrefabUtility.LoadPrefabContents(TargetPlayerPath);
            try
            {
                var origin = RequireSingleNamedTransform(targetRoot.transform, BatteryOriginName);
                var combat = targetRoot.GetComponentInChildren<NetworkPlayerCombatController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_PLAYER_BATTERY_FEEDBACK_VALIDATE_FAILED reason=combat_controller_missing");
                var effect = new SerializedObject(combat).FindProperty("batteryUseEffect")
                    ?.objectReferenceValue as ParticleSystem;
                if (effect == null || effect.name != FeedbackName || !effect.transform.IsChildOf(origin))
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_BATTERY_FEEDBACK_VALIDATE_FAILED " +
                        "reason=effect_reference_invalid expected=BatteryThrowOrigin/PHS_BatteryUseFeedback");
                }

                var count = origin.GetComponentsInChildren<ParticleSystem>(true)
                    .Count(candidate => candidate.name == FeedbackName);
                if (count != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_PLAYER_BATTERY_FEEDBACK_VALIDATE_FAILED reason=effect_count actual={count}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        private static ParticleSystem RequireOrCopyEffect(ParticleSystem source, Transform targetOrigin)
        {
            var matches = targetOrigin.GetComponentsInChildren<ParticleSystem>(true)
                .Where(candidate => candidate.name == FeedbackName)
                .ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=duplicate_effect count={matches.Length}");
            }

            if (matches.Length == 1)
            {
                return matches[0];
            }

            var instance = UnityEngine.Object.Instantiate(source.gameObject, targetOrigin, false);
            instance.name = FeedbackName;
            return instance.GetComponent<ParticleSystem>()
                ?? throw new InvalidOperationException(
                    "PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=copied_particle_missing");
        }

        private static ParticleSystem RequireSingleNamedParticle(Transform root, string objectName)
        {
            var matches = root.GetComponentsInChildren<ParticleSystem>(true)
                .Where(candidate => candidate.name == objectName)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=source_effect_count actual={matches.Length}");
            }

            return matches[0];
        }

        private static Transform RequireSingleNamedTransform(Transform root, string objectName)
        {
            var matches = root.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == objectName)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=named_transform_count name={objectName} actual={matches.Length}");
            }

            return matches[0];
        }

        private static void SetReference(Component component, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_PLAYER_BATTERY_FEEDBACK_AUTHOR_FAILED reason=property_missing name={propertyName}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
