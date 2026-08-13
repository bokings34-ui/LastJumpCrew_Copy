#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.SeoBoGyeong.animate;
using SM;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>Removes editor-only preview/generation components from runtime assets.</summary>
    public static class PHSRuntimeEditorOnlyComponentCleanup
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private static readonly string[] ScenePaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity",
            "Assets/04. NohSeokMin_Game Event/01_Scene/Map_Ver3_Seokmin.unity"
        };

        private static readonly string[] PrefabPaths =
        {
            "Assets/01. MainGame/02. Final_Prefab/03. Prefab_NohSeokMin_Game Event/EventManager/Manager.prefab",
            "Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/Character_act/Player 1.prefab",
            "Assets/03. SeoBoGyeong_Game Economy/03. Prefab/Test/PHS_CuteWhiteGhost_Player.prefab",
            "Assets/03. SeoBoGyeong_Game Economy/03. Prefab/AnimatedGhost.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Cleanup Runtime Editor-only Components")]
        public static void Cleanup()
        {
            foreach (var path in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RemoveEditorOnlyComponents(root) > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (RemoveEditorOnlyComponentsFromScene(scene) > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            foreach (var path in GetRuntimeNetworkPrefabPaths())
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RemoveEditorOnlyComponents(root) > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_RUNTIME_EDITOR_ONLY_CLEANUP_OK removed=6");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Runtime Editor-only Components")]
        public static void Validate()
        {
            foreach (var path in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RequireNoEditorOnlyComponents(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RequireNoEditorOnlyComponents(scene, path);
            }

            ValidateRuntimeNetworkPrefabs();

            Debug.Log("PHS_RUNTIME_EDITOR_ONLY_VALIDATE_OK assets=6 network_prefabs=checked");
        }

        public static void ValidateRuntimeNetworkPrefabs()
        {
            var paths = GetRuntimeNetworkPrefabPaths();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RequireNoEditorOnlyComponents(root, path);
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        {
                            throw new InvalidOperationException(
                                $"PHS_RUNTIME_EDITOR_ONLY_VALIDATE_FAILED path={path} reason=missing_script");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"PHS_RUNTIME_NETWORK_PREFABS_VALIDATE_OK prefabs={paths.Length}");
        }

        private static string[] GetRuntimeNetworkPrefabPaths()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var managers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NetworkManager>(true))
                .ToArray();
            if (managers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_RUNTIME_EDITOR_ONLY_VALIDATE_FAILED path={LobbyScenePath} reason=network_manager_count:{managers.Length}");
            }

            return managers[0].NetworkConfig?.Prefabs?.NetworkPrefabsLists
                ?.Where(list => list != null)
                .SelectMany(list => list.PrefabList)
                .Where(entry => entry != null && entry.Prefab != null)
                .Select(entry => AssetDatabase.GetAssetPath(entry.Prefab))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();
        }

        private static int RemoveEditorOnlyComponents(GameObject root)
        {
            var removed = 0;
            removed += RemoveAll<EditorOnlyThirdPersonPreview>(root.GetComponentsInChildren<EditorOnlyThirdPersonPreview>(true));
            removed += RemoveAll<SpawnPointAutoGenerator>(root.GetComponentsInChildren<SpawnPointAutoGenerator>(true));
            return removed;
        }

        private static int RemoveEditorOnlyComponentsFromScene(Scene scene)
        {
            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                removed += RemoveEditorOnlyComponents(root);
            }

            return removed;
        }

        private static int RemoveAll<T>(T[] components) where T : Component
        {
            foreach (var component in components)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }

            return components.Length;
        }

        private static void RequireNoEditorOnlyComponents(GameObject root, string path)
        {
            if (root.GetComponentInChildren<EditorOnlyThirdPersonPreview>(true) != null
                || root.GetComponentInChildren<SpawnPointAutoGenerator>(true) != null)
            {
                throw new InvalidOperationException(
                    $"PHS_RUNTIME_EDITOR_ONLY_VALIDATE_FAILED path={path} reason=component_remaining");
            }
        }

        private static void RequireNoEditorOnlyComponents(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                RequireNoEditorOnlyComponents(root, path);
            }
        }
    }
}
#endif
