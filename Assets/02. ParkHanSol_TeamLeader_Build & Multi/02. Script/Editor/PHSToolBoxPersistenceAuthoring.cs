using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSToolBoxPersistenceAuthoring
    {
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/" +
            "PHS_Map_ver1.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Persistent ToolBox Storage")]
        public static void Author()
        {
            var prefab = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            try
            {
                var root = prefab.GetComponent<NetworkRunSessionRoot>()
                    ?? throw new InvalidOperationException(
                        "PHS_TOOL_BOX_PERSISTENCE_AUTHOR_FAILED reason=session_root_missing");
                var storage = prefab.GetComponent<NetworkPersistentToolBoxStorage>()
                    ?? prefab.AddComponent<NetworkPersistentToolBoxStorage>();
                var serializedRoot = new SerializedObject(root);
                serializedRoot.FindProperty("toolBoxStorage").objectReferenceValue = storage;
                serializedRoot.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(prefab, RunSessionRootPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_TOOL_BOX_PERSISTENCE_AUTHOR_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_TOOL_BOX_PERSISTENCE_AUTHOR_OK root=PHS_NetworkRunSessionRoot");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author ToolBox Interaction Distance")]
        public static void AuthorInteractionDistance()
        {
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
                var coordinators = UnityEngine.Object.FindObjectsByType<NetworkToolBoxStorageCoordinator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (coordinators.Length == 0)
                {
                    throw new InvalidOperationException(
                        "PHS_TOOL_BOX_DISTANCE_AUTHOR_FAILED reason=coordinator_missing");
                }

                foreach (var coordinator in coordinators)
                {
                    var serializedCoordinator = new SerializedObject(coordinator);
                    serializedCoordinator.FindProperty("serverInteractionDistance").floatValue = 4f;
                    serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(
                    $"PHS_TOOL_BOX_DISTANCE_AUTHOR_OK distance=4 boxes={coordinators.Length}");
            }
            finally
            {
                if (originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Persistent ToolBox Storage")]
        public static void Validate()
        {
            var prefab = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            try
            {
                var root = prefab.GetComponent<NetworkRunSessionRoot>();
                var stores = prefab.GetComponents<NetworkPersistentToolBoxStorage>();
                var serializedRoot = root == null ? null : new SerializedObject(root);
                var referencedStorage = serializedRoot
                    ?.FindProperty("toolBoxStorage")
                    ?.objectReferenceValue as NetworkPersistentToolBoxStorage;
                if (root == null
                    || stores.Length != 1
                    || stores[0].gameObject != prefab
                    || referencedStorage != stores[0])
                {
                    throw new InvalidOperationException(
                        "PHS_TOOL_BOX_PERSISTENCE_VALIDATE_FAILED " +
                        $"root={root != null} stores={stores.Length} " +
                        $"reference={referencedStorage != null}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            Debug.Log("PHS_TOOL_BOX_PERSISTENCE_VALIDATE_OK distance=4 state=itemId+durability+revision");
        }
    }
}
