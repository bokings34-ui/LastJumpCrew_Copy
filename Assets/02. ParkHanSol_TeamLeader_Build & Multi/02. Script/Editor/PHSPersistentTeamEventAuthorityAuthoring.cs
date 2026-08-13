using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SM;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Places the team event NetworkObject under the persistent run root.  A map-scene
    /// instance is destroyed by a Shop LoadSceneMode.Single transition and therefore
    /// cannot own team incident state.
    /// </summary>
    public static class PHSPersistentTeamEventAuthorityAuthoring
    {
        private const string RunRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";
        private const string EventRuntimePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/" +
            "Prefab/Integration0716/PHS_EventRuntimeSystem.prefab";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/" +
            "PHS_Map_ver1.unity";
        private static readonly (string RoomId, string StationName)[] RoomStationBindings =
        {
            ("Room A", "PHS_Utility_BatteryStation_RoomA"),
            ("Room B", "PHS_Utility_BatteryStation"),
            ("Room C", "PHS_Utility_BatteryStation_RoomC"),
            ("중앙 복도", "PHS_Utility_BatteryStation_RoomD")
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Persistent Team Event Authority")]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=play_mode_active");
            }

            AuthorRunRoot();
            RemoveSceneAuthority();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            Debug.Log(
                "PHS_PERSISTENT_EVENT_AUTHOR_OK authority=session_root " +
                "map_scene_authority=removed destroyWithScene=false");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Persistent Team Event Authority")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var runRoot = AssetDatabase.LoadAssetAtPath<GameObject>(RunRootPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=run_root_missing");
            var root = runRoot.GetComponent<NetworkRunSessionRoot>();
            var coordinator = runRoot.GetComponent<NetworkEventCoordinator>();
            var scheduler = runRoot.GetComponentInChildren<PHSNetworkEventScheduler>(true);
            if (root == null || coordinator == null || scheduler == null
                || root.GetComponent<Unity.Netcode.NetworkObject>() == null
                || coordinator.GetComponent<Unity.Netcode.NetworkObject>() != root.GetComponent<Unity.Netcode.NetworkObject>()
                || runRoot.GetComponentsInChildren<NetworkEventCoordinator>(true).Length != 1)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=authority_contract_missing");
            }

            var rootData = new SerializedObject(root);
            if (rootData.FindProperty("eventCoordinator")?.objectReferenceValue != coordinator
                || rootData.FindProperty("eventScheduler")?.objectReferenceValue != scheduler)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=session_root_reference_invalid");
            }

            var coordinatorData = new SerializedObject(coordinator);
            var eventManager = coordinatorData.FindProperty("eventManager")?.objectReferenceValue as EventManager;
            var roomRegistry = coordinatorData.FindProperty("roomRegistry")?.objectReferenceValue as RoomRegistry;
            var presenter = coordinatorData.FindProperty("effectMirrorPresenter")?.objectReferenceValue
                as NetworkEventEffectMirrorPresenter;
            var micVoicePresenter = runRoot.GetComponentInChildren<MicDestroyVoiceEffectPresenter>(true);
            if (eventManager == null
                || roomRegistry == null
                || roomRegistry.gameObject != runRoot
                || runRoot.GetComponentsInChildren<RoomRegistry>(true).Length != 1
                || presenter == null
                || !presenter.ValidateConfiguration()
                || micVoicePresenter == null
                || new SerializedObject(micVoicePresenter).FindProperty("eventCoordinator")?.objectReferenceValue
                    != coordinator
                || coordinatorData.FindProperty("eventScheduler")?.objectReferenceValue != scheduler
                || new SerializedObject(scheduler).FindProperty("coordinator")?.objectReferenceValue != coordinator)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=coordinator_runtime_reference_invalid");
            }

            ValidatePersistentRoomStationAlignment(runRoot);

            WithMapScene(scene =>
            {
                var sceneAuthorities = scene.GetRootGameObjects()
                    .SelectMany(candidate => candidate.GetComponentsInChildren<NetworkEventCoordinator>(true))
                    .ToArray();
                if (sceneAuthorities.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=map_scene_authority_present actual={sceneAuthorities.Length}");
                }

                var eventManagers = scene.GetRootGameObjects()
                    .SelectMany(candidate => candidate.GetComponentsInChildren<EventManager>(true))
                    .ToArray();
                if (eventManagers.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=map_scene_event_manager_present actual={eventManagers.Length}");
                }
            });

            Debug.Log(
                "PHS_PERSISTENT_EVENT_VALIDATE_OK authority=session_root " +
                "map_scene_authority=0 destroyWithScene=false");
        }

        private static void AuthorRunRoot()
        {
            var stationPositions = GetMapStationPositions();
            var runRoot = PrefabUtility.LoadPrefabContents(RunRootPrefabPath);
            try
            {
                var root = runRoot.GetComponent<NetworkRunSessionRoot>()
                    ?? throw new InvalidOperationException(
                        "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=run_root_component_missing");
                var authorityRoot = FindOrCreateAuthorityDataRoot(runRoot.transform);
                authorityRoot.name = "PHS_PersistentTeamEventAuthority";
                authorityRoot.transform.SetParent(runRoot.transform, false);
                authorityRoot.transform.localPosition = Vector3.zero;
                authorityRoot.transform.localRotation = Quaternion.identity;
                authorityRoot.transform.localScale = Vector3.one;

                var previousCoordinator = authorityRoot.GetComponent<NetworkEventCoordinator>();
                var coordinator = runRoot.GetComponent<NetworkEventCoordinator>();
                if (coordinator == null)
                {
                    coordinator = runRoot.AddComponent<NetworkEventCoordinator>();
                }

                var sourceCoordinator = previousCoordinator != null
                    ? previousCoordinator
                    : LoadSourceCoordinator();
                if (sourceCoordinator != null && sourceCoordinator != coordinator)
                {
                    EditorUtility.CopySerialized(sourceCoordinator, coordinator);
                }

                var scheduler = authorityRoot.GetComponent<PHSNetworkEventScheduler>()
                    ?? throw new InvalidOperationException(
                        "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=scheduler_missing");
                var roomRegistry = EnsureRootRoomRegistry(runRoot, authorityRoot);
                RemoveNestedAuthorityNetworkComponents(authorityRoot, coordinator);
                SetReference(root, "eventCoordinator", coordinator);
                SetReference(root, "eventScheduler", scheduler);
                SetReference(scheduler, "coordinator", coordinator);
                SetReference(coordinator, "eventManager", RequireOne<EventManager>(authorityRoot, "event_manager"));
                SetReference(coordinator, "eventScheduler", scheduler);
                SetReference(coordinator, "roomRegistry", roomRegistry);
                SetReference(
                    coordinator,
                    "effectMirrorPresenter",
                    RequireOne<NetworkEventEffectMirrorPresenter>(authorityRoot, "effect_mirror_presenter"));
                SetReference(
                    RequireOne<MicDestroyVoiceEffectPresenter>(authorityRoot, "mic_destroy_voice_presenter"),
                    "eventCoordinator",
                    coordinator);
                AlignPersistentRoomsToStations(authorityRoot, stationPositions);
                PrefabUtility.SaveAsPrefabAsset(runRoot, RunRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(runRoot);
            }
        }

        private static Dictionary<string, Vector3> GetMapStationPositions()
        {
            var positions = new Dictionary<string, Vector3>(RoomStationBindings.Length);
            WithMapScene(scene =>
            {
                var transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true));
                foreach (var binding in RoomStationBindings)
                {
                    var matches = transforms
                        .Where(transform => transform.name == binding.StationName)
                        .ToArray();
                    if (matches.Length != 1)
                    {
                        throw new InvalidOperationException(
                            $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=room_station_ambiguous " +
                            $"room={binding.RoomId} station={binding.StationName} actual={matches.Length}");
                    }

                    positions.Add(binding.RoomId, matches[0].position);
                }
            });
            return positions;
        }

        private static void AlignPersistentRoomsToStations(
            GameObject authorityRoot,
            IReadOnlyDictionary<string, Vector3> stationPositions)
        {
            var rooms = authorityRoot.GetComponentsInChildren<ShipRoom>(true);
            foreach (var binding in RoomStationBindings)
            {
                var matches = rooms.Where(room => room.RoomId == binding.RoomId).ToArray();
                if (matches.Length != 1 || !stationPositions.TryGetValue(binding.RoomId, out var stationPosition))
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=room_station_binding_invalid " +
                        $"room={binding.RoomId} rooms={matches.Length}");
                }

                matches[0].transform.position = stationPosition;
                EditorUtility.SetDirty(matches[0].transform);
            }
        }

        private static void ValidatePersistentRoomStationAlignment(GameObject runRoot)
        {
            var stationPositions = GetMapStationPositions();
            var rooms = runRoot.GetComponentsInChildren<ShipRoom>(true);
            if (rooms.Length != RoomStationBindings.Length)
            {
                throw new InvalidOperationException(
                    $"PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=persistent_room_count_invalid " +
                    $"actual={rooms.Length} expected={RoomStationBindings.Length}");
            }
            foreach (var binding in RoomStationBindings)
            {
                var matches = rooms.Where(room => room.RoomId == binding.RoomId).ToArray();
                if (matches.Length != 1
                    || !stationPositions.TryGetValue(binding.RoomId, out var stationPosition)
                    || (matches[0].transform.position - stationPosition).sqrMagnitude > 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"PHS_PERSISTENT_EVENT_VALIDATE_FAILED reason=room_station_position_invalid " +
                        $"room={binding.RoomId} station={binding.StationName} rooms={matches.Length}");
                }
            }
        }

        private static GameObject FindOrCreateAuthorityDataRoot(Transform parent)
        {
            var existing = parent
                .GetComponentsInChildren<PHSNetworkEventScheduler>(true)
                .Select(scheduler => scheduler.gameObject)
                .Distinct()
                .ToArray();
            if (existing.Length == 1)
            {
                return existing[0];
            }

            if (existing.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=authority_data_root_ambiguous actual={existing.Length}");
            }

            return CreateAuthorityInstance(parent);
        }

        private static NetworkEventCoordinator LoadSourceCoordinator()
        {
            var authorityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventRuntimePrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=event_runtime_prefab_missing");
            return authorityPrefab.GetComponentInChildren<NetworkEventCoordinator>(true)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=event_runtime_source_coordinator_missing");
        }

        private static T RequireOne<T>(GameObject authorityRoot, string key)
            where T : Component
        {
            var matches = authorityRoot.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason={key}_invalid actual={matches.Length}");
            }

            return matches[0];
        }

        private static void RemoveNestedAuthorityNetworkComponents(
            GameObject authorityRoot,
            NetworkEventCoordinator sessionRootCoordinator)
        {
            foreach (var nestedCoordinator in authorityRoot
                         .GetComponentsInChildren<NetworkEventCoordinator>(true)
                         .Where(candidate => candidate != sessionRootCoordinator)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(nestedCoordinator);
            }

            var nestedAuthorityNetworkObject = authorityRoot.GetComponent<Unity.Netcode.NetworkObject>();
            if (nestedAuthorityNetworkObject != null
                && nestedAuthorityNetworkObject.gameObject != sessionRootCoordinator.gameObject)
            {
                UnityEngine.Object.DestroyImmediate(nestedAuthorityNetworkObject);
            }
        }

        private static RoomRegistry EnsureRootRoomRegistry(
            GameObject runRoot,
            GameObject authorityRoot)
        {
            var roomRegistry = runRoot.GetComponent<RoomRegistry>();
            if (roomRegistry == null)
            {
                roomRegistry = runRoot.AddComponent<RoomRegistry>();
            }

            foreach (var nestedRegistry in authorityRoot
                         .GetComponentsInChildren<RoomRegistry>(true)
                         .Where(candidate => candidate != roomRegistry)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(nestedRegistry);
            }

            return roomRegistry;
        }

        private static GameObject CreateAuthorityInstance(Transform parent)
        {
            var authorityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventRuntimePrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=event_runtime_prefab_missing");
            var instance = PrefabUtility.InstantiatePrefab(authorityPrefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=event_runtime_instantiate_failed");
            }

            return instance;
        }

        private static void RemoveSceneAuthority()
        {
            WithMapScene(scene =>
            {
                var authorities = scene.GetRootGameObjects()
                    .SelectMany(candidate => candidate.GetComponentsInChildren<NetworkEventCoordinator>(true))
                    .ToArray();
                foreach (var authority in authorities)
                {
                    var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(authority.gameObject);
                    if (prefabRoot == null)
                    {
                        throw new InvalidOperationException(
                            $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=scene_authority_not_prefab anchor={authority.name}");
                    }

                    UnityEngine.Object.DestroyImmediate(prefabRoot);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=map_scene_save_failed");
                }
            });
        }

        private static void WithMapScene(Action<Scene> action)
        {
            var scene = SceneManager.GetSceneByPath(MapScenePath);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void SetReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value)
        {
            var data = new SerializedObject(owner);
            var property = data.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_PERSISTENT_EVENT_AUTHOR_FAILED reason=property_missing property={propertyName}");
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }
    }
}
