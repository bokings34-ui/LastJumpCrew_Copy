#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSGameplaySpawnCapacityAuthoring
    {
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/" +
            "BEAVER_2026/PHS_Map_ver1.unity";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/" +
            "BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/" +
            "BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string SpawnRootName = "Spawn Points";
        private const string SafeZoneName = "PHS_WarpSafeZone";
        private const int MultiplayerSpawnCount = 8;
        private const float MinimumSpawnSeparation = 0.75f;

        private static readonly Vector3[] MapAdditionalLocalPositions =
        {
            new(-393.8f, -3.69f, -7.5f),
            new(-392.2f, -3.69f, -7.5f),
            new(-393.8f, -3.69f, -4.5f),
            new(-392.2f, -3.69f, -4.5f)
        };

        private static readonly Vector3[] ShopAdditionalLocalPositions =
        {
            new(-3f, 1f, -2f),
            new(0f, 1f, -2f),
            new(3f, 1f, -2f),
            new(6f, 1f, -2f)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Eight Player Gameplay Spawns")]
        public static void Author()
        {
            RequireNoDirtyLoadedScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AuthorMultiplayerScene(
                    MapScenePath,
                    MapAdditionalLocalPositions,
                    separateSafeZone: true);
                AuthorMultiplayerScene(
                    ShopScenePath,
                    ShopAdditionalLocalPositions,
                    separateSafeZone: false);
                ValidateScene(TutorialScenePath, 1, requireSeparatedSafeZone: false);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "PHS_GAMEPLAY_SPAWN_AUTHOR_OK map=8 shop=8 tutorial=1 " +
                "safe_zone=separated geometry=unchanged");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Eight Player Gameplay Spawns")]
        public static void Validate()
        {
            RequireNoDirtyLoadedScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ValidateScene(
                    MapScenePath,
                    MultiplayerSpawnCount,
                    requireSeparatedSafeZone: true);
                ValidateScene(
                    ShopScenePath,
                    MultiplayerSpawnCount,
                    requireSeparatedSafeZone: false);
                ValidateScene(
                    TutorialScenePath,
                    1,
                    requireSeparatedSafeZone: false);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            Debug.Log(
                "PHS_GAMEPLAY_SPAWN_VALIDATE_OK map=8 shop=8 tutorial=1 " +
                "unique=true safe_zone=separated");
        }

        private static void AuthorMultiplayerScene(
            string scenePath,
            IReadOnlyList<Vector3> additionalLocalPositions,
            bool separateSafeZone)
        {
            if (additionalLocalPositions == null
                || additionalLocalPositions.Count != MultiplayerSpawnCount - 4)
            {
                throw new InvalidOperationException(
                    "PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=additional_layout_invalid");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var context = RequireSingleContext(scene);
            var spawnRoot = RequireSpawnRoot(context);

            if (separateSafeZone)
            {
                SeparateSafeZone(scene, context, spawnRoot);
            }

            var initialSpawns = GetDirectSpawnChildren(spawnRoot);
            if (initialSpawns.Length != 4 && initialSpawns.Length != MultiplayerSpawnCount)
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=initial_spawn_count_invalid " +
                    $"scene={scene.name} actual={initialSpawns.Length}");
            }

            for (var index = 0; index < 4; index++)
            {
                RequireDirectChild(spawnRoot, $"Spawn Point {index + 1}");
            }

            for (var index = 4; index < MultiplayerSpawnCount; index++)
            {
                var pointName = $"Spawn Point {index + 1}";
                var point = FindDirectChild(spawnRoot, pointName);
                if (point == null)
                {
                    point = new GameObject(pointName).transform;
                    Undo.RegisterCreatedObjectUndo(
                        point.gameObject,
                        $"Create {scene.name} {pointName}");
                    point.SetParent(spawnRoot, false);
                }

                Undo.RecordObject(point, $"Configure {scene.name} {pointName}");
                point.localPosition = additionalLocalPositions[index - 4];
                point.localRotation = Quaternion.identity;
                point.localScale = Vector3.one;
                point.SetSiblingIndex(index);
                EditorUtility.SetDirty(point);
            }

            ValidateLoadedScene(
                scene,
                MultiplayerSpawnCount,
                requireSeparatedSafeZone: separateSafeZone);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=scene_save_failed scene={scene.name}");
            }
        }

        private static void SeparateSafeZone(
            Scene scene,
            GameplaySceneContext context,
            Transform spawnRoot)
        {
            var safeZones = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == SafeZoneName)
                .ToArray();
            if (safeZones.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=safe_zone_count_invalid " +
                    $"scene={scene.name} actual={safeZones.Length}");
            }

            var safeZone = safeZones[0];
            if (safeZone.parent == spawnRoot)
            {
                Undo.SetTransformParent(
                    safeZone,
                    context.transform,
                    "Separate warp safe zone from player spawn candidates");
                EditorUtility.SetDirty(safeZone);
            }
        }

        private static void ValidateScene(
            string scenePath,
            int expectedSpawnCount,
            bool requireSeparatedSafeZone)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ValidateLoadedScene(scene, expectedSpawnCount, requireSeparatedSafeZone);
        }

        private static void ValidateLoadedScene(
            Scene scene,
            int expectedSpawnCount,
            bool requireSeparatedSafeZone)
        {
            var context = RequireSingleContext(scene);
            var spawnRoot = RequireSpawnRoot(context);
            var spawnPoints = GetDirectSpawnChildren(spawnRoot);
            if (spawnRoot.childCount != expectedSpawnCount
                || spawnPoints.Length != expectedSpawnCount)
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=spawn_count_invalid " +
                    $"scene={scene.name} root_children={spawnRoot.childCount} " +
                    $"spawn_points={spawnPoints.Length} expected={expectedSpawnCount}");
            }

            var selected = new HashSet<Transform>();
            for (var index = 0; index < expectedSpawnCount; index++)
            {
                if (!context.TryGetSpawnPoint((ulong)index, out var point)
                    || point == null
                    || point.parent != spawnRoot
                    || !selected.Add(point))
                {
                    throw new InvalidOperationException(
                        $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=runtime_selection_invalid " +
                        $"scene={scene.name} client={index}");
                }
            }

            for (var first = 0; first < spawnPoints.Length; first++)
            {
                for (var second = first + 1; second < spawnPoints.Length; second++)
                {
                    if (Vector3.Distance(
                            spawnPoints[first].position,
                            spawnPoints[second].position)
                        < MinimumSpawnSeparation)
                    {
                        throw new InvalidOperationException(
                            $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=spawn_overlap " +
                            $"scene={scene.name} a={spawnPoints[first].name} " +
                            $"b={spawnPoints[second].name}");
                    }
                }
            }

            if (expectedSpawnCount == MultiplayerSpawnCount)
            {
                for (var index = 0; index < MultiplayerSpawnCount; index++)
                {
                    if (spawnRoot.GetChild(index).name != $"Spawn Point {index + 1}")
                    {
                        throw new InvalidOperationException(
                            $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=sibling_order_invalid " +
                            $"scene={scene.name} index={index}");
                    }
                }
            }

            if (requireSeparatedSafeZone)
            {
                var safeZone = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .SingleOrDefault(candidate => candidate.name == SafeZoneName);
                if (safeZone == null || safeZone.IsChildOf(spawnRoot))
                {
                    throw new InvalidOperationException(
                        $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=safe_zone_not_separated " +
                        $"scene={scene.name}");
                }
            }
        }

        private static GameplaySceneContext RequireSingleContext(Scene scene)
        {
            var contexts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameplaySceneContext>(true))
                .ToArray();
            if (contexts.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=context_count_invalid " +
                    $"scene={scene.name} actual={contexts.Length}");
            }

            return contexts[0];
        }

        private static Transform RequireSpawnRoot(GameplaySceneContext context)
        {
            var spawnRoot = context.transform.Find(SpawnRootName);
            var configuredRoot = new SerializedObject(context)
                .FindProperty("spawnPointsRoot")?.objectReferenceValue as Transform;
            if (spawnRoot == null || configuredRoot != spawnRoot)
            {
                throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_VALIDATE_FAILED reason=spawn_root_reference_invalid " +
                    $"scene={context.gameObject.scene.name}");
            }

            return spawnRoot;
        }

        private static Transform[] GetDirectSpawnChildren(Transform spawnRoot)
        {
            return Enumerable.Range(0, spawnRoot.childCount)
                .Select(spawnRoot.GetChild)
                .Where(child => child.name.StartsWith("Spawn Point ", StringComparison.Ordinal)
                    || child.name == "Spawn_01")
                .ToArray();
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            return FindDirectChild(parent, name)
                ?? throw new InvalidOperationException(
                    $"PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=existing_spawn_missing " +
                    $"scene={parent.gameObject.scene.name} point={name}");
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void RequireNoDirtyLoadedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded
                    && scene.isDirty
                    && !EditorSceneManager.IsPreviewScene(scene)
                    && scene.name != "DontDestroyOnLoad")
                {
                    throw new InvalidOperationException(
                        $"PHS_GAMEPLAY_SPAWN_AUTHOR_FAILED reason=loaded_scene_dirty " +
                        $"scene={scene.name}");
                }
            }
        }
    }
}
#endif
