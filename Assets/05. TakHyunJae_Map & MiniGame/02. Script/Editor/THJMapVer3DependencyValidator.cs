#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.TakHyunJae.Editor
{
    public static class THJMapVer3DependencyValidator
    {
        private const string MenuPath =
            "Tools/TakHyunJae/Validate Map Ver3 Dependencies";

        private static readonly string[] PrefabPaths =
        {
            "Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/SpaceShip_SpaceCrew_Inside.prefab",
            "Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/Spaceship_SpaceCrew_Outside.prefab"
        };

        private static readonly Regex GuidPattern = new Regex(
            @"guid:\s*([0-9a-f]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem(MenuPath)]
        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            var referencedGuids = new HashSet<string>(StringComparer.Ordinal);
            var missingScriptCount = 0;

            foreach (var prefabPath in PrefabPaths)
            {
                ValidateSerializedGuids(prefabPath, referencedGuids, errors);
                missingScriptCount += CountMissingScripts(prefabPath, errors);
            }

            if (missingScriptCount > 0)
            {
                errors.Add($"missing_scripts={missingScriptCount}");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "THJ_MAP_VER3_DEPENDENCY_VALIDATE_FAILED errors=" +
                    string.Join("|", errors));
            }

            Debug.Log(
                "THJ_MAP_VER3_DEPENDENCY_VALIDATE_OK " +
                $"prefabs={PrefabPaths.Length} " +
                $"resolvedGuids={referencedGuids.Count} " +
                "missingScripts=0");
        }

        private static void ValidateSerializedGuids(
            string prefabPath,
            ISet<string> referencedGuids,
            ICollection<string> errors)
        {
            var fullPath = Path.GetFullPath(prefabPath);
            if (!File.Exists(fullPath))
            {
                errors.Add($"prefab_missing:path={prefabPath}");
                return;
            }

            var text = File.ReadAllText(fullPath);
            foreach (Match match in GuidPattern.Matches(text))
            {
                var guid = match.Groups[1].Value;
                referencedGuids.Add(guid);
                if (IsBuiltInGuid(guid))
                {
                    continue;
                }

                var resolvedPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    errors.Add(
                        $"guid_unresolved:prefab={prefabPath}:guid={guid}");
                }
            }
        }

        private static int CountMissingScripts(
            string prefabPath,
            ICollection<string> errors)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                return contents
                    .GetComponentsInChildren<Transform>(true)
                    .Sum(transform =>
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject));
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"prefab_load_failed:path={prefabPath}:" +
                    $"type={exception.GetType().Name}");
                return 0;
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static bool IsBuiltInGuid(string guid)
        {
            return string.Equals(
                       guid,
                       "0000000000000000e000000000000000",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       guid,
                       "0000000000000000f000000000000000",
                       StringComparison.Ordinal);
        }
    }
}
#endif
