using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSMapVer3RedesignAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Map Ver3 Redesign")]
        public static void Author()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DisableLegacyVisuals(scene);
            AuthorGravityLayout(scene);
            AuthorSpawnLayout(scene);
            AuthorGameplayStations(scene);
            AuthorIncidentSources(scene);
            AuthorAccidentAnchors(scene);
            ConfigureMapProjection(scene);

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "initial_scene_save_failed");

            PHS0719IncidentLocationAuthoring.MigrateIncidentLocations();
            scene = SceneManager.GetSceneByPath(ScenePath);
            ConfigureAccidentReferences(scene);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "final_scene_save_failed");

            Debug.Log(
                "PHS_MAP_VER3_REDESIGN_AUTHOR_OK gravityZones=7 fireRooms=4 " +
                "accidentAnchors=12 legacyVisualsDisabled=true mapProjection=112x130");
        }

        private static void DisableLegacyVisuals(Scene scene)
        {
            Find(scene, "Cube").gameObject.SetActive(false);
            Find(scene, "PHS_ShipAccessRetrofit/PHS_EntryWing_A").gameObject.SetActive(false);
            Find(scene, "PHS_ShipAccessRetrofit/PHS_EntryWing_B").gameObject.SetActive(false);
            Find(scene, "PHS_ShipAccessRetrofit/PHS_ExteriorCollisionShell").gameObject.SetActive(true);
        }

        private static void AuthorGravityLayout(Scene scene)
        {
            ConfigureGravityArea(scene, "PHS_Exterior_ZeroGravityArea", new Vector3(2f, 12f, 55f), new Vector3(112f, 30f, 130f), 0, false);
            ConfigureGravityArea(scene, "PHS_Gravity_CommandRoom", new Vector3(0f, 3f, 7.5f), new Vector3(18f, 11f, 15f), 100, true);
            ConfigureGravityArea(scene, "PHS_Gravity_Bridge", new Vector3(0f, 3f, 22.5f), new Vector3(18f, 11f, 15f), 100, true);
            ConfigureGravityArea(scene, "PHS_Gravity_MainHall", new Vector3(0f, 3f, 55f), new Vector3(44f, 11f, 50f), 100, true);
            ConfigureGravityArea(scene, "PHS_Gravity_AftCorridor", new Vector3(0f, 3f, 95f), new Vector3(20f, 11f, 30f), 100, true);
            ConfigureGravityArea(scene, "PHS_Gravity_EntryWing A", new Vector3(-30f, 3f, 55f), new Vector3(14f, 11f, 26f), 100, true);
            ConfigureGravityArea(scene, "PHS_Gravity_EntryWing B", new Vector3(34f, 3f, 55f), new Vector3(14f, 11f, 36f), 100, true);

            var gravityRoot = Find(scene, "PHS_Map_Runtime/GravityZones");
            var serviceArea = FindDirectChild(gravityRoot, "PHS_ServiceGravityArea");
            if (serviceArea == null)
            {
                var source = FindDirectChild(gravityRoot, "PHS_Gravity_EntryWing A");
                serviceArea = UnityEngine.Object.Instantiate(source.gameObject, gravityRoot, false).transform;
                serviceArea.name = "PHS_ServiceGravityArea";
            }

            ConfigureGravityArea(serviceArea, new Vector3(-30f, 3f, 55f), new Vector3(12f, 11f, 18f), 1000, false);
        }

        private static void ConfigureGravityArea(
            Scene scene,
            string name,
            Vector3 position,
            Vector3 size,
            int priority,
            bool canToggle)
        {
            ConfigureGravityArea(
                Find(scene, $"PHS_Map_Runtime/GravityZones/{name}"),
                position,
                size,
                priority,
                canToggle);
        }

        private static void ConfigureGravityArea(
            Transform area,
            Vector3 position,
            Vector3 size,
            int priority,
            bool canToggle)
        {
            area.position = position;
            area.rotation = Quaternion.identity;
            var collider = area.GetComponent<BoxCollider>();
            Require(collider != null, $"gravity_collider_missing:{area.name}");
            collider.center = priority > 0 ? new Vector3(0f, -2.5f, 0f) : Vector3.zero;
            collider.size = size;
            collider.isTrigger = true;

            SetSerializedValue(area.GetComponent<NetworkPlayerGravityArea>(), "priority", priority);
            SetSerializedValue(area.GetComponent<NetworkPlayerGravityArea>(), "canToggleShipGravity", canToggle);
            SetSerializedValue(area.GetComponent<GravityZone>(), "priority", priority);
            SetSerializedValue(area.GetComponent<GravityZone>(), "canToggleShipGravity", canToggle);
        }

        private static void AuthorSpawnLayout(Scene scene)
        {
            var spawnRoot = Find(scene, "PHS_Map_Runtime/Spawn Points");
            var positions = new[]
            {
                new Vector3(-2f, 1f, 2f),
                new Vector3(0f, 1f, 2f),
                new Vector3(2f, 1f, 2f),
                new Vector3(-2f, 1f, 0f)
            };

            for (var index = 0; index < positions.Length; index++)
            {
                FindDirectChild(spawnRoot, $"Spawn Point {index + 1}").position = positions[index];
            }

            FindDirectChild(spawnRoot, "PHS_WarpSafeZone").position = new Vector3(0f, 0f, 7.5f);
        }

        private static void AuthorGameplayStations(Scene scene)
        {
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_BatteryVending", new Vector3(-3f, 0f, 69f));
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_FireExtinguisherVending", new Vector3(-6f, 0f, 69f));
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_UtilityBay/PHS_Utility_Oxygen", new Vector3(34f, 0f, 38f));
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_UtilityBay/PHS_GravityGenerator", new Vector3(-30f, 0f, 55f));
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_UtilityBay/PHS_Utility_BatteryStation", new Vector3(34f, 0.08f, 55f), Quaternion.Euler(0f, -90f, 0f));
            ConfigureBatteryFeedbackPoint(scene);
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_Portals/PHS_ExteriorShopPortal_0717", new Vector3(2f, 1.5f, 72f));
            Move(scene, "PHS_Map_Runtime/Interaction/PHS_TravelSystem_0715/PHS_TravelConsole_0715", new Vector3(-6f, 0f, 10f));
            Move(scene, "PHS_TeamIntegration/PHS_CannonTerminal", new Vector3(-4f, 0.1f, 16f));
            Move(scene, "PHS_TeamIntegration/PHS_WireTerminal", new Vector3(0f, 0.1f, 16f));
            Move(scene, "PHS_TeamIntegration/PHS_PowerTerminal", new Vector3(4f, 0.1f, 16f));
        }

        private static void ConfigureBatteryFeedbackPoint(Scene scene)
        {
            var station = Find(
                scene,
                "PHS_Map_Runtime/Interaction/PHS_UtilityBay/PHS_Utility_BatteryStation");
            var socket = station.GetComponent<BatteryInsertPowerStationSocket>();
            var socketState = new SerializedObject(socket);
            var installedVisual = socketState.FindProperty("installedBatteryVisual")
                .objectReferenceValue as GameObject;
            Require(installedVisual != null, "battery_installed_visual_missing");

            var feedbackPoint = FindDirectChild(station, "PHS_BatteryFeedbackPoint");
            if (feedbackPoint == null)
            {
                feedbackPoint = new GameObject("PHS_BatteryFeedbackPoint").transform;
                feedbackPoint.SetParent(station, false);
            }

            feedbackPoint.position = installedVisual.transform.position;
            feedbackPoint.rotation = installedVisual.transform.rotation;
            SetSerializedValue(socket, "feedbackPoint", feedbackPoint);
        }

        private static void AuthorIncidentSources(Scene scene)
        {
            Move(scene, "PHS_TeamIntegration/GameEventManager/Rooms/RoomA", new Vector3(-16f, 0f, 43f));
            Move(scene, "PHS_TeamIntegration/GameEventManager/Rooms/RoomB", new Vector3(17f, 0f, 43f));
            Move(scene, "PHS_TeamIntegration/GameEventManager/Rooms/RoomC", new Vector3(0f, 0f, 94f));
            Move(scene, "PHS_TeamIntegration/GameEventManager/Rooms/RoomD", new Vector3(0f, 0f, 48f));
        }

        private static void AuthorAccidentAnchors(Scene scene)
        {
            var runtime = Find(scene, "PHS_Map_Runtime/PHS_ShipRuntime");
            Move(scene, "PHS_Map_Runtime/PHS_ShipRuntime/FireAnchor", new Vector3(0f, 0.2f, 55f));
            Move(scene, "PHS_Map_Runtime/PHS_ShipRuntime/PowerAnchor", new Vector3(-30f, 1f, 45f));
            Move(scene, "PHS_Map_Runtime/PHS_ShipRuntime/DeviceAnchor", new Vector3(6f, 1f, 100f));
            Move(scene, "PHS_Map_Runtime/PHS_ShipRuntime/HullAnchor", new Vector3(-30f, 1f, 65f));
            Move(scene, "PHS_Map_Runtime/PHS_ShipRuntime/SteamAnchor", new Vector3(-6f, 1.1f, 94f));

            EnsureAnchorClone(runtime, "SteamAnchor", "SteamAnchor_B", "engine_pipe_b", new Vector3(6f, 1.1f, 102f));
            EnsureAnchorClone(runtime, "SteamAnchor", "SteamAnchor_LeftWing", "wing_pipe_left", new Vector3(-30f, 1.1f, 58f));
            EnsureAnchorClone(runtime, "DeviceAnchor", "DeviceAnchor_RightWing", "engine_device_right", new Vector3(34f, 1f, 48f));
            EnsureAnchorClone(runtime, "HullAnchor", "HullAnchor_RightWing", "hull_right_wing", new Vector3(34f, 1f, 68f));
            EnsureAnchorClone(runtime, "PowerAnchor", "PowerAnchor_RightWing", "power_right_wing", new Vector3(34f, 1f, 58f));
        }

        private static void EnsureAnchorClone(
            Transform parent,
            string sourceName,
            string cloneName,
            string anchorId,
            Vector3 position)
        {
            var clone = FindDirectChild(parent, cloneName);
            if (clone == null)
            {
                var source = FindDirectChild(parent, sourceName);
                clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false).transform;
                clone.name = cloneName;
                foreach (var location in clone.GetComponents<PHSIncidentLocationAnchor>())
                {
                    UnityEngine.Object.DestroyImmediate(location);
                }
            }

            clone.position = position;
            var anchor = clone.GetComponent<PHSShipAccidentAnchor>();
            SetSerializedValue(anchor, "anchorId", anchorId);
        }

        private static void ConfigureMapProjection(Scene scene)
        {
            var layout = Find(scene, "PHS_ShipMapWorldLayout").GetComponent<PHSShipMapWorldLayout>();
            SetSerializedValue(layout, "worldCenterXZ", new Vector2(2f, 55f));
            SetSerializedValue(layout, "worldSizeXZ", new Vector2(112f, 130f));
        }

        private static void ConfigureAccidentReferences(Scene scene)
        {
            var anchors = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSShipAccidentAnchor>(true))
                .OrderBy(anchor => anchor.AnchorId, StringComparer.Ordinal)
                .ToArray();
            Require(anchors.Length == 12, $"accident_anchor_count:{anchors.Length}");

            var coordinator = Find(scene, "PHS_Map_Runtime/PHS_ShipRuntime")
                .GetComponent<PHSNetworkShipAccidentCoordinator>();
            var mapLayout = Find(scene, "PHS_ShipMapWorldLayout")
                .GetComponent<PHSShipMapWorldLayout>();
            SetSerializedValue(coordinator, "anchors", anchors);
            SetSerializedValue(mapLayout, "accidentAnchors", anchors);
        }

        private static void Move(Scene scene, string path, Vector3 position, Quaternion? rotation = null)
        {
            var target = Find(scene, path);
            target.position = position;
            if (rotation.HasValue)
            {
                target.rotation = rotation.Value;
            }
        }

        private static Transform Find(Scene scene, string path)
        {
            var segments = path.Split('/');
            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == segments[0]);
            Require(root != null, $"root_missing:{segments[0]}");
            var current = root.transform;
            for (var index = 1; index < segments.Length; index++)
            {
                current = FindDirectChild(current, segments[index]);
                Require(current != null, $"path_missing:{path}:segment={segments[index]}");
            }

            return current;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetSerializedValue(UnityEngine.Object target, string propertyName, object value)
        {
            Require(target != null, $"serialized_target_missing:{propertyName}");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            Require(property != null, $"serialized_property_missing:{target.GetType().Name}:{propertyName}");
            switch (value)
            {
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                case int intValue:
                    property.intValue = intValue;
                    break;
                case string stringValue:
                    property.stringValue = stringValue;
                    break;
                case Vector2 vector2Value:
                    property.vector2Value = vector2Value;
                    break;
                case UnityEngine.Object[] objectArray:
                    property.arraySize = objectArray.Length;
                    for (var index = 0; index < objectArray.Length; index++)
                    {
                        property.GetArrayElementAtIndex(index).objectReferenceValue = objectArray[index];
                    }
                    break;
                case UnityEngine.Object objectValue:
                    property.objectReferenceValue = objectValue;
                    break;
                default:
                    throw new InvalidOperationException($"unsupported_serialized_value:{propertyName}:{value?.GetType().Name}");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"PHS_MAP_VER3_REDESIGN_AUTHOR_FAILED reason={reason}");
            }
        }
    }
}
