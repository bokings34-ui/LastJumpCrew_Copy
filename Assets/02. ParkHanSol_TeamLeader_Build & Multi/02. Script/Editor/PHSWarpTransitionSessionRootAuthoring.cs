#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Owns the warp transition UI from the persistent session root, not the map-scoped
    /// legacy team integration.  This keeps the view alive through map/shop scene loads.
    /// </summary>
    public static class PHSWarpTransitionSessionRootAuthoring
    {
        private const string SessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string WarpTransitionPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_WarpTransitionSystem.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Persistent Warp Transition")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(SessionRootPrefabPath);
            try
            {
                var coordinator = root.GetComponent<NetworkRunFlowCoordinator>()
                    ?? throw new InvalidOperationException(
                        "PHS_WARP_SESSION_AUTHOR_FAILED reason=run_flow_missing");
                var presenters = root.GetComponentsInChildren<WarpTransitionPresenter>(true);
                if (presenters.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_SESSION_AUTHOR_FAILED reason=duplicate_presenters count={presenters.Length}");
                }

                var presenter = presenters.Length == 1 ? presenters[0] : null;
                if (presenter == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(WarpTransitionPrefabPath)
                        ?? throw new InvalidOperationException(
                            "PHS_WARP_SESSION_AUTHOR_FAILED reason=warp_prefab_missing");
                    var instance = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
                    presenter = instance != null
                        ? instance.GetComponentInChildren<WarpTransitionPresenter>(true)
                        : null;
                }

                if (presenter == null)
                {
                    throw new InvalidOperationException(
                        "PHS_WARP_SESSION_AUTHOR_FAILED reason=presenter_missing_after_instance");
                }

                presenter.transform.SetAsLastSibling();
                presenter.gameObject.SetActive(true);
                var data = new SerializedObject(presenter);
                data.FindProperty("runFlowCoordinator").objectReferenceValue = coordinator;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SessionRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log("PHS_WARP_SESSION_AUTHOR_OK owner=session_root presenter=single coordinator=assigned");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Persistent Warp Transition")]
        public static void ValidateOrThrow()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(SessionRootPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_WARP_SESSION_VALIDATION_FAILED reason=session_root_missing");
            var coordinator = root.GetComponent<NetworkRunFlowCoordinator>();
            var presenters = root.GetComponentsInChildren<WarpTransitionPresenter>(true);
            if (coordinator == null || presenters.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_WARP_SESSION_VALIDATION_FAILED reason=ownership_invalid coordinator={coordinator != null} presenters={presenters.Length}");
            }

            var data = new SerializedObject(presenters[0]);
            if (data.FindProperty("runFlowCoordinator")?.objectReferenceValue != coordinator)
            {
                throw new InvalidOperationException(
                    "PHS_WARP_SESSION_VALIDATION_FAILED reason=coordinator_reference_invalid");
            }

            if (!presenters[0].gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "PHS_WARP_SESSION_VALIDATION_FAILED reason=presenter_inactive");
            }

            Debug.Log("PHS_WARP_SESSION_VALIDATION_OK owner=session_root presenter=single");
        }
    }
}
#endif
