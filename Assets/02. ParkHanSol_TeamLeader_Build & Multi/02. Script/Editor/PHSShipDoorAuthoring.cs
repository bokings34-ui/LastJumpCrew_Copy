#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Doors;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHSShipDoorAuthoring
    {
        private const string RootName = "PHS_NetworkShipDoors";

        [MenuItem("Tools/ParkHanSol/Doors/Author Main Map Doors")]
        public static void AuthorMainMapDoors()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded
                || !scene.path.EndsWith("/BEAVER_2026/PHS_Map_ver1.unity",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_DOOR_AUTHORING_FAILED reason=wrong_scene scene={scene.path}");
            }

            var legacyDoors = UnityEngine.Object
                .FindObjectsByType<DoorDoubleSlide>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(door => door.gameObject.scene == scene
                    && door.doorL != null && door.doorR != null)
                .OrderBy(door => GetHierarchyPath(door.transform),
                    StringComparer.Ordinal)
                .ToArray();
            if (legacyDoors.Length != 20)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_DOOR_AUTHORING_FAILED reason=door_count expected=20 actual={legacyDoors.Length}");
            }

            var oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            var root = new GameObject(RootName,
                typeof(NetworkObject), typeof(PHSNetworkShipDoorCoordinator));
            SceneManager.MoveGameObjectToScene(root, scene);
            var coordinator = root.GetComponent<PHSNetworkShipDoorCoordinator>();
            var bindings = new List<PHSNetworkShipDoorCoordinator.DoorBinding>(
                legacyDoors.Length);

            for (var i = 0; i < legacyDoors.Length; i++)
            {
                bindings.Add(CreateDoorBinding(root.transform,
                    coordinator, legacyDoors[i], i));
            }

            coordinator.EditorConfigure(bindings.ToArray());
            EditorUtility.SetDirty(coordinator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateMainMapDoors();
            Debug.Log($"PHS_SHIP_DOOR_AUTHORING_PASSED count={bindings.Count} scene={scene.path}", root);
        }

        [MenuItem("Tools/ParkHanSol/Doors/Validate Main Map Doors")]
        public static void ValidateMainMapDoors()
        {
            var coordinator = UnityEngine.Object
                .FindFirstObjectByType<PHSNetworkShipDoorCoordinator>(
                    FindObjectsInactive.Include);
            var targets = UnityEngine.Object.FindObjectsByType<PHSShipDoorTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var buttons = UnityEngine.Object
                .FindObjectsByType<PHSShipDoorLockButton>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            var errors = new List<string>();

            if (coordinator == null || coordinator.DoorCount != 20)
            {
                errors.Add($"coordinator_or_count actual={coordinator?.DoorCount ?? 0}");
            }
            if (targets.Length != 20)
            {
                errors.Add($"target_count actual={targets.Length}");
            }
            if (buttons.Length != 20)
            {
                errors.Add($"button_count actual={buttons.Length}");
            }
            foreach (var target in targets)
            {
                var colliders = target.GetComponentsInChildren<Collider>(true);
                if (!colliders.Any(collider => collider.isTrigger)
                    || !colliders.Any(collider => !collider.isTrigger))
                {
                    errors.Add($"target_colliders target={target.name}");
                }
                if (target.GetComponentInChildren<NavMeshObstacle>(true) == null)
                {
                    errors.Add($"navmesh_obstacle target={target.name}");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_DOOR_VALIDATION_FAILED " + string.Join(" | ", errors));
            }

            Debug.Log($"PHS_SHIP_DOOR_VALIDATION_PASSED count={targets.Length}", coordinator);
        }

        private static PHSNetworkShipDoorCoordinator.DoorBinding CreateDoorBinding(
            Transform parent,
            PHSNetworkShipDoorCoordinator coordinator,
            DoorDoubleSlide door,
            int index)
        {
            door.enabled = false;
            EditorUtility.SetDirty(door);

            var bounds = GetDoorBounds(door);
            var gameplayRoot = new GameObject($"Door_{index:00}_{door.name}",
                typeof(BoxCollider), typeof(PHSShipDoorTarget));
            gameplayRoot.transform.SetParent(parent, false);
            gameplayRoot.transform.position = bounds.center;
            var repairCollider = gameplayRoot.GetComponent<BoxCollider>();
            repairCollider.isTrigger = true;
            repairCollider.size = ClampDoorSize(bounds.size);
            var target = gameplayRoot.GetComponent<PHSShipDoorTarget>();

            var blockerObject = new GameObject("SolidBlocker",
                typeof(BoxCollider), typeof(NavMeshObstacle));
            blockerObject.transform.SetParent(gameplayRoot.transform, false);
            var blocker = blockerObject.GetComponent<BoxCollider>();
            blocker.size = repairCollider.size;
            var obstacle = blockerObject.GetComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = blocker.center;
            obstacle.size = blocker.size;
            obstacle.carving = true;

            var sensorObject = new GameObject("PresenceSensor",
                typeof(BoxCollider));
            sensorObject.transform.SetParent(gameplayRoot.transform, false);
            var sensor = sensorObject.GetComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.size = new Vector3(
                Mathf.Max(repairCollider.size.x + 4f, 5f),
                Mathf.Max(repairCollider.size.y, 2.5f),
                Mathf.Max(repairCollider.size.z + 4f, 5f));

            var buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "LockButton";
            buttonObject.transform.SetParent(gameplayRoot.transform, false);
            buttonObject.transform.localScale = new Vector3(0.35f, 0.45f, 0.15f);
            buttonObject.transform.position = bounds.center
                + door.transform.right
                * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.45f);
            var button = buttonObject.AddComponent<PHSShipDoorLockButton>();

            target.Initialize(coordinator, index);
            button.Initialize(coordinator, index);
            return new PHSNetworkShipDoorCoordinator.DoorBinding
            {
                LegacyDoor = door,
                LeftLeaf = door.doorL,
                RightLeaf = door.doorR,
                PresenceSensor = sensor,
                SolidBlocker = blocker,
                NavMeshBlocker = obstacle,
                Target = target,
                Button = button,
                LeftClosedLocalPosition = door.doorL.localPosition,
                RightClosedLocalPosition = door.doorR.localPosition,
                OpenDirection = door.directionType switch
                {
                    DoorDoubleSlide.Direction.X => Vector3.right,
                    DoorDoubleSlide.Direction.Y => Vector3.up,
                    DoorDoubleSlide.Direction.Z => Vector3.back,
                    _ => throw new ArgumentOutOfRangeException()
                },
                OpenDistance = door.openDistance
            };
        }

        private static Bounds GetDoorBounds(DoorDoubleSlide door)
        {
            var renderers = door.doorL.GetComponentsInChildren<Renderer>(true)
                .Concat(door.doorR.GetComponentsInChildren<Renderer>(true))
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_DOOR_AUTHORING_FAILED reason=renderer_missing door={door.name}");
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static Vector3 ClampDoorSize(Vector3 source)
        {
            return new Vector3(
                Mathf.Max(source.x, 0.35f),
                Mathf.Max(source.y, 2f),
                Mathf.Max(source.z, 0.35f));
        }

        private static string GetHierarchyPath(Transform current)
        {
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
