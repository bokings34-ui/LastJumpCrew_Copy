using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Authors the exterior arrival orientation and reuses the tutorial
    /// TargetIndicator hierarchy for the map HUD's return-to-ship navigator.
    /// </summary>
    public static class PHSExteriorSpawnNavigationAuthoring
    {
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string ArrivalName = "PHS_ExteriorDebrisArrivalPoint";
        private const string ReturnPortalName =
            ExteriorTestTeleportInteractable.DebrisReturnPortalName;
        private const string NavigationUiName = "PHS_ExteriorReturnNavigationUI";

        [MenuItem("Tools/ParkHanSol/Author Exterior Spawn And Return Navigation")]
        public static void Author()
        {
            var mapScene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var templateScene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Additive);
            try
            {
                var templateRoot = ResolveTutorialTargetIndicator(templateScene);
                var binder = FindSingle<PHSMapPlayerHudBinder>(mapScene, "map_hud_binder");
                var arrival = FindNamedTransform(mapScene, ArrivalName);
                var returnPortal = FindSingle<ExteriorTestTeleportInteractable>(
                    mapScene,
                    "return_portal",
                    candidate => candidate.name == ReturnPortalName);
                var hudRoot = ResolveHudRoot(binder);

                ConfigureArrivalFacingExterior(arrival, returnPortal.transform);
                var navigationUi = ReplaceNavigationUi(templateRoot, hudRoot);
                var navigationText = navigationUi
                    .GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(candidate => candidate.name == "TargetIndicatorText");
                Require(navigationText != null, "tutorial_target_indicator_text_missing");

                var navigation = binder.GetComponent<PHSExteriorReturnNavigation>()
                    ?? binder.gameObject.AddComponent<PHSExteriorReturnNavigation>();
                var serializedNavigation = new SerializedObject(navigation);
                serializedNavigation.FindProperty("targetIndicatorRoot")
                    .objectReferenceValue = navigationUi;
                serializedNavigation.FindProperty("targetIndicatorText")
                    .objectReferenceValue = navigationText;
                serializedNavigation.FindProperty("returnPortal")
                    .objectReferenceValue = returnPortal;
                serializedNavigation.FindProperty("arrivedDistance").floatValue =
                    returnPortal.ServerInteractionDistance;
                serializedNavigation.ApplyModifiedPropertiesWithoutUndo();
                navigationUi.SetActive(false);

                EditorUtility.SetDirty(arrival);
                EditorUtility.SetDirty(navigation);
                EditorSceneManager.MarkSceneDirty(mapScene);
                EditorSceneManager.SaveScene(mapScene);
                ValidateOrThrow();
                Debug.Log(
                    "PHS_EXTERIOR_SPAWN_NAVIGATION_AUTHOR_OK " +
                    $"arrival={arrival.position} returnPortal={returnPortal.name} " +
                    $"ui={navigationUi.name}");
            }
            finally
            {
                if (templateScene.IsValid() && templateScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(templateScene, true);
                }
            }
        }

        [MenuItem("Tools/ParkHanSol/Validate Exterior Spawn And Return Navigation")]
        public static void ValidateOrThrow()
        {
            var mapScene = SceneManager.GetActiveScene().path == MapScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var arrival = FindNamedTransform(mapScene, ArrivalName);
            var returnPortal = FindSingle<ExteriorTestTeleportInteractable>(
                mapScene,
                "return_portal",
                candidate => candidate.name == ReturnPortalName);
            var binder = FindSingle<PHSMapPlayerHudBinder>(mapScene, "map_hud_binder");
            var navigation = binder.GetComponent<PHSExteriorReturnNavigation>();
            Require(navigation != null, "return_navigation_component_missing");
            Require(navigation.IsConfigured, "return_navigation_reference_missing");

            var outward = Vector3.ProjectOnPlane(
                arrival.position - returnPortal.transform.position,
                Vector3.up).normalized;
            Require(outward.sqrMagnitude > 0.01f, "arrival_outward_direction_invalid");
            Require(
                Vector3.Dot(arrival.forward, outward) > 0.99f,
                "arrival_rotation_not_facing_exterior");

            var serializedNavigation = new SerializedObject(navigation);
            Require(
                serializedNavigation.FindProperty("returnPortal").objectReferenceValue
                    == returnPortal,
                "return_navigation_portal_reference_invalid");
            var uiRoot = serializedNavigation.FindProperty("targetIndicatorRoot")
                .objectReferenceValue as GameObject;
            Require(
                uiRoot != null && uiRoot.name == NavigationUiName,
                "return_navigation_tutorial_ui_missing");

            Debug.Log(
                "PHS_EXTERIOR_SPAWN_NAVIGATION_VALIDATE_OK " +
                $"arrivalForward={arrival.forward} outward={outward} " +
                $"returnDistance={returnPortal.ServerInteractionDistance:0.##}");
        }

        private static GameObject ResolveTutorialTargetIndicator(Scene tutorialScene)
        {
            var targetText = Resources.FindObjectsOfTypeAll<NetworkTutorialRoomController>()
                .Where(candidate => candidate.gameObject.scene == tutorialScene)
                .Select(candidate => new SerializedObject(candidate)
                    .FindProperty("targetIndicatorText").objectReferenceValue as TMP_Text)
                .FirstOrDefault(candidate => candidate != null);
            Require(targetText != null, "tutorial_target_indicator_text_reference_missing");
            var root = targetText.transform.parent != null
                ? targetText.transform.parent.gameObject
                : null;
            Require(root != null && root.name == "TargetIndicatorBackground",
                "tutorial_target_indicator_root_missing");
            return root;
        }

        private static Transform ResolveHudRoot(PHSMapPlayerHudBinder binder)
        {
            var serializedBinder = new SerializedObject(binder);
            var presenter = serializedBinder.FindProperty("playHudPresenter")
                .objectReferenceValue as Component;
            Require(presenter != null, "map_hud_presenter_missing");
            var hudRoot = FindChild(presenter.transform, "Hud Root");
            Require(hudRoot != null, "map_hud_root_missing");
            return hudRoot;
        }

        private static GameObject ReplaceNavigationUi(
            GameObject templateRoot,
            Transform hudRoot)
        {
            foreach (var existing in hudRoot
                         .GetComponentsInChildren<Transform>(true)
                         .Where(candidate => candidate.name == NavigationUiName)
                         .Select(candidate => candidate.gameObject)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var instance = UnityEngine.Object.Instantiate(templateRoot, hudRoot, false);
            instance.name = NavigationUiName;
            return instance;
        }

        private static void ConfigureArrivalFacingExterior(
            Transform arrival,
            Transform returnPortal)
        {
            var outward = Vector3.ProjectOnPlane(
                arrival.position - returnPortal.position,
                Vector3.up);
            Require(outward.sqrMagnitude > 0.01f, "arrival_outward_direction_invalid");
            arrival.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
        }

        private static Transform FindNamedTransform(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == name)
                .ToArray();
            Require(matches.Length == 1, $"{name}_count={matches.Length}");
            return matches[0];
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static T FindSingle<T>(
            Scene scene,
            string label,
            Func<T, bool> predicate = null) where T : Component
        {
            var matches = Resources.FindObjectsOfTypeAll<T>()
                .Where(candidate => candidate.gameObject.scene == scene)
                .Where(candidate => predicate == null || predicate(candidate))
                .ToArray();
            Require(matches.Length == 1, $"{label}_count={matches.Length}");
            return matches[0];
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_EXTERIOR_SPAWN_NAVIGATION_FAILED reason={reason}");
            }
        }
    }
}
