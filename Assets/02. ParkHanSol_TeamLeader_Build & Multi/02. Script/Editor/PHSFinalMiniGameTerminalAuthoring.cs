using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSFinalMiniGameTerminalAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const float InteractionRange = 1.5f;

        private static readonly string[] TerminalPrefabPaths =
        {
            Root + "/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_Final_WireTerminal.prefab",
            Root + "/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_Final_PowerTerminal.prefab",
            Root + "/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_Final_CannonTerminal.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Team Mini Game Interaction Range")]
        public static void Author()
        {
            foreach (var path in TerminalPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var terminal = RequireSingle<PHSFinalMiniGameTerminal>(root, path);
                    var collider = RequireSingle<Collider>(root, path);
                    var data = new SerializedObject(terminal);
                    data.FindProperty("interactionRangeCollider").objectReferenceValue = collider;
                    data.FindProperty("interactionRange").floatValue = InteractionRange;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log(
                "PHS_FINAL_MINIGAME_INTERACTION_RANGE_AUTHOR_OK terminals=3 " +
                $"range={InteractionRange:F2} collider=inspector");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Team Mini Game Interaction Range")]
        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            foreach (var path in TerminalPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var terminal = RequireSingle<PHSFinalMiniGameTerminal>(root, path);
                    var collider = RequireSingle<Collider>(root, path);
                    var data = new SerializedObject(terminal);
                    var configuredCollider = data.FindProperty("interactionRangeCollider")
                        ?.objectReferenceValue as Collider;
                    var configuredRange = data.FindProperty("interactionRange")?.floatValue ?? 0f;
                    if (configuredCollider != collider || Mathf.Abs(configuredRange - InteractionRange) > 0.001f)
                    {
                        errors.Add(
                            $"terminal_range_invalid path={path} collider=" +
                            $"{(configuredCollider == null ? "missing" : configuredCollider.name)} " +
                            $"range={configuredRange:F2}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_FINAL_MINIGAME_INTERACTION_RANGE_VALIDATION_FAILED\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log(
                "PHS_FINAL_MINIGAME_INTERACTION_RANGE_VALIDATION_OK terminals=3 " +
                $"range={InteractionRange:F2}");
        }

        private static T RequireSingle<T>(GameObject root, string path)
            where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "PHS_FINAL_MINIGAME_INTERACTION_RANGE_AUTHOR_FAILED " +
                    $"reason=component_count type={typeof(T).Name} count={matches.Length} path={path}");
            }

            return matches[0];
        }
    }
}
