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
        private const string ButtonPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/PHS_DoorLockButton.prefab";

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

            var buttonPrefab = EnsureButtonPrefab();

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
                    coordinator, buttonPrefab, legacyDoors[i], i));
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
            if (buttons.Length != 40)
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
                var targetButtons = target.GetComponentsInChildren<
                    PHSShipDoorLockButton>(true);
                if (targetButtons.Length != 2)
                {
                    errors.Add($"target_button_count target={target.name} actual={targetButtons.Length}");
                }
                foreach (var button in targetButtons)
                {
                    var source = PrefabUtility
                        .GetCorrespondingObjectFromSource(button.gameObject);
                    if (source == null || AssetDatabase.GetAssetPath(source)
                        != ButtonPrefabPath)
                    {
                        errors.Add($"button_prefab target={target.name} button={button.name}");
                    }
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
            GameObject buttonPrefab,
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

            var buttons = CreateButtons(gameplayRoot.transform, coordinator,
                buttonPrefab, door, index);

            target.Initialize(coordinator, index);
            return new PHSNetworkShipDoorCoordinator.DoorBinding
            {
                LegacyDoor = door,
                LeftLeaf = door.doorL,
                RightLeaf = door.doorR,
                PresenceSensor = sensor,
                SolidBlocker = blocker,
                NavMeshBlocker = obstacle,
                Target = target,
                Buttons = buttons,
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

        private static PHSShipDoorLockButton[] CreateButtons(
            Transform parent,
            PHSNetworkShipDoorCoordinator coordinator,
            GameObject buttonPrefab,
            DoorDoubleSlide door,
            int doorIndex)
        {
            var localBounds = GetDoorLocalBounds(door);
            var normalLocal = localBounds.size.x <= localBounds.size.z
                ? Vector3.right
                : Vector3.forward;
            var tangentLocal = normalLocal == Vector3.right
                ? Vector3.forward
                : Vector3.right;
            var normalExtent = normalLocal == Vector3.right
                ? localBounds.extents.x
                : localBounds.extents.z;
            var tangentExtent = tangentLocal == Vector3.right
                ? localBounds.extents.x
                : localBounds.extents.z;
            var localPosition = localBounds.center;
            localPosition.y = localBounds.min.y
                + Mathf.Min(1.2f, localBounds.size.y * 0.55f);
            localPosition += tangentLocal * (tangentExtent + 0.22f);

            var result = new PHSShipDoorLockButton[2];
            for (var sideIndex = 0; sideIndex < result.Length; sideIndex++)
            {
                var sideSign = sideIndex == 0 ? 1f : -1f;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    buttonPrefab, parent);
                instance.name = sideIndex == 0
                    ? "LockButton_SideA"
                    : "LockButton_SideB";
                instance.transform.position = door.transform.TransformPoint(
                    localPosition + normalLocal
                    * sideSign * (normalExtent + 0.08f));
                var outward = door.transform.TransformDirection(
                    normalLocal * sideSign);
                instance.transform.rotation = Quaternion.LookRotation(
                    outward, Vector3.up);
                result[sideIndex] = instance
                    .GetComponent<PHSShipDoorLockButton>();
                result[sideIndex].Initialize(coordinator, doorIndex, sideIndex);
            }

            return result;
        }

        private static GameObject EnsureButtonPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                ButtonPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("PHS_DoorLockButton",
                typeof(BoxCollider), typeof(PHSShipDoorLockButton));
            var collider = root.GetComponent<BoxCollider>();
            collider.size = new Vector3(0.34f, 0.46f, 0.14f);

            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            housing.transform.SetParent(root.transform, false);
            housing.transform.localScale = new Vector3(0.34f, 0.46f, 0.12f);
            UnityEngine.Object.DestroyImmediate(housing.GetComponent<Collider>());

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "StateIndicator";
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0f, 0.075f);
            indicator.transform.localScale = new Vector3(0.22f, 0.22f, 0.03f);
            UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());
            root.GetComponent<PHSShipDoorLockButton>()
                .EditorConfigureRenderer(indicator.GetComponent<Renderer>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root,
                ButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_DOOR_AUTHORING_FAILED reason=button_prefab_save");
            }
            return prefab;
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

        private static Bounds GetDoorLocalBounds(DoorDoubleSlide door)
        {
            var renderers = door.doorL.GetComponentsInChildren<Renderer>(true)
                .Concat(door.doorR.GetComponentsInChildren<Renderer>(true))
                .ToArray();
            var initialized = false;
            var localBounds = new Bounds();
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var worldCorner = bounds.center + Vector3.Scale(
                                bounds.extents, new Vector3(x, y, z));
                            var localCorner = door.transform.InverseTransformPoint(
                                worldCorner);
                            if (!initialized)
                            {
                                localBounds = new Bounds(localCorner, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }
            return localBounds;
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
