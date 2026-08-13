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
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";

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
            var rooms = FindSceneComponents<ShipRoom>(scene)
                .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                .ToArray();
            var enemyDeviceTargets = FindSceneComponents<EnemyDeviceTarget>(scene);

            Require(rooms.Length == 4, $"room_count:{rooms.Length}");
            Require(enemyDeviceTargets.Length > 0,
                "enemy_device_target_count:0");
            foreach (var enemyDeviceTarget in enemyDeviceTargets)
            {
                Require(enemyDeviceTarget.GetComponent<NetworkObject>() != null,
                    $"enemy_device_target_network_object_missing:{enemyDeviceTarget.name}");
                var targetData = new SerializedObject(enemyDeviceTarget);
                Require(targetData.FindProperty("maximumHealth")?.intValue > 0,
                    $"enemy_device_target_health_invalid:{enemyDeviceTarget.name}");
                Require(targetData.FindProperty("visualRoot")?.objectReferenceValue != null,
                    $"enemy_device_target_visual_missing:{enemyDeviceTarget.name}");
                var destroyedEvent = targetData.FindProperty("destroyedEvent");
                Require(destroyedEvent != null
                        && Enum.IsDefined(typeof(EventId), destroyedEvent.intValue)
                        && destroyedEvent.intValue > 0,
                    $"enemy_device_target_event_missing:{enemyDeviceTarget.name}");
            }
            Require(coordinator.GetComponent<NetworkObject>() != null,
                "coordinator_network_object_missing");
            Require(presenter.ValidateConfiguration(),
                "effect_presenter_invalid");
            var runSessionRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                RunSessionRootPrefabPath);
            Require(runSessionRootPrefab != null,
                $"run_session_root_prefab_missing:{RunSessionRootPrefabPath}");
            var impactAdapter = runSessionRootPrefab.GetComponent<PHSShipEventImpactAdapter>();
            Require(impactAdapter != null,
                "ship_event_impact_adapter_missing");
            Require(impactAdapter.GetComponent<NetworkShipSystemsState>() != null,
                "ship_event_impact_adapter_owner_invalid");
            ValidateFailureConsequenceMappings();

            RequireReference(eventManager, "registry");
            RequireReference(scheduler, "coordinator", coordinator);
            RequireReference(coordinator, "eventManager", eventManager);
            RequireReference(coordinator, "eventScheduler", scheduler);
            RequireReference(coordinator, "roomRegistry", roomRegistry);
            RequireReference(coordinator, "effectMirrorPresenter", presenter);
            RequireReference(runtime, "externalThreatScheduler", scheduler);

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
                + $"enemyDeviceTargets={enemyDeviceTargets.Length} "
                + "failureConsequences=3 missingScripts=0");
        }

        private static void ValidateFailureConsequenceMappings()
        {
            RequireFailureConsequence(EventId.EmpAttack, EventId.Fire);
            RequireFailureConsequence(EventId.MeteorAttack, EventId.OxygenLeak);
            RequireFailureConsequence(EventId.EnemyScout, EventId.EnemySpawn);
        }

        private static void RequireFailureConsequence(
            EventId sourceEventId,
            EventId expectedConsequenceEventId)
        {
            Require(
                PHSShipEventImpactAdapter.TryGetFailureConsequence(
                    sourceEventId,
                    out var actualConsequenceEventId)
                && actualConsequenceEventId == expectedConsequenceEventId,
                $"failure_consequence_mismatch:{sourceEventId}:{actualConsequenceEventId}");
        }

        private static void WireNetworkRuntime(Scene scene)
        {
            var eventManager = RequireOne<EventManager>(scene, "event_manager");
            var scheduler = RequireOne<PHSNetworkEventScheduler>(scene, "event_scheduler");
            var roomRegistry = RequireOne<RoomRegistry>(scene, "room_registry");
            var coordinator = RequireOne<NetworkEventCoordinator>(scene, "event_coordinator");
            var presenter = RequireOne<NetworkEventEffectMirrorPresenter>(scene, "effect_presenter");
            var runtime = RequireOne<PHSMapRuntimeContext>(scene, "map_runtime");
            Require(coordinator.GetComponent<NetworkObject>() != null,
                "coordinator_network_object_missing");

            SetReference(scheduler, "coordinator", coordinator);
            SetReference(coordinator, "eventManager", eventManager);
            SetReference(coordinator, "eventScheduler", scheduler);
            SetReference(coordinator, "roomRegistry", roomRegistry);
            SetReference(coordinator, "effectMirrorPresenter", presenter);
            SetReference(runtime, "externalThreatScheduler", scheduler);
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
