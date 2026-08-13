using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using SM;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Guards the relocated legacy event-spawn hierarchy without baking or
    /// relocating it. It keeps the source radius-based neighbor rule intact;
    /// Enemy and oxygen events require every point to be on the existing NavMesh.
    /// </summary>
    public static class PHSMapSpawnNavigationValidator
    {
        private const string MainMapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string LegacyNavigationName = "PHS_0715_Navigation";
        private const int ExpectedPointCount = 81;
        private const int ExpectedIgnoreVolumeCount = 10;

        [MenuItem("Tools/ParkHanSol/Validate Map Spawn And Navigation")]
        public static void ValidateOrThrow()
        {
            var activeScene = OpenMainMapSceneIfNeeded();
            var configs = FindSceneComponents<ShipSpawnPointConfig>(activeScene);
            Require(configs.Length == 1, $"spawn_config_count={configs.Length}");
            var config = configs[0];
            var spawnRoot = config.transform.root;
            Require(spawnRoot != null, "spawn_root_missing");
            var serializedConfig = new SerializedObject(config);
            var configuredPoints = serializedConfig.FindProperty("spawnPoints");
            Require(configuredPoints != null && configuredPoints.arraySize == ExpectedPointCount, "configured_spawn_point_count");
            Require(serializedConfig.FindProperty("autoConnectOnAwake").boolValue, "spawn_auto_connect_disabled");
            Require(serializedConfig.FindProperty("neighborConnectionRadius").floatValue > 0f, "spawn_neighbor_radius_invalid");

            var points = spawnRoot.GetComponentsInChildren<ShipSpawnPoint>(true);
            Require(points.Length == ExpectedPointCount, $"spawn_point_count={points.Length}");
            Require(points.All(point => point != null), "spawn_point_null");
            Require(points.All(point => point.transform.IsChildOf(spawnRoot)), "spawn_point_outside_root");

            var volumes = spawnRoot.GetComponentsInChildren<NavMeshModifierVolume>(true);
            Require(volumes.Length == ExpectedIgnoreVolumeCount, $"ignore_volume_count={volumes.Length}");
            Require(volumes.All(volume => volume.area == 1), "ignore_volume_area_not_walkable");

            var surfaces = FindSceneComponents<NavMeshSurface>(activeScene);
            Require(surfaces.Length == 1, $"navmesh_surface_count={surfaces.Length}");
            Require(FindSceneTransforms(activeScene, LegacyNavigationName).Length == 0, "legacy_navigation_root_present");

            var contexts = FindSceneComponents<GameplaySceneContext>(activeScene);
            Require(contexts.Length == 1, $"gameplay_context_count={contexts.Length}");
            var playerSpawnRoot = contexts[0].transform.Find("Spawn Points");
            Require(playerSpawnRoot != null && playerSpawnRoot.childCount > 0, "player_spawn_root_missing");
            var serializedContext = new SerializedObject(contexts[0]);
            Require(serializedContext.FindProperty("spawnPointsRoot").objectReferenceValue == playerSpawnRoot,
                "gameplay_spawn_root_unbound");

            ValidateNeighborReferences(points);
            ValidateNavMeshPlacement(points);

            Debug.Log($"PHS_MAP_SPAWN_NAV_VALIDATE_OK points={points.Length} volumes={volumes.Length} surface={surfaces[0].name}");
        }

        [MenuItem("Tools/ParkHanSol/Repair Map Spawn Neighbors")]
        public static void RepairNeighbors()
        {
            var activeScene = OpenMainMapSceneIfNeeded();
            var configs = FindSceneComponents<ShipSpawnPointConfig>(activeScene);
            Require(configs.Length == 1, $"spawn_config_count={configs.Length}");

            var points = configs[0].transform.root.GetComponentsInChildren<ShipSpawnPoint>(true);
            Require(points.Length == ExpectedPointCount, $"spawn_point_count={points.Length}");

            Undo.RecordObjects(points, "Reconnect relocated event spawn points");
            configs[0].AutoConnectNeighbors();
            foreach (var point in points)
            {
                EditorUtility.SetDirty(point);
            }

            var contexts = FindSceneComponents<GameplaySceneContext>(activeScene);
            Require(contexts.Length == 1, $"gameplay_context_count={contexts.Length}");
            var playerSpawnRoot = contexts[0].transform.Find("Spawn Points");
            Require(playerSpawnRoot != null && playerSpawnRoot.childCount > 0, "player_spawn_root_missing");
            var serializedContext = new SerializedObject(contexts[0]);
            serializedContext.FindProperty("spawnPointsRoot").objectReferenceValue = playerSpawnRoot;
            serializedContext.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"PHS_MAP_SPAWN_NEIGHBORS_REPAIRED points={points.Length} playerSpawnRoot={playerSpawnRoot.name}");
        }

        private static void ValidateNeighborReferences(IReadOnlyList<ShipSpawnPoint> points)
        {
            var allowed = new HashSet<ShipSpawnPoint>(points);
            Require(points.All(point => point.Neighbors.All(allowed.Contains)), "spawn_neighbor_outside_config");
            Require(points.All(point => point.Neighbors.All(neighbor => neighbor.Neighbors.Contains(point))), "spawn_neighbor_not_bidirectional");
        }

        private static void ValidateNavMeshPlacement(IEnumerable<ShipSpawnPoint> points)
        {
            foreach (var point in points)
            {
                if (!NavMesh.SamplePosition(point.transform.position, out var hit, 0.5f, NavMesh.AllAreas)
                    || Vector3.Distance(point.transform.position, hit.position) > 0.5f)
                {
                    throw new InvalidOperationException($"PHS_MAP_SPAWN_NAV_VALIDATE_FAILED navmesh_miss={point.name}");
                }
            }
        }

        private static Transform[] FindSceneTransforms(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .ToArray();
        }

        private static Scene OpenMainMapSceneIfNeeded()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == MainMapScenePath)
            {
                return activeScene;
            }

            return EditorSceneManager.OpenScene(MainMapScenePath, OpenSceneMode.Single);
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(component => component.gameObject.scene == scene)
                .ToArray();
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"PHS_MAP_SPAWN_NAV_VALIDATE_FAILED {reason}");
            }
        }
    }
}
