using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Guards the four authored start yaws and the existing travel-console
    /// root placement used by the start-camera setup.
    /// </summary>
    public static class PHSStartSpawnVisualAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string SpawnRootPath = "PHS_Map_Runtime/Spawn Points";
        private const string TravelConsolePath =
            "PHS_Map_Runtime/Interaction/PHS_TravelSystem_0715/PHS_TravelConsole_0715";
        private static readonly Vector3 TravelConsolePosition = new(-8.3f, -3.74f, 10f);
        private const float ExpectedSpawnYaw = 0f;

        [MenuItem("Tools/ParkHanSol/Validate Start Spawn Visual Precondition")]
        public static void ValidateOrThrow()
        {
            var scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var spawnRoot = Find(scene, SpawnRootPath);
            var console = Find(scene, TravelConsolePath);

            for (var index = 1; index <= 4; index++)
            {
                var spawn = spawnRoot.Find($"Spawn Point {index}");
                Require(spawn != null, $"spawn_missing index={index}");
                Require(Mathf.Abs(Mathf.DeltaAngle(spawn.eulerAngles.y, ExpectedSpawnYaw)) < 0.1f,
                    $"spawn_yaw_changed index={index} yaw={spawn.eulerAngles.y:0.##}");

            }

            Require(Vector3.Distance(console.position, TravelConsolePosition) < 0.01f,
                $"travel_console_root_changed position={console.position}");

            Debug.Log($"PHS_START_SPAWN_VISUAL_VALIDATE_OK console={console.position} spawns=4");
        }

        private static Transform Find(Scene scene, string path)
        {
            var current = scene.GetRootGameObjects().SingleOrDefault(root => root.name == path.Split('/')[0])?.transform;
            foreach (var segment in path.Split('/').Skip(1))
            {
                current = current?.Find(segment);
            }

            Require(current != null, $"transform_missing path={path}");
            return current;
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"PHS_START_SPAWN_VISUAL_FAILED reason={reason}");
            }
        }
    }
}
