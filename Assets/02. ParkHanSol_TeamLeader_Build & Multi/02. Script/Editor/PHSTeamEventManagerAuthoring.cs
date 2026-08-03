using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using SM;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSTeamEventManagerAuthoring
    {
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string TeamIntegrationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_EventRuntimeSystem.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Team Integration/Author Network Event Manager")]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_TEAM_EVENT_MANAGER_AUTHOR_FAILED reason=play_mode_active");
            }

            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var eventManagers = FindSceneComponents<EventManager>(scene);
            if (eventManagers.Length == 0)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TeamIntegrationPrefabPath);
                Require(prefab != null,
                    $"team_prefab_missing path={TeamIntegrationPrefabPath}");

                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                Require(instance != null, "team_prefab_instantiate_failed");
                instance.name = "PHS_TeamIntegration";
                instance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                instance.transform.localScale = Vector3.one;
            }
            else
            {
                Require(eventManagers.Length == 1,
                    $"event_manager_count:{eventManagers.Length}");
            }

            WireNetworkRuntime(scene);
            ValidateSceneOrThrow(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "map_save_failed");
            AssetDatabase.SaveAssets();

            Debug.Log(
                "PHS_TEAM_EVENT_MANAGER_AUTHOR_OK "
                + "networked=true managers=1 rooms=4 scene=PHS_Map_ver1");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Team Integration/Validate Network Event Manager")]
        public static void ValidateFromMenu()
        {
            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            ValidateSceneOrThrow(scene);
        }

        public static void ValidateSceneOrThrow(Scene scene)
        {
            var eventManager = RequireOne<EventManager>(scene, "event_manager");
            var scheduler = RequireOne<PHSNetworkEventScheduler>(scene, "event_scheduler");
            var roomRegistry = RequireOne<RoomRegistry>(scene, "room_registry");
            var coordinator = RequireOne<NetworkEventCoordinator>(scene, "event_coordinator");
            var presenter = RequireOne<NetworkEventEffectMirrorPresenter>(scene, "effect_presenter");
            var runtime = RequireOne<PHSMapRuntimeContext>(scene, "map_runtime");
            var consumer = RequireOne<PHSMapIncidentCommandConsumer>(scene, "incident_consumer");
            var rooms = FindSceneComponents<ShipRoom>(scene)
                .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                .ToArray();

            Require(rooms.Length == 4, $"room_count:{rooms.Length}");
            Require(coordinator.GetComponent<NetworkObject>() != null,
                "coordinator_network_object_missing");
            Require(presenter.ValidateConfiguration(),
                "effect_presenter_invalid");

            RequireReference(eventManager, "registry");
            RequireReference(scheduler, "coordinator", coordinator);
            RequireReference(coordinator, "eventManager", eventManager);
            RequireReference(coordinator, "eventScheduler", scheduler);
            RequireReference(coordinator, "roomRegistry", roomRegistry);
            RequireReference(coordinator, "effectMirrorPresenter", presenter);
            RequireReference(runtime, "externalThreatScheduler", scheduler);
            RequireReference(runtime, "incidentCommandConsumer", consumer);
            RequireReference(consumer, "eventCoordinator", coordinator);

            var consumerData = new SerializedObject(consumer);
            var roomProperty = consumerData.FindProperty("rooms");
            Require(roomProperty != null && roomProperty.isArray,
                "consumer_rooms_property_missing");
            Require(roomProperty.arraySize == rooms.Length,
                $"consumer_room_count:{roomProperty.arraySize}");
            for (var index = 0; index < rooms.Length; index++)
            {
                Require(
                    roomProperty.GetArrayElementAtIndex(index).objectReferenceValue
                        == rooms[index],
                    $"consumer_room_mismatch:{index}");
            }

            var missingScripts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject));
            Require(missingScripts == 0,
                $"scene_missing_scripts:{missingScripts}");

            Debug.Log(
                "PHS_TEAM_EVENT_MANAGER_VALIDATION_OK "
                + "networked=true managers=1 schedulers=1 coordinators=1 rooms=4 "
                + "missingScripts=0");
        }

        private static void WireNetworkRuntime(Scene scene)
        {
            var eventManager = RequireOne<EventManager>(scene, "event_manager");
            var scheduler = RequireOne<PHSNetworkEventScheduler>(scene, "event_scheduler");
            var roomRegistry = RequireOne<RoomRegistry>(scene, "room_registry");
            var coordinator = RequireOne<NetworkEventCoordinator>(scene, "event_coordinator");
            var presenter = RequireOne<NetworkEventEffectMirrorPresenter>(scene, "effect_presenter");
            var runtime = RequireOne<PHSMapRuntimeContext>(scene, "map_runtime");
            var consumer = RequireOne<PHSMapIncidentCommandConsumer>(scene, "incident_consumer");
            var rooms = FindSceneComponents<ShipRoom>(scene)
                .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                .ToArray();

            Require(rooms.Length == 4, $"room_count:{rooms.Length}");
            Require(coordinator.GetComponent<NetworkObject>() != null,
                "coordinator_network_object_missing");

            SetReference(scheduler, "coordinator", coordinator);
            SetReference(coordinator, "eventManager", eventManager);
            SetReference(coordinator, "eventScheduler", scheduler);
            SetReference(coordinator, "roomRegistry", roomRegistry);
            SetReference(coordinator, "effectMirrorPresenter", presenter);
            SetReference(runtime, "externalThreatScheduler", scheduler);
            SetReference(runtime, "incidentCommandConsumer", consumer);
            SetReference(consumer, "eventCoordinator", coordinator);

            var consumerData = new SerializedObject(consumer);
            var roomProperty = consumerData.FindProperty("rooms");
            Require(roomProperty != null && roomProperty.isArray,
                "consumer_rooms_property_missing");
            roomProperty.arraySize = rooms.Length;
            for (var index = 0; index < rooms.Length; index++)
            {
                roomProperty.GetArrayElementAtIndex(index).objectReferenceValue
                    = rooms[index];
            }
            consumerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(consumer);
        }

        private static T RequireOne<T>(Scene scene, string label)
            where T : Component
        {
            var values = FindSceneComponents<T>(scene);
            Require(values.Length == 1, $"{label}_count:{values.Length}");
            return values[0];
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void SetReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value)
        {
            var data = new SerializedObject(owner);
            var property = data.FindProperty(propertyName);
            Require(property != null,
                $"property_missing owner={owner.GetType().Name} property={propertyName}");
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void RequireReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object expected = null)
        {
            var property = new SerializedObject(owner).FindProperty(propertyName);
            Require(property != null,
                $"property_missing owner={owner.GetType().Name} property={propertyName}");
            Require(property.objectReferenceValue != null,
                $"reference_missing owner={owner.GetType().Name} property={propertyName}");
            if (expected != null)
            {
                Require(property.objectReferenceValue == expected,
                    $"reference_mismatch owner={owner.GetType().Name} property={propertyName}");
            }
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_TEAM_EVENT_MANAGER_FAILED reason={reason}");
            }
        }
    }
}
