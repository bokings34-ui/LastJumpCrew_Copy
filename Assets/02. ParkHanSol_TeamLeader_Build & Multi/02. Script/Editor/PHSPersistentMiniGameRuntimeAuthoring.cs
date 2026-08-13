using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Keeps the team mini-game UI alive with the spawned run session, instead of the
    /// disabled legacy integration object that belongs to the map scene.
    /// </summary>
    public static class PHSPersistentMiniGameRuntimeAuthoring
    {
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";

        private const string MiniGameRuntimePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/" +
            "Prefab/Integration0716/PHS_MiniGameRuntimeSystem.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Persistent Mini Game Runtime")]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_MINIGAME_AUTHOR_FAILED reason=play_mode_active");
            }

            var runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniGameRuntimePrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_MINIGAME_AUTHOR_FAILED reason=runtime_prefab_missing");
            var runRoot = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            var changed = false;
            try
            {
                var managers = runRoot.GetComponentsInChildren<PHSMiniGameManager>(true);
                if (managers.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_MINIGAME_AUTHOR_FAILED reason=manager_duplicate count={managers.Length}");
                }

                if (managers.Length == 0)
                {
                    var runtime = PrefabUtility.InstantiatePrefab(runtimePrefab, runRoot.transform) as GameObject;
                    if (runtime == null)
                    {
                        throw new InvalidOperationException(
                            "PHS_PERSISTENT_MINIGAME_AUTHOR_FAILED reason=runtime_instantiate_failed");
                    }

                    runtime.name = "PHS_PersistentMiniGameRuntime";
                    runtime.SetActive(true);
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(runRoot, RunSessionRootPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(runRoot);
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            ValidateOrThrow();
            Debug.Log("PHS_PERSISTENT_MINIGAME_AUTHOR_OK owner=session_root manager=1 runtime=active");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Persistent Mini Game Runtime")]
        public static void ValidateOrThrow()
        {
            var runRoot = AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_MINIGAME_VALIDATE_FAILED reason=session_root_prefab_missing");
            var manager = runRoot.GetComponentInChildren<PHSMiniGameManager>(true);
            var managers = runRoot.GetComponentsInChildren<PHSMiniGameManager>(true);
            if (manager == null || managers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PERSISTENT_MINIGAME_VALIDATE_FAILED reason=manager_count count={managers.Length}");
            }

            if (!manager.gameObject.activeSelf || !manager.transform.IsChildOf(runRoot.transform))
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_MINIGAME_VALIDATE_FAILED reason=manager_not_active_session_child");
            }

            var data = new SerializedObject(manager);
            var canvasRoot = data.FindProperty("canvasRoot")?.objectReferenceValue as GameObject;
            var miniGames = data.FindProperty("miniGames");
            if (canvasRoot == null || miniGames == null || miniGames.arraySize < 3)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_MINIGAME_VALIDATE_FAILED reason=runtime_reference_invalid");
            }

            for (var index = 0; index < miniGames.arraySize; index++)
            {
                if (miniGames.GetArrayElementAtIndex(index).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_MINIGAME_VALIDATE_FAILED reason=mini_game_missing index={index}");
                }
            }

            Debug.Log(
                $"PHS_PERSISTENT_MINIGAME_VALIDATE_OK owner=session_root manager=1 games={miniGames.arraySize}");
        }
    }

}
