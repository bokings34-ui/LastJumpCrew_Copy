using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkLobbyUiCleanupAuthoring
    {
        private const string LobbyPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string FrontendRootName =
            "PHS_NetworkLobbyCustomizationFrontend";
        private const string CustomizationPanelName = "CustomizationPanel";
        private const string ExtrusionName = "PHS_M3D_Extrusion";

        [MenuItem("Tools/ParkHanSol/BEAVER/Cleanup Network Lobby UI")]
        public static void Author()
        {
            RequirePrefab();
            var root = PrefabUtility.LoadPrefabContents(LobbyPrefabPath);
            try
            {
                var before = Capture(root);
                LogCount("before", before);

                var removedMissingScripts = RemoveAllMissingMonoBehaviours(root);
                var deletedObjects = DeleteOrphanExtrusions(root);
                SetRequiredActiveStates(root);

                var after = Capture(root);
                EnsureProtectedComponentsPreserved(before, after);
                if (after.MissingMonoBehaviours != 0
                    || after.OrphanExtrusions != 0)
                {
                    throw Failure(
                        "cleanup_incomplete " +
                        $"missing={after.MissingMonoBehaviours} " +
                        $"orphans={after.OrphanExtrusions}");
                }

                LogCount("after", after);
                PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
                Debug.Log(
                    "PHS_NETWORK_LOBBY_UI_CLEANUP_AUTHORING_OK " +
                    $"path={LobbyPrefabPath} " +
                    $"removedMissingScripts={removedMissingScripts} " +
                    $"deletedObjects={deletedObjects}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Count Network Lobby UI Cleanup")]
        public static void Count()
        {
            RequirePrefab();
            var root = PrefabUtility.LoadPrefabContents(LobbyPrefabPath);
            try
            {
                LogCount("validator", Capture(root));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int RemoveAllMissingMonoBehaviours(GameObject root)
        {
            var removedCount = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var owner = transform.gameObject;
                var missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(owner);
                if (missingCount == 0)
                {
                    continue;
                }

                removedCount += missingCount;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(owner);
            }

            return removedCount;
        }

        private static int DeleteOrphanExtrusions(GameObject root)
        {
            var candidates = root
                .GetComponentsInChildren<Transform>(true)
                .Where(IsOrphanExtrusion)
                .OrderByDescending(GetDepth)
                .ToArray();
            var deletedCount = 0;
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                deletedCount++;
            }

            return deletedCount;
        }

        private static bool IsOrphanExtrusion(Transform candidate)
        {
            if (candidate.name != ExtrusionName || candidate.childCount != 0)
            {
                return false;
            }

            return candidate
                .GetComponents<Component>()
                .Count(component => component != null) == 1;
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private static void SetRequiredActiveStates(GameObject root)
        {
            var frontend = RequireSingleNamed(root, FrontendRootName);
            var customizationPanel = RequireSingleNamed(
                frontend.gameObject,
                CustomizationPanelName);
            frontend.gameObject.SetActive(true);
            customizationPanel.gameObject.SetActive(false);
        }

        private static Transform RequireSingleNamed(GameObject root, string name)
        {
            var matches = root
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == name)
                .ToArray();
            if (matches.Length != 1)
            {
                throw Failure($"object_count name={name} count={matches.Length}");
            }

            return matches[0];
        }

        private static CleanupCount Capture(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var components = root
                .GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .ToArray();
            var frontend = RequireSingleNamed(root, FrontendRootName);
            var customizationPanel = RequireSingleNamed(
                frontend.gameObject,
                CustomizationPanelName);

            return new CleanupCount(
                transforms.Sum(transform =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject)),
                transforms.Count(IsOrphanExtrusion),
                transforms.Length,
                components.Count(component => component is Button),
                components.Count(component => component is TMP_Text),
                components.Count(component => component is MonoBehaviour),
                components.Count(component =>
                    component.GetType().Name.EndsWith(
                        "Controller",
                        StringComparison.Ordinal)),
                frontend.gameObject.activeSelf,
                customizationPanel.gameObject.activeSelf);
        }

        private static void EnsureProtectedComponentsPreserved(
            CleanupCount before,
            CleanupCount after)
        {
            if (before.Buttons != after.Buttons
                || before.TmpTexts != after.TmpTexts
                || before.ValidMonoBehaviours != after.ValidMonoBehaviours
                || before.Controllers != after.Controllers)
            {
                throw Failure(
                    "protected_component_count_changed " +
                    $"buttons={before.Buttons}->{after.Buttons} " +
                    $"tmp={before.TmpTexts}->{after.TmpTexts} " +
                    $"monoBehaviours={before.ValidMonoBehaviours}->{after.ValidMonoBehaviours} " +
                    $"controllers={before.Controllers}->{after.Controllers}");
            }
        }

        private static void LogCount(string phase, CleanupCount count)
        {
            Debug.Log(
                "PHS_NETWORK_LOBBY_UI_CLEANUP_COUNT " +
                $"phase={phase} " +
                $"missingMonoBehaviours={count.MissingMonoBehaviours} " +
                $"orphanExtrusions={count.OrphanExtrusions} " +
                $"gameObjects={count.GameObjects} " +
                $"buttons={count.Buttons} " +
                $"tmp={count.TmpTexts} " +
                $"monoBehaviours={count.ValidMonoBehaviours} " +
                $"controllers={count.Controllers} " +
                $"frontendActive={count.FrontendActive} " +
                $"customizationPanelActive={count.CustomizationPanelActive}");
        }

        private static void RequirePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath) == null)
            {
                throw Failure($"prefab_missing path={LobbyPrefabPath}");
            }
        }

        private static InvalidOperationException Failure(string reason)
        {
            return new InvalidOperationException(
                $"PHS_NETWORK_LOBBY_UI_CLEANUP_AUTHORING_FAILED reason={reason}");
        }

        private readonly struct CleanupCount
        {
            public CleanupCount(
                int missingMonoBehaviours,
                int orphanExtrusions,
                int gameObjects,
                int buttons,
                int tmpTexts,
                int validMonoBehaviours,
                int controllers,
                bool frontendActive,
                bool customizationPanelActive)
            {
                MissingMonoBehaviours = missingMonoBehaviours;
                OrphanExtrusions = orphanExtrusions;
                GameObjects = gameObjects;
                Buttons = buttons;
                TmpTexts = tmpTexts;
                ValidMonoBehaviours = validMonoBehaviours;
                Controllers = controllers;
                FrontendActive = frontendActive;
                CustomizationPanelActive = customizationPanelActive;
            }

            public int MissingMonoBehaviours { get; }
            public int OrphanExtrusions { get; }
            public int GameObjects { get; }
            public int Buttons { get; }
            public int TmpTexts { get; }
            public int ValidMonoBehaviours { get; }
            public int Controllers { get; }
            public bool FrontendActive { get; }
            public bool CustomizationPanelActive { get; }
        }
    }
}
