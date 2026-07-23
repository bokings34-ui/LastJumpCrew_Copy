using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using LastJumpCrew.ParkHanSol.Multiplayer.Validation;
using LastJumpCrew.ParkHanSol.Shop;
using LastJumpCrew.SeoBoGyeong.Economy;
using SM;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHS0715IntegrationValidator
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string FeatureInspectionScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity";
        private const string MapSceneName = "PHS_Map_ver1";
        private const string ShopSceneName = "PHS_ExteriorShopScene";
        private const string ShopCatalogPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/ShopProducts/PHS_ShopCatalog_0715.asset";
        private const string UtilityItemCatalogPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/PHS_UtilityItemCatalog_0717.asset";
        private const string SellStationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/ShopCheckoutCounter/PHS_DebrisSellStation.prefab";
        private const string PlayHudPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab";
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string ShipRuntimePrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab";
        private const string TradeStationPrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/02. Prefab_SeoBoGyeong_Game Economy/TradeStation.prefab";
        private const string ShopDisplayDeskPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/PHS_ShopDisplayDesk_Shared.prefab";
        private const string EventPresentationPrefabFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation";
        private const string MapProfileFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps";

        private static readonly string[] RequiredBuildScenes =
        {
            LobbyScenePath,
            TutorialScenePath,
            MapScenePath,
            ShopScenePath
        };

        private static readonly int[] IncidentGameplayMapIds =
        {
            8001,
            8002,
            8003,
            8004
        };

        private static readonly Type[] GravityDuplicateGuardTypes =
        {
            typeof(PlayerGravityReceiver),
            typeof(ZeroGravityCollisionPusher),
            typeof(AudioSource),
            typeof(RigidbodyGrappleTarget),
            typeof(GrappleCollectibleItem)
        };

        [MenuItem("Tools/ParkHanSol/Validate 0715 Integration")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        [MenuItem("Tools/ParkHanSol/Migrate 0715 Economy Ledger")]
        public static void MigrateLegacyEconomyOwnersToRunRoot()
        {
            var scenePaths = new[] { MapScenePath, ShopScenePath };
            var removedCount = 0;
            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var legacyRoots = UnityEngine.Object.FindObjectsByType<SessionPurchaseStateRoot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                foreach (var legacyRoot in legacyRoots)
                {
                    var owner = legacyRoot.gameObject;
                    UnityEngine.Object.DestroyImmediate(legacyRoot, true);
                    removedCount++;
                    Debug.Log(
                        $"PHS_ECONOMY_LEDGER_MIGRATED scene={scenePath} object={GetHierarchyPath(owner.transform)} keptDeliveryAdapter={owner.GetComponent<SessionPurchaseDeliveryService>() != null}",
                        owner);
                }

                if (legacyRoots.Length > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException(
                            $"PHS_ECONOMY_LEDGER_MIGRATION_FAILED scene={scenePath}");
                    }
                }
            }

            Debug.Log($"PHS_ECONOMY_LEDGER_MIGRATION_OK removed={removedCount} scenes={scenePaths.Length}");
        }

        [MenuItem("Tools/ParkHanSol/Migrate 0718 Run RNG Ledger")]
        public static void MigrateRunRandomLedgerToRoot()
        {
            var prefab = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"PHS_RUN_RNG_MIGRATION_FAILED reason=prefab_missing path={RunSessionRootPrefabPath}");
            }

            try
            {
                var ledgers = prefab.GetComponents<NetworkRunRandomLedger>();
                if (ledgers.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_RUN_RNG_MIGRATION_FAILED reason=duplicate_ledger count={ledgers.Length}");
                }

                var ledger = ledgers.Length == 1
                    ? ledgers[0]
                    : prefab.AddComponent<NetworkRunRandomLedger>();
                var networkBehaviours = prefab.GetComponents<NetworkBehaviour>();
                if (networkBehaviours.Length == 0
                    || networkBehaviours[networkBehaviours.Length - 1] != ledger)
                {
                    throw new InvalidOperationException(
                        "PHS_RUN_RNG_MIGRATION_FAILED reason=ledger_not_last_network_behaviour");
                }

                if (PrefabUtility.SaveAsPrefabAsset(prefab, RunSessionRootPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_RUN_RNG_MIGRATION_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            Debug.Log(
                $"PHS_RUN_RNG_MIGRATION_OK prefab={RunSessionRootPrefabPath}");
        }

        [MenuItem("Tools/ParkHanSol/Migrate 0718 Incident Ledger")]
        public static void MigrateRunIncidentLedgerIntegration()
        {
            var originalActiveScene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (originalActiveScene.IsValid() && originalActiveScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"PHS_INCIDENT_MIGRATION_FAILED reason=active_scene_dirty scene={originalActiveScene.path}");
            }

            var loadedMapScene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MapScenePath);
            if (loadedMapScene.IsValid()
                && loadedMapScene.isLoaded
                && loadedMapScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"PHS_INCIDENT_MIGRATION_FAILED reason=map_scene_dirty scene={MapScenePath}");
            }

            var profileCount = MigrateIncidentMapProfiles();
            MigrateIncidentRootPrefab();
            var roomCount = MigrateMapIncidentConsumerPreservingActiveScene(
                originalActiveScene);
            Debug.Log(
                $"PHS_INCIDENT_MIGRATION_OK prefab={RunSessionRootPrefabPath} " +
                $"profiles={profileCount} rooms={roomCount} scene={MapScenePath}");
        }

        private static int MigrateIncidentMapProfiles()
        {
            var targetIds = new HashSet<int>(IncidentGameplayMapIds);
            var profiles = AssetDatabase
                .FindAssets(
                    "t:PHSMapProfileSO",
                    new[] { MapProfileFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path =>
                    AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>(path))
                .Where(profile => profile != null && targetIds.Contains(profile.MapId))
                .ToArray();
            foreach (var mapId in IncidentGameplayMapIds)
            {
                var matches = profiles
                    .Where(profile => profile.MapId == mapId)
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED reason=profile_count_invalid " +
                        $"map={mapId} actual={matches.Length}");
                }

                var profile = matches[0];
                var serializedProfile = new SerializedObject(profile);
                var pressureCapacity =
                    serializedProfile.FindProperty("incidentPressureCapacity");
                var maximumExternal =
                    serializedProfile.FindProperty("maximumActiveExternalThreats");
                var maximumInternal =
                    serializedProfile.FindProperty("maximumActiveInternalAccidents");
                if (pressureCapacity == null
                    || maximumExternal == null
                    || maximumInternal == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED reason=profile_contract_missing " +
                        $"map={mapId} path={AssetDatabase.GetAssetPath(profile)}");
                }

                pressureCapacity.intValue = 3;
                maximumExternal.intValue = 1;
                maximumInternal.intValue = 2;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            return profiles.Length;
        }

        private static void MigrateIncidentRootPrefab()
        {
            var prefab = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"PHS_INCIDENT_MIGRATION_FAILED reason=prefab_missing " +
                    $"path={RunSessionRootPrefabPath}");
            }

            try
            {
                var ledgers =
                    prefab.GetComponentsInChildren<NetworkRunIncidentLedger>(true);
                if (ledgers.Length > 1
                    || (ledgers.Length == 1
                        && ledgers[0].gameObject != prefab))
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED " +
                        $"reason=incident_ledger_owner_invalid count={ledgers.Length}");
                }

                var ledger = ledgers.Length == 1
                    ? ledgers[0]
                    : prefab.AddComponent<NetworkRunIncidentLedger>();
                var serializedLedger = new SerializedObject(ledger);
                var defaultPressure = serializedLedger
                    .FindProperty("defaultPressureCapacity");
                var defaultExternal = serializedLedger
                    .FindProperty("defaultMaximumExternalCommands");
                var defaultInternal = serializedLedger
                    .FindProperty("defaultMaximumInternalCommands");
                var commandHistory = serializedLedger
                    .FindProperty("maximumCommandHistory");
                if (defaultPressure == null
                    || defaultExternal == null
                    || defaultInternal == null
                    || commandHistory == null)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=incident_ledger_contract_missing");
                }

                defaultPressure.intValue = 3;
                defaultExternal.intValue = 1;
                defaultInternal.intValue = 2;
                commandHistory.intValue = Math.Max(
                    32,
                    commandHistory.intValue);
                serializedLedger.ApplyModifiedPropertiesWithoutUndo();
                var directors =
                    prefab.GetComponentsInChildren<PHSNetworkIncidentDirector>(true);
                if (directors.Length > 1
                    || (directors.Length == 1
                        && directors[0].gameObject != prefab))
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED " +
                        $"reason=incident_director_owner_invalid count={directors.Length}");
                }

                _ = directors.Length == 1
                    ? directors[0]
                    : prefab.AddComponent<PHSNetworkIncidentDirector>();
                var networkBehaviours = prefab.GetComponents<NetworkBehaviour>();
                if (networkBehaviours.Length == 0
                    || networkBehaviours[networkBehaviours.Length - 1] != ledger)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=incident_ledger_not_last_network_behaviour");
                }

                if (PrefabUtility.SaveAsPrefabAsset(
                        prefab,
                        RunSessionRootPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static int MigrateMapIncidentConsumerPreservingActiveScene(
            UnityEngine.SceneManagement.Scene originalActiveScene)
        {
            var mapScene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MapScenePath);
            var openedMapScene = false;
            try
            {
                if (!mapScene.IsValid() || !mapScene.isLoaded)
                {
                    mapScene = EditorSceneManager.OpenScene(
                        MapScenePath,
                        OpenSceneMode.Additive);
                    openedMapScene = true;
                }

                if (!mapScene.IsValid() || !mapScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED reason=map_scene_open_failed " +
                        $"scene={MapScenePath}");
                }

                var runtimes = FindSceneComponents<PHSMapRuntimeContext>(mapScene);
                var eventCoordinators =
                    FindSceneComponents<NetworkEventCoordinator>(mapScene);
                var accidentCoordinators =
                    FindSceneComponents<PHSNetworkShipAccidentCoordinator>(mapScene);
                var rooms = FindSceneComponents<ShipRoom>(mapScene)
                    .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                    .ToArray();
                if (runtimes.Length != 1
                    || eventCoordinators.Length != 1
                    || accidentCoordinators.Length != 1
                    || rooms.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED reason=map_contract_invalid " +
                        $"runtime={runtimes.Length} event={eventCoordinators.Length} " +
                        $"accident={accidentCoordinators.Length} rooms={rooms.Length}");
                }

                if (rooms.Any(room => string.IsNullOrWhiteSpace(room.RoomId))
                    || rooms.Select(room => room.RoomId)
                        .Distinct(StringComparer.Ordinal)
                        .Count() != rooms.Length)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=map_room_ids_invalid");
                }

                var runtime = runtimes[0];
                var eventCoordinator = eventCoordinators[0];
                var accidentCoordinator = accidentCoordinators[0];
                var consumers =
                    FindSceneComponents<PHSMapIncidentCommandConsumer>(mapScene);
                if (consumers.Length > 1
                    || (consumers.Length == 1
                        && consumers[0].gameObject != runtime.gameObject))
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED " +
                        $"reason=incident_consumer_owner_invalid count={consumers.Length}");
                }

                var consumer = consumers.Length == 1
                    ? consumers[0]
                    : runtime.gameObject.AddComponent<PHSMapIncidentCommandConsumer>();
                var serializedConsumer = new SerializedObject(consumer);
                var consumerEvent =
                    serializedConsumer.FindProperty("eventCoordinator");
                var consumerAccident =
                    serializedConsumer.FindProperty("accidentCoordinator");
                var consumerRooms = serializedConsumer.FindProperty("rooms");
                if (consumerEvent == null
                    || consumerAccident == null
                    || consumerRooms == null
                    || !consumerRooms.isArray)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=incident_consumer_contract_missing");
                }

                consumerEvent.objectReferenceValue = eventCoordinator;
                consumerAccident.objectReferenceValue = accidentCoordinator;
                consumerRooms.arraySize = rooms.Length;
                for (var index = 0; index < rooms.Length; index++)
                {
                    consumerRooms
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue = rooms[index];
                }

                serializedConsumer.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(consumer);

                var serializedRuntime = new SerializedObject(runtime);
                var runtimeConsumer =
                    serializedRuntime.FindProperty("incidentCommandConsumer");
                if (runtimeConsumer == null)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=map_runtime_consumer_contract_missing");
                }

                runtimeConsumer.objectReferenceValue = consumer;
                serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(runtime);

                var serializedEventCoordinator =
                    new SerializedObject(eventCoordinator);
                var autoStart = serializedEventCoordinator
                    .FindProperty("startSchedulerOnServerSpawn");
                if (autoStart == null)
                {
                    throw new InvalidOperationException(
                        "PHS_INCIDENT_MIGRATION_FAILED " +
                        "reason=event_auto_start_contract_missing");
                }

                autoStart.boolValue = false;
                serializedEventCoordinator.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    eventCoordinator);

                EditorSceneManager.MarkSceneDirty(mapScene);
                if (!EditorSceneManager.SaveScene(mapScene))
                {
                    throw new InvalidOperationException(
                        $"PHS_INCIDENT_MIGRATION_FAILED reason=map_scene_save_failed " +
                        $"scene={MapScenePath}");
                }

                return rooms.Length;
            }
            finally
            {
                if (originalActiveScene.IsValid()
                    && originalActiveScene.isLoaded)
                {
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(
                        originalActiveScene);
                }

                if (openedMapScene
                    && mapScene.IsValid()
                    && mapScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(mapScene, true);
                }
            }
        }

        public static string ValidateOrThrow()
        {
            var originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var canRestoreOriginalSceneSetup = originalSceneSetup.Any(
                setup => setup.isLoaded && setup.isActive);
            var dirtyLoadedScenes = Enumerable
                .Range(
                    0,
                    UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Select(
                    UnityEngine.SceneManagement.SceneManager.GetSceneAt)
                .Where(scene =>
                    scene.IsValid()
                    && scene.isLoaded
                    && scene.isDirty)
                .Select(scene =>
                    string.IsNullOrWhiteSpace(scene.path)
                        ? $"<unsaved>:{scene.name}"
                        : scene.path)
                .ToArray();
            if (dirtyLoadedScenes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_0715_VALIDATE_FAILED reason=loaded_scene_dirty " +
                    $"scenes={string.Join(",", dirtyLoadedScenes)}");
            }

            var errors = new List<string>();
            try
            {
                ValidateBuildSettings(errors);
                ValidateLobbyScene(errors);
                ValidateTutorialScene(errors);
                ValidateMapScene(errors);
                ValidateShopScene(errors);
                ValidateShopPresentationPrefabs(errors);
                ValidateUtilityItemCatalog(errors);
                ValidateSellStationPrefab(errors);
                ValidatePlayHudPrefab(errors);
                ValidatePlayerPrefab(errors);
                ValidateRunSessionRootPrefab(errors);
                ValidateShipRuntimePrefab(errors);
                ValidateEventPresentationPrefabs(errors);
            }
            finally
            {
                if (canRestoreOriginalSceneSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        originalSceneSetup);
                }
            }

            if (errors.Count > 0)
            {
                var message = $"PHS_0715_VALIDATE_FAILED count={errors.Count}\n- {string.Join("\n- ", errors)}";
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            const string success = "PHS_0715_VALIDATE_OK errors=0 scenes=4 prefabs=11";
            Debug.Log(success);
            return success;
        }

        private static void ValidateBuildSettings(ICollection<string> errors)
        {
            var configuredScenes = EditorBuildSettings.scenes;
            foreach (var configuredScene in configuredScenes)
            {
                Require(
                    !string.IsNullOrWhiteSpace(configuredScene.path)
                    && AssetDatabase.LoadAssetAtPath<SceneAsset>(configuredScene.path) != null,
                    $"build_scene_missing path={configuredScene.path}",
                    errors);
            }

            var enabledScenes = configuredScenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            Require(
                enabledScenes.SequenceEqual(RequiredBuildScenes, StringComparer.Ordinal),
                $"build_scenes_invalid expected={string.Join(",", RequiredBuildScenes)} actual={string.Join(",", enabledScenes)}",
                errors);
        }

        private static void ValidateLobbyScene(ICollection<string> errors)
        {
            OpenAndValidateScene(LobbyScenePath, errors);
            ValidateNoSceneOwnedStageClock("lobby", errors);
            ValidateNoSceneOwnedEconomyLedger("lobby", errors);
            ValidateNoSceneOwnedRandomLedger("lobby", errors);
            ValidateNoSceneOwnedIncidentRootComponents("lobby", errors);
            ValidateNoLegacyEconomyOwner("lobby", errors);
            var networkManager = FindOne<NetworkManager>("lobby_network_manager", errors);
            if (networkManager == null)
            {
                return;
            }

            Require(networkManager.GetComponent<UnityTransport>() != null, "lobby_transport_missing", errors);
            Require(networkManager.NetworkConfig != null, "lobby_network_config_missing", errors);
            Require(
                networkManager.NetworkConfig != null && networkManager.NetworkConfig.PlayerPrefab != null,
                "lobby_player_prefab_missing",
                errors);
            ValidateLobbyNetworkPrefabRegistration(networkManager, errors);
            var rootBootstrap = FindOne<NetworkRunSessionRootBootstrap>(
                "lobby_run_session_root_bootstrap",
                errors);
            if (rootBootstrap != null)
            {
                Require(
                    rootBootstrap.gameObject == networkManager.gameObject,
                    "lobby_run_session_root_bootstrap_owner_invalid",
                    errors);
                var serializedBootstrap = new SerializedObject(rootBootstrap);
                var configuredRoot = serializedBootstrap
                    .FindProperty("runSessionRootPrefab")
                    ?.objectReferenceValue;
                Require(
                    configuredRoot != null
                    && AssetDatabase.GetAssetPath(configuredRoot) == RunSessionRootPrefabPath,
                    "lobby_run_session_root_prefab_invalid",
                    errors);
            }

            FindOne<MultiplayerRoomService>("lobby_room_service", errors);
        }

        private static void ValidateTutorialScene(ICollection<string> errors)
        {
            OpenAndValidateScene(TutorialScenePath, errors);
            var director = FindOne<NetworkTutorialDirector>(
                "tutorial_director",
                errors);
            if (director == null)
            {
                return;
            }

            var serializedDirector = new SerializedObject(director);
            RequireObject(
                serializedDirector,
                "playerController",
                "tutorial_player_controller_missing",
                errors);
            RequireObject(
                serializedDirector,
                "grappleController",
                "tutorial_grapple_controller_missing",
                errors);
            RequireObject(
                serializedDirector,
                "itemHolder",
                "tutorial_item_holder_missing",
                errors);
            RequireObject(
                serializedDirector,
                "instructionText",
                "tutorial_instruction_text_missing",
                errors);
            RequireObject(
                serializedDirector,
                "completionPanel",
                "tutorial_completion_panel_missing",
                errors);
            RequireObject(
                serializedDirector,
                "returnToLobbyButton",
                "tutorial_return_button_missing",
                errors);

            var station = FindOne<NetworkTutorialInteractionStation>(
                "tutorial_interaction_station",
                errors);
            if (station != null)
            {
                RequireObject(
                    new SerializedObject(station),
                    "tutorialDirector",
                    "tutorial_station_director_missing",
                    errors);
            }
        }

        private static void ValidateLobbyNetworkPrefabRegistration(
            NetworkManager networkManager,
            ICollection<string> errors)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(UtilityItemCatalogPath);
            Require(catalog != null, "lobby_utility_item_catalog_missing", errors);
            if (catalog == null || networkManager.NetworkConfig == null)
            {
                return;
            }

            var networkPrefabLists = networkManager.NetworkConfig.Prefabs?.NetworkPrefabsLists;
            Require(
                networkPrefabLists != null && networkPrefabLists.Count > 0,
                "lobby_network_prefab_lists_missing",
                errors);
            if (networkPrefabLists == null)
            {
                return;
            }

            var runSessionRootPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPrefabPath);
            Require(
                runSessionRootPrefab != null,
                $"lobby_run_session_root_prefab_missing path={RunSessionRootPrefabPath}",
                errors);
            if (runSessionRootPrefab != null)
            {
                Require(
                    networkPrefabLists.Any(
                        list => list != null && list.Contains(runSessionRootPrefab)),
                    "lobby_run_session_root_prefab_unregistered",
                    errors);
            }

            foreach (var itemData in catalog.Items)
            {
                var droppedPrefab = itemData?.DroppedPrefab;
                if (droppedPrefab == null)
                {
                    continue;
                }

                Require(
                    networkPrefabLists.Any(list => list != null && list.Contains(droppedPrefab)),
                    $"lobby_network_prefab_unregistered item={itemData.ItemId} path={AssetDatabase.GetAssetPath(droppedPrefab)}",
                    errors);
            }
        }

        private static void ValidateMapScene(ICollection<string> errors)
        {
            OpenAndValidateScene(MapScenePath, errors);
            Require(
                PHS0719IncidentLocationAuthoring.ValidateAuthoredScene(
                    out var incidentLocationReason),
                $"map_incident_location_authoring_invalid " +
                $"reason={incidentLocationReason}",
                errors);
            ValidateNoSceneOwnedStageClock("map", errors);
            ValidateNoSceneOwnedEconomyLedger("map", errors);
            ValidateNoSceneOwnedRandomLedger("map", errors);
            ValidateNoSceneOwnedIncidentRootComponents("map", errors);
            ValidateNoLegacyEconomyOwner("map", errors);
            ValidateGravityDuplicateComponents("map", errors);
            ValidateGameplayContext("map", errors);

            PHSMapCatalogSO mapCatalog = null;
            var console = FindOne<NetworkTravelConsoleController>("map_travel_console", errors);
            if (console != null)
            {
                Require(console.enabled, "map_travel_console_disabled", errors);
                Require(console.GetComponent<NetworkObject>() != null, "map_travel_console_network_object_missing", errors);
                var serializedConsole = new SerializedObject(console);
                mapCatalog = serializedConsole.FindProperty("mapCatalog")?.objectReferenceValue as PHSMapCatalogSO;
                ValidateMapCatalog("map_travel_console", mapCatalog, 2, errors);
                RequireObject(serializedConsole, "mapRuntimeContext", "map_travel_console_runtime_context_missing", errors);
                RequireObject(serializedConsole, "debrisScreenText", "map_debris_screen_missing", errors);
                RequireObject(serializedConsole, "shopScreenText", "map_shop_screen_missing", errors);
                RequireObject(serializedConsole, "actionScreenText", "map_action_screen_missing", errors);
                RequireArray(serializedConsole, "debrisChoiceObjects", 1, "map_debris_choice_objects_missing", errors);
            }

            var warpPresenter = FindOne<WarpTransitionPresenter>("map_warp_presenter", errors);
            if (warpPresenter != null)
            {
                var serializedWarpPresenter = new SerializedObject(warpPresenter);
                RequireObject(serializedWarpPresenter, "transitionCanvasGroup", "map_warp_canvas_missing", errors);
                RequireObject(serializedWarpPresenter, "warpVisualRoot", "map_warp_visual_missing", errors);
                RequireObject(serializedWarpPresenter, "warpStatusCardRoot", "map_warp_status_card_missing", errors);
                RequireObject(serializedWarpPresenter, "warpStatusText", "map_warp_status_text_missing", errors);
                RequireObject(serializedWarpPresenter, "normalSkybox", "map_warp_normal_skybox_missing", errors);
                RequireObject(serializedWarpPresenter, "warpSkybox", "map_warp_skybox_missing", errors);
                RequireObject(serializedWarpPresenter, "arrivalSkybox", "map_warp_arrival_skybox_missing", errors);
            }

            FindOne<PHSMiniGameManager>("map_minigame_manager", errors);
            var miniGameTerminals = UnityEngine.Object.FindObjectsByType<PHSFinalMiniGameTerminal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                miniGameTerminals.Length >= 3,
                "map_event_minigame_terminals_insufficient",
                errors);
            foreach (var terminal in miniGameTerminals)
            {
                Require(terminal.IsConfigured, $"map_minigame_pair_invalid terminal={terminal.name}", errors);
                Require(terminal.GetComponent<Collider>() != null,
                    $"map_minigame_collider_missing terminal={terminal.name}", errors);
                ValidateMiniGameIndicator(terminal, errors);
            }

            var localDebrisPortals = UnityEngine.Object.FindObjectsByType<ExteriorTestTeleportInteractable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                localDebrisPortals.Length == 2,
                $"map_debris_portal_pair_count_invalid actual={localDebrisPortals.Length}",
                errors);
            var portalNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var portal in localDebrisPortals)
            {
                Require(portal.isActiveAndEnabled, $"map_debris_portal_inactive portal={portal.name}", errors);
                Require(
                    portalNames.Add(portal.name),
                    $"map_debris_portal_name_duplicate portal={portal.name}",
                    errors);
                var serializedPortal = new SerializedObject(portal);
                RequireObject(
                    serializedPortal,
                    "destination",
                    $"map_debris_portal_destination_missing portal={portal.name}",
                    errors);
                Require(
                    serializedPortal.FindProperty("serverInteractionDistance")?.floatValue >= 0.5f,
                    $"map_debris_portal_distance_invalid portal={portal.name}",
                    errors);
            }

            ValidateNetworkScenePortal(
                "map_shop_entry",
                "PHS_ExteriorShopPortal_0717",
                ShopSceneName,
                ShopSceneTransitionMode.RequireShopPhase,
                errors);

            FindOne<WarpChargeDebugInput>("map_warp_charge_debug_input", errors);
            ValidateSceneSellZones("map", errors);

            var debrisServiceGravityArea = UnityEngine.Object.FindObjectsByType<NetworkPlayerGravityArea>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(area => area.name == "PHS_ServiceGravityArea");
            Require(debrisServiceGravityArea != null, "map_debris_service_gravity_area_missing", errors);
            if (debrisServiceGravityArea != null)
            {
                var serializedGravityArea = new SerializedObject(debrisServiceGravityArea);
                Require(
                    serializedGravityArea.FindProperty("gravityMode")?.enumValueIndex
                        == (int)NetworkPlayerGravityMode.ShipGravity,
                    "map_debris_service_gravity_mode_invalid expected=ShipGravity",
                    errors);
                Require(
                    serializedGravityArea.FindProperty("priority")?.intValue == 1000,
                    "map_debris_service_gravity_priority_invalid expected=1000",
                    errors);
            }

            var exteriorGravityArea = UnityEngine.Object.FindObjectsByType<NetworkPlayerGravityArea>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(area => area.name == "PHS_Exterior_ZeroGravityArea");
            Require(exteriorGravityArea != null, "map_exterior_gravity_area_missing", errors);
            if (exteriorGravityArea != null)
            {
                var serializedExteriorArea = new SerializedObject(exteriorGravityArea);
                Require(
                    serializedExteriorArea.FindProperty("gravityMode")?.enumValueIndex
                        == (int)NetworkPlayerGravityMode.Spacewalk,
                    "map_exterior_gravity_area_mode_invalid expected=Spacewalk",
                    errors);
            }

            var gravityZones = UnityEngine.Object.FindObjectsByType<GravityZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            ValidateGravityZone(
                gravityZones.FirstOrDefault(zone => zone.name == "PHS_Exterior_ZeroGravityArea"),
                LastJumpCrew.Common.GravityMode.Spacewalk,
                0,
                "map_exterior_gravity_zone",
                errors);
            ValidateGravityZone(
                gravityZones.FirstOrDefault(zone => zone.name == "PHS_ServiceGravityArea"),
                LastJumpCrew.Common.GravityMode.ShipGravity,
                1000,
                "map_debris_service_gravity_zone",
                errors);

            var enemyDeviceTargets = UnityEngine.Object.FindObjectsByType<EnemyDeviceTarget>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                enemyDeviceTargets.Length >= 4,
                $"map_enemy_device_targets_insufficient actual={enemyDeviceTargets.Length}",
                errors);
            foreach (var deviceTarget in enemyDeviceTargets)
            {
                var serializedDeviceTarget = new SerializedObject(deviceTarget);
                RequireObject(
                    serializedDeviceTarget,
                    "visualRoot",
                    $"map_enemy_device_visual_missing target={deviceTarget.name}",
                    errors);
                Require(
                    serializedDeviceTarget.FindProperty("destructionAccident")?.enumValueIndex
                        != (int)PHSShipAccidentId.None,
                    $"map_enemy_device_accident_missing target={deviceTarget.name}",
                    errors);
                Require(
                    !string.IsNullOrWhiteSpace(
                        serializedDeviceTarget.FindProperty("requestedAnchorId")?.stringValue),
                    $"map_enemy_device_anchor_missing target={deviceTarget.name}",
                    errors);
                Require(
                    deviceTarget.GetComponentInChildren<Collider>(true) != null,
                    $"map_enemy_device_collider_missing target={deviceTarget.name}",
                    errors);
            }

            foreach (var safeZone in UnityEngine.Object.FindObjectsByType<NetworkWarpSafeZone>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var safeTrigger = safeZone.GetComponent<BoxCollider>();
                Require(safeTrigger != null && safeTrigger.isTrigger, "map_warp_safe_trigger_invalid", errors);
            }

            var eventManager = FindOne<EventManager>("map_event_manager", errors);
            if (eventManager != null)
            {
                var serializedEventManager = new SerializedObject(eventManager);
                RequireObject(
                    serializedEventManager,
                    "registry",
                    "map_event_registry_missing",
                    errors);
                var registry = serializedEventManager.FindProperty("registry")?.objectReferenceValue
                    as EventRegistrySO;
                var powerOffData = registry == null ? null : registry.GetData(EventId.PowerOff);
                Require(
                    powerOffData != null &&
                    powerOffData.Id == EventId.PowerOff &&
                    powerOffData.Type == SM.EventType.Internal,
                    "map_event_power_off_data_missing_or_invalid",
                    errors);
            }

            var enemySpawnSetting = FindOne<EnemySpawnSetting>(
                "map_enemy_spawn_setting",
                errors);
            if (enemySpawnSetting != null)
            {
                var spawnGroups = new SerializedObject(enemySpawnSetting)
                    .FindProperty("spawnGroups");
                Require(
                    spawnGroups != null
                    && spawnGroups.isArray
                    && spawnGroups.arraySize > 0,
                    "map_enemy_spawn_groups_missing",
                    errors);
                if (spawnGroups != null && spawnGroups.isArray)
                {
                    for (var index = 0; index < spawnGroups.arraySize; index++)
                    {
                        RequireRelativeObject(
                            spawnGroups.GetArrayElementAtIndex(index),
                            "spawnPoint",
                            $"map_enemy_spawn_point_missing index={index}",
                            errors);
                    }
                }
            }

            var eventScheduler = FindOne<PHSNetworkEventScheduler>("map_event_scheduler", errors);
            if (eventScheduler != null)
            {
                var serializedScheduler = new SerializedObject(eventScheduler);
                var configuredEvents = CollectWeightedSchedulerEvents(serializedScheduler);

                foreach (var requiredEvent in new[]
                         {
                             EventId.Fire,
                             EventId.EnemySpawn,
                             EventId.OxygenLeak,
                             EventId.MeteorAttack,
                             EventId.EmpAttack,
                             EventId.EnemyScout
                         })
                {
                    Require(
                        configuredEvents.Contains(requiredEvent),
                        $"map_event_scheduler_pool_missing event={requiredEvent}",
                        errors);
                }
            }

            var roomRegistry = FindOne<RoomRegistry>("map_room_registry", errors);
            var eventCoordinator = FindOne<NetworkEventCoordinator>("map_event_network_coordinator", errors);
            if (eventCoordinator != null)
            {
                Require(
                    eventCoordinator.GetComponent<NetworkObject>() != null,
                    "map_event_network_object_missing",
                    errors);
                var serializedCoordinator = new SerializedObject(eventCoordinator);
                Require(
                    serializedCoordinator.FindProperty("startSchedulerOnServerSpawn")?.boolValue == false,
                    "map_event_scheduler_auto_start_must_be_disabled",
                    errors);
                RequireObject(
                    serializedCoordinator,
                    "eventManager",
                    "map_event_coordinator_manager_missing",
                    errors);
                RequireObject(
                    serializedCoordinator,
                    "eventScheduler",
                    "map_event_coordinator_scheduler_missing",
                    errors);
                RequireObject(
                    serializedCoordinator,
                    "roomRegistry",
                    "map_event_coordinator_room_registry_missing",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("eventManager")?.objectReferenceValue == eventManager,
                    "map_event_coordinator_manager_mismatch",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("eventScheduler")?.objectReferenceValue == eventScheduler,
                    "map_event_coordinator_scheduler_mismatch",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("roomRegistry")?.objectReferenceValue == roomRegistry,
                    "map_event_coordinator_room_registry_mismatch",
                    errors);
                RequireObject(
                    serializedCoordinator,
                    "effectMirrorPresenter",
                    "map_event_effect_mirror_presenter_missing",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("fireHullDamagePerEffect")?.intValue > 0,
                    "map_event_fire_ship_impact_invalid",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("oxygenLifeSupportDamagePerEffect")?.intValue > 0,
                    "map_event_oxygen_ship_impact_invalid",
                    errors);
                Require(
                    serializedCoordinator.FindProperty("enemyEngineDamagePerEffect")?.intValue > 0,
                    "map_event_enemy_ship_impact_invalid",
                    errors);
            }

            ValidateMapRuntimeContext(mapCatalog, eventScheduler, eventCoordinator, errors);
            ValidateShipAccidentRuntime(mapCatalog, errors);

            var effectMirrorPresenter = FindOne<NetworkEventEffectMirrorPresenter>(
                "map_event_effect_mirror_presenter",
                errors);
            if (effectMirrorPresenter != null)
            {
                var serializedPresenter = new SerializedObject(effectMirrorPresenter);
                RequireObject(serializedPresenter, "firePresentationPrefab", "map_fire_presentation_missing", errors);
                RequireObject(serializedPresenter, "oxygenLeakPresentationPrefab", "map_oxygen_presentation_missing", errors);
                RequireObject(serializedPresenter, "playerAttackEnemyPresentationPrefab", "map_player_enemy_presentation_missing", errors);
                RequireObject(serializedPresenter, "deviceAttackEnemyPresentationPrefab", "map_device_enemy_presentation_missing", errors);
                RequireObject(serializedPresenter, "presentationRoot", "map_event_presentation_root_missing", errors);
            }

            Require(
                FindAllMonoBehaviours().Count(component => component is IRoom) >= 4,
                "map_rooms_insufficient expected=4",
                errors);

            ValidateEventHudWiring(
                "map",
                FindOne<PHSNetworkEventHudView>("map_event_hud_view", errors),
                FindOne<PHSNetworkEventHudBinder>("map_event_hud_binder", errors),
                FindOne<PHSShipSystemsHudBinder>("map_ship_systems_hud_binder", errors),
                errors);
            ValidatePartyCreditsWiring(
                "map",
                FindOne<PartyCreditsHudBinder>("map_party_credits_hud_binder", errors),
                errors);
            ValidatePurchaseDeliveryBox(errors);

            ValidateShipPowerWiring(errors);
        }

        private static void ValidateFeatureInspectionScene(
            ICollection<string> errors)
        {
            OpenAndValidateScene(FeatureInspectionScenePath, errors);
            Require(
                PHS0719IncidentLocationAuthoring.ValidateAuthoredScene(
                    out var incidentLocationReason),
                $"feature_incident_location_authoring_invalid " +
                $"reason={incidentLocationReason}",
                errors);
            ValidateNoSceneOwnedStageClock("feature", errors);
            ValidateNoSceneOwnedEconomyLedger("feature", errors);
            ValidateNoSceneOwnedRandomLedger("feature", errors);
            ValidateNoSceneOwnedIncidentRootComponents("feature", errors);

            var mapRuntime = FindOne<PHSMapRuntimeContext>(
                "feature_runtime_context",
                errors);
            var eventCoordinator = FindOne<NetworkEventCoordinator>(
                "feature_event_coordinator",
                errors);
            var consumer = FindOne<PHSMapIncidentCommandConsumer>(
                "feature_incident_consumer",
                errors);
            var gateway = mapRuntime == null
                ? null
                : mapRuntime.GetComponent<PHSIncidentRequestGateway>();
            if (mapRuntime != null && consumer != null)
            {
                var serializedRuntime = new SerializedObject(mapRuntime);
                Require(
                    serializedRuntime.FindProperty("incidentCommandConsumer")
                        ?.objectReferenceValue == consumer,
                    "feature_runtime_incident_consumer_reference_mismatch",
                    errors);
                Require(
                    consumer.gameObject == mapRuntime.gameObject,
                    "feature_incident_consumer_owner_invalid",
                    errors);
                Require(
                    consumer.enabled && consumer.IncidentLayout != null,
                    "feature_incident_consumer_not_ready",
                    errors);
                Require(
                    !consumer.AllowLegacyLocationFallback,
                    "feature_incident_legacy_location_fallback_must_be_false",
                    errors);
                Require(
                    eventCoordinator == null
                    || consumer.EventCoordinator == eventCoordinator,
                    "feature_incident_event_coordinator_mismatch",
                    errors);

                var selectors = UnityEngine.Object
                    .FindObjectsByType<PHSIncidentConsequenceSelector>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                Require(
                    selectors.Length == 1,
                    $"feature_incident_consequence_selector_count_invalid " +
                    $"expected=1 actual={selectors.Length}",
                    errors);
                if (selectors.Length == 1)
                {
                    var selector = selectors[0];
                    var serializedConsumer = new SerializedObject(consumer);
                    Require(
                        selector.gameObject == mapRuntime.gameObject,
                        "feature_incident_consequence_selector_owner_invalid",
                        errors);
                    Require(
                        serializedConsumer.FindProperty("consequenceSelector")
                            ?.objectReferenceValue == selector,
                        "feature_incident_consequence_selector_reference_mismatch",
                        errors);
                    Require(
                        selector.RequestGateway == gateway,
                        "feature_incident_consequence_gateway_mismatch",
                        errors);
                    Require(
                        selector.AccidentCoordinator
                            == consumer.AccidentCoordinator,
                        "feature_incident_consequence_accident_coordinator_mismatch",
                        errors);
                }
            }

            var terminalEventIds = new HashSet<EventId>
            {
                EventId.EnemyScout,
                EventId.MeteorAttack,
                EventId.EmpAttack
            };
            var configuredTerminalEventIds = new HashSet<EventId>();
            var buttons = UnityEngine.Object
                .FindObjectsByType<PHSFeatureInspectionTriggerButton>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                var serializedButton = new SerializedObject(button);
                if (serializedButton.FindProperty("triggerKind")?.enumValueIndex
                        != (int)PHSFeatureInspectionTriggerKind.NetworkEvent)
                {
                    continue;
                }

                var eventId = (EventId)(serializedButton
                    .FindProperty("networkEventId")?.intValue ?? 0);
                if (!terminalEventIds.Contains(eventId))
                {
                    continue;
                }

                Require(
                    configuredTerminalEventIds.Add(eventId),
                    $"feature_terminal_event_button_duplicate event={eventId}",
                    errors);
                Require(
                    gateway != null
                    && serializedButton.FindProperty("incidentGateway")
                        ?.objectReferenceValue == gateway,
                    $"feature_terminal_event_gateway_mismatch event={eventId}",
                    errors);
                Require(
                    consumer != null
                    && serializedButton.FindProperty("incidentLayout")
                        ?.objectReferenceValue == consumer.IncidentLayout,
                    $"feature_terminal_event_layout_mismatch event={eventId}",
                    errors);

                var room = serializedButton.FindProperty("networkEventRoom")
                    ?.objectReferenceValue as ShipRoom;
                Require(
                    room == null,
                    $"feature_terminal_event_target_must_be_automatic event={eventId}",
                    errors);
            }

            Require(
                terminalEventIds.SetEquals(configuredTerminalEventIds),
                $"feature_terminal_event_buttons_invalid expected=3 " +
                $"actual={configuredTerminalEventIds.Count}",
                errors);
        }

        private static void ValidateShopScene(ICollection<string> errors)
        {
            OpenAndValidateScene(ShopScenePath, errors);
            ValidateNoSceneOwnedStageClock("shop", errors);
            ValidateNoSceneOwnedEconomyLedger("shop", errors);
            ValidateNoSceneOwnedRandomLedger("shop", errors);
            ValidateNoSceneOwnedIncidentRootComponents("shop", errors);
            ValidateNoLegacyEconomyOwner("shop", errors);
            ValidateGameplayContext("shop", errors);
            ValidateNetworkScenePortal(
                "shop_return",
                "PHS_ReturnToShipPortal",
                MapSceneName,
                ShopSceneTransitionMode.CompleteShop,
                errors);

            var displayController = FindOne<ShopRandomDisplayController>("shop_display_controller", errors);
            if (displayController != null)
            {
                var serializedDisplay = new SerializedObject(displayController);
                RequireArray(serializedDisplay, "displaySlots", 8, "shop_display_slots_insufficient", errors);
                Require(
                    serializedDisplay.FindProperty("displaySlots")?.arraySize == 12,
                    "shop_display_slots_invalid expected=12",
                    errors);
                Require(
                    serializedDisplay.FindProperty("minimumDisplayCount")?.intValue == 8,
                    "shop_minimum_display_count_invalid expected=8",
                    errors);
                Require(
                    serializedDisplay.FindProperty("maximumDisplayCount")?.intValue == 10,
                    "shop_maximum_display_count_invalid expected=10",
                    errors);
                Require(
                    serializedDisplay.FindProperty("allowDuplicateProducts")?.boolValue == false,
                    "shop_duplicate_products_must_be_disabled",
                    errors);
            }

            var purchaseService = FindOne<ShopPurchaseService>("shop_purchase_service", errors);
            if (purchaseService != null)
            {
                var serializedPurchase = new SerializedObject(purchaseService);
                RequireObject(serializedPurchase, "catalog", "shop_catalog_missing", errors);
                RequireObject(serializedPurchase, "walletSource", "shop_wallet_source_missing", errors);
                RequireObject(serializedPurchase, "deliverySource", "shop_delivery_source_missing", errors);
                var deliverySource = serializedPurchase
                    .FindProperty("deliverySource")
                    ?.objectReferenceValue as MonoBehaviour;
                Require(
                    deliverySource is IShopPurchaseTransactionService,
                    "shop_purchase_transaction_service_missing",
                    errors);
            }

            var checkoutZone = FindOne<ShopCheckoutZone>("shop_trade_station_checkout_zone", errors);
            if (checkoutZone != null)
            {
                var stationRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(checkoutZone.gameObject);
                Require(
                    stationRoot != null &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(stationRoot) == TradeStationPrefabPath,
                    "shop_trade_station_prefab_invalid",
                    errors);
                var serializedZone = new SerializedObject(checkoutZone);
                RequireObject(serializedZone, "checkoutTrigger", "shop_trade_station_trigger_missing", errors);
                RequireObject(serializedZone, "priceText", "shop_trade_station_price_text_missing", errors);
                RequireObject(serializedZone, "catalog", "shop_trade_station_catalog_missing", errors);
                RequireObject(
                    serializedZone,
                    "purchaseServiceSource",
                    "shop_trade_station_purchase_service_missing",
                    errors);
            }

            var checkoutButton = FindOne<ShopCheckoutButtonInteractable>(
                "shop_trade_station_button",
                errors);
            if (checkoutButton != null)
            {
                var serializedButton = new SerializedObject(checkoutButton);
                RequireObject(serializedButton, "checkoutZone", "shop_trade_station_button_zone_missing", errors);
                RequireObject(serializedButton, "pressVisual", "shop_trade_station_press_visual_missing", errors);
            }

            var pressVisual = FindOne<ShopCheckoutButtonPressVisual>(
                "shop_trade_station_press_visual",
                errors);
            if (pressVisual != null)
            {
                var serializedVisual = new SerializedObject(pressVisual);
                var buttonVisual = serializedVisual.FindProperty("buttonVisual")?.objectReferenceValue as Transform;
                Require(buttonVisual != null, "shop_trade_station_cylinder_missing", errors);
                Require(
                    buttonVisual != null && buttonVisual.name == "Cylinder" &&
                    buttonVisual.parent != null && buttonVisual.parent.name == "Cylinder" &&
                    buttonVisual.parent.parent == pressVisual.transform,
                    "shop_trade_station_cylinder_hierarchy_invalid",
                    errors);
                RequireObject(
                    serializedVisual,
                    "buttonRenderer",
                    "shop_trade_station_cylinder_renderer_missing",
                    errors);
            }

            Require(
                UnityEngine.Object.FindObjectsByType<CheckoutDetector>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length == 0,
                "shop_legacy_checkout_detector_present",
                errors);
            Require(
                UnityEngine.Object.FindObjectsByType<CheckoutButton>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length == 0,
                "shop_legacy_checkout_button_present",
                errors);

            var shelfSlots = UnityEngine.Object.FindObjectsByType<ShopDisplaySlot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(shelfSlots.Length == 12, "shop_display_slots_invalid expected=12", errors);
            foreach (var slot in shelfSlots)
            {
                Require(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(slot.gameObject) ==
                    ShopDisplayDeskPrefabPath,
                    $"shop_display_slot_prefab_invalid slot={slot.name}",
                    errors);
                RequireObject(
                    new SerializedObject(slot),
                    "itemSpawnPoint",
                    $"shop_display_spawn_point_missing slot={slot.name}",
                    errors);
            }

            ValidateShopCatalog(errors);
            ValidatePartyCreditsWiring(
                "shop",
                FindOne<PartyCreditsHudBinder>("shop_party_credits_hud_binder", errors),
                errors);
        }

        private static void ValidateShopPresentationPrefabs(ICollection<string> errors)
        {
            ValidateTradeStationPrefab(errors);
            ValidateShopDisplayDeskPrefab(errors);
        }

        private static void ValidateTradeStationPrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(TradeStationPrefabPath);
            if (prefab == null)
            {
                errors.Add($"trade_station_prefab_missing path={TradeStationPrefabPath}");
                return;
            }

            try
            {
                Require(prefab.name == "TradeStation", "trade_station_root_name_invalid", errors);
                Require(
                    prefab.GetComponentsInChildren<CheckoutDetector>(true).Length == 0,
                    "trade_station_legacy_detector_present",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<CheckoutButton>(true).Length == 0,
                    "trade_station_legacy_button_present",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<ShopCheckoutZone>(true).Length == 1,
                    "trade_station_checkout_zone_count_invalid expected=1",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<ShopCheckoutButtonInteractable>(true).Length == 1,
                    "trade_station_button_count_invalid expected=1",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<ShopCheckoutButtonPressVisual>(true).Length == 1,
                    "trade_station_press_visual_count_invalid expected=1",
                    errors);
                var cylinder = prefab.transform.Find("Button/Cylinder");
                Require(cylinder != null, "trade_station_button_cylinder_missing", errors);
                Require(
                    cylinder != null && cylinder.GetComponent<Renderer>() != null,
                    "trade_station_button_cylinder_renderer_missing",
                    errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateShopDisplayDeskPrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(ShopDisplayDeskPrefabPath);
            if (prefab == null)
            {
                errors.Add($"shop_display_desk_prefab_missing path={ShopDisplayDeskPrefabPath}");
                return;
            }

            try
            {
                Require(
                    prefab.name == "PHS_ShopDisplayDesk_Shared",
                    "shop_display_desk_root_name_invalid",
                    errors);
                var slots = prefab.GetComponentsInChildren<ShopDisplaySlot>(true);
                Require(slots.Length == 2, "shop_display_desk_slots_invalid expected=2", errors);
                foreach (var slot in slots)
                {
                    RequireObject(
                        new SerializedObject(slot),
                        "itemSpawnPoint",
                        $"shop_display_desk_spawn_point_missing slot={slot.name}",
                        errors);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateMapCatalog(
            string label,
            PHSMapCatalogSO mapCatalog,
            int minimumSelectableProfiles,
            ICollection<string> errors)
        {
            Require(mapCatalog != null, $"{label}_map_catalog_missing", errors);
            if (mapCatalog == null)
            {
                return;
            }

            Require(
                mapCatalog.TryValidate(out var reason),
                $"{label}_map_catalog_invalid detail={reason}",
                errors);
            Require(
                mapCatalog.Profiles.Count >= minimumSelectableProfiles,
                $"{label}_map_catalog_profiles_insufficient actual={mapCatalog.Profiles.Count}",
                errors);
            Require(
                mapCatalog.Profiles.Count(profile => profile != null && profile.Selectable)
                >= minimumSelectableProfiles,
                $"{label}_map_catalog_selectable_profiles_insufficient",
                errors);
            ValidateIncidentMapProfiles(label, mapCatalog, errors);
        }

        private static void ValidateIncidentMapProfiles(
            string label,
            PHSMapCatalogSO mapCatalog,
            ICollection<string> errors)
        {
            foreach (var mapId in IncidentGameplayMapIds)
            {
                var matches = mapCatalog.Profiles
                    .Where(profile => profile != null && profile.MapId == mapId)
                    .ToArray();
                Require(
                    matches.Length == 1,
                    $"{label}_incident_profile_count_invalid " +
                    $"map={mapId} expected=1 actual={matches.Length}",
                    errors);
                if (matches.Length != 1)
                {
                    continue;
                }

                var profile = matches[0];
                Require(
                    profile.IncidentPressureCapacity == 3,
                    $"{label}_incident_pressure_invalid " +
                    $"map={mapId} expected=3 " +
                    $"actual={profile.IncidentPressureCapacity}",
                    errors);
                Require(
                    profile.MaximumActiveExternalThreats == 1,
                    $"{label}_incident_external_cap_invalid " +
                    $"map={mapId} expected=1 " +
                    $"actual={profile.MaximumActiveExternalThreats}",
                    errors);
                Require(
                    profile.MaximumActiveInternalAccidents == 2,
                    $"{label}_incident_internal_cap_invalid " +
                    $"map={mapId} expected=2 " +
                    $"actual={profile.MaximumActiveInternalAccidents}",
                    errors);
            }
        }

        private static HashSet<EventId> CollectWeightedSchedulerEvents(SerializedObject serializedScheduler)
        {
            var configuredEvents = new HashSet<EventId>();
            var weightedEvents = serializedScheduler.FindProperty("weightedEvents");
            if (weightedEvents == null || !weightedEvents.isArray)
            {
                return configuredEvents;
            }

            for (var index = 0; index < weightedEvents.arraySize; index++)
            {
                var entry = weightedEvents.GetArrayElementAtIndex(index);
                var eventId = entry.FindPropertyRelative("eventId");
                if (eventId == null)
                {
                    continue;
                }

                if (eventId.propertyType == SerializedPropertyType.Enum
                    && eventId.enumValueIndex >= 0
                    && eventId.enumValueIndex < eventId.enumNames.Length
                    && Enum.TryParse<EventId>(eventId.enumNames[eventId.enumValueIndex], out var parsedEventId))
                {
                    configuredEvents.Add(parsedEventId);
                    continue;
                }

                configuredEvents.Add((EventId)eventId.intValue);
            }

            return configuredEvents;
        }

        private static void ValidateMapRuntimeContext(
            PHSMapCatalogSO mapCatalog,
            PHSNetworkEventScheduler eventScheduler,
            NetworkEventCoordinator eventCoordinator,
            ICollection<string> errors)
        {
            var mapRuntime = FindOne<PHSMapRuntimeContext>("map_runtime_context", errors);
            if (mapRuntime == null)
            {
                return;
            }

            Require(
                mapRuntime.enabled,
                "map_runtime_context_disabled",
                errors);
            var serializedRuntime = new SerializedObject(mapRuntime);
            RequireObject(serializedRuntime, "mapCatalog", "map_runtime_catalog_missing", errors);
            RequireObject(serializedRuntime, "environmentRoot", "map_runtime_environment_root_missing", errors);
            RequireObject(serializedRuntime, "warpTransitionPresenter", "map_runtime_warp_presenter_missing", errors);
            RequireObject(serializedRuntime, "warpMaintenanceProfile", "map_runtime_warp_maintenance_profile_missing", errors);
            RequireObject(serializedRuntime, "shopPortalProfile", "map_runtime_shop_portal_profile_missing", errors);
            RequireObject(serializedRuntime, "shopPortalRoot", "map_runtime_shop_portal_root_missing", errors);
            RequireObject(serializedRuntime, "debrisStream", "map_runtime_debris_stream_missing", errors);
            var debrisStream =
                serializedRuntime.FindProperty("debrisStream")?.objectReferenceValue as PHSRandomDebrisStream;
            if (debrisStream != null)
            {
                ValidateMapDebrisPhysicsReferences(debrisStream, errors);
            }

            RequireObject(serializedRuntime, "externalThreatScheduler", "map_runtime_external_scheduler_missing", errors);
            RequireObject(serializedRuntime, "internalAccidentCoordinator", "map_runtime_internal_accident_missing", errors);
            RequireObject(
                serializedRuntime,
                "incidentCommandConsumer",
                "map_runtime_incident_consumer_missing",
                errors);
            Require(
                mapCatalog == null
                || serializedRuntime.FindProperty("mapCatalog")?.objectReferenceValue == mapCatalog,
                "map_runtime_catalog_mismatch",
                errors);
            Require(
                eventScheduler == null
                || serializedRuntime.FindProperty("externalThreatScheduler")?.objectReferenceValue == eventScheduler,
                "map_runtime_external_scheduler_mismatch",
                errors);
            Require(
                eventCoordinator == null
                || serializedRuntime.FindProperty("externalThreatScheduler")?.objectReferenceValue ==
                eventCoordinator.GetComponent<PHSNetworkEventScheduler>(),
                "map_runtime_event_coordinator_scheduler_mismatch",
                errors);
            ValidateMapIncidentCommandConsumer(
                mapRuntime,
                eventCoordinator,
                serializedRuntime.FindProperty("incidentCommandConsumer")
                    ?.objectReferenceValue as PHSMapIncidentCommandConsumer,
                errors);
            var shopPortalRoot =
                serializedRuntime.FindProperty("shopPortalRoot")?.objectReferenceValue as GameObject;
            Require(
                shopPortalRoot != null &&
                shopPortalRoot.GetComponent<NetworkScenePortalInteractable>() != null,
                "map_runtime_shop_portal_component_missing",
                errors);
        }

        private static void ValidateMapIncidentCommandConsumer(
            PHSMapRuntimeContext mapRuntime,
            NetworkEventCoordinator eventCoordinator,
            PHSMapIncidentCommandConsumer configuredConsumer,
            ICollection<string> errors)
        {
            var consumers =
                UnityEngine.Object.FindObjectsByType<PHSMapIncidentCommandConsumer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                consumers.Length == 1,
                $"map_incident_consumer_count_invalid expected=1 " +
                $"actual={consumers.Length}",
                errors);
            if (consumers.Length != 1)
            {
                return;
            }

            var consumer = consumers[0];
            Require(
                consumer.gameObject == mapRuntime.gameObject,
                "map_incident_consumer_owner_invalid expected=map_runtime_context",
                errors);
            Require(
                configuredConsumer == consumer,
                "map_runtime_incident_consumer_reference_mismatch",
                errors);
            Require(
                consumer.enabled,
                "map_incident_consumer_disabled",
                errors);
            Require(
                consumer.IncidentLayout != null,
                "map_incident_layout_reference_missing",
                errors);
            Require(
                !consumer.AllowLegacyLocationFallback,
                "map_incident_legacy_location_fallback_must_be_false",
                errors);

            var accidentCoordinators =
                UnityEngine.Object.FindObjectsByType<PHSNetworkShipAccidentCoordinator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                accidentCoordinators.Length == 1,
                $"map_incident_accident_coordinator_count_invalid expected=1 " +
                $"actual={accidentCoordinators.Length}",
                errors);
            var expectedAccidentCoordinator = accidentCoordinators.Length == 1
                ? accidentCoordinators[0]
                : null;
            var sceneRooms = UnityEngine.Object.FindObjectsByType<ShipRoom>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                .ToArray();
            Require(
                sceneRooms.Length > 0,
                "map_incident_rooms_empty",
                errors);
            Require(
                sceneRooms.All(room => !string.IsNullOrWhiteSpace(room.RoomId)),
                "map_incident_room_id_missing",
                errors);
            Require(
                sceneRooms.Select(room => room.RoomId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == sceneRooms.Length,
                "map_incident_room_id_duplicate",
                errors);

            var serializedConsumer = new SerializedObject(consumer);
            Require(
                eventCoordinator == null
                || serializedConsumer.FindProperty("eventCoordinator")
                    ?.objectReferenceValue == eventCoordinator,
                "map_incident_event_coordinator_mismatch",
                errors);
            Require(
                expectedAccidentCoordinator == null
                || serializedConsumer.FindProperty("accidentCoordinator")
                    ?.objectReferenceValue == expectedAccidentCoordinator,
                "map_incident_accident_coordinator_mismatch",
                errors);
            var selectors =
                UnityEngine.Object.FindObjectsByType<PHSIncidentConsequenceSelector>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                selectors.Length == 1,
                $"map_incident_consequence_selector_count_invalid " +
                $"expected=1 actual={selectors.Length}",
                errors);
            if (selectors.Length == 1)
            {
                var selector = selectors[0];
                Require(
                    selector.gameObject == mapRuntime.gameObject,
                    "map_incident_consequence_selector_owner_invalid",
                    errors);
                Require(
                    serializedConsumer.FindProperty("consequenceSelector")
                        ?.objectReferenceValue == selector,
                    "map_incident_consequence_selector_reference_mismatch",
                    errors);
                Require(
                    selector.RequestGateway
                        == mapRuntime.GetComponent<PHSIncidentRequestGateway>(),
                    "map_incident_consequence_gateway_mismatch",
                    errors);
                Require(
                    expectedAccidentCoordinator == null
                    || selector.AccidentCoordinator
                        == expectedAccidentCoordinator,
                    "map_incident_consequence_accident_coordinator_mismatch",
                    errors);
            }
            var configuredRooms = serializedConsumer.FindProperty("rooms");
            Require(
                configuredRooms != null
                && configuredRooms.isArray
                && configuredRooms.arraySize == sceneRooms.Length,
                $"map_incident_room_count_invalid expected={sceneRooms.Length} " +
                $"actual={(configuredRooms != null && configuredRooms.isArray ? configuredRooms.arraySize : -1)}",
                errors);
            if (configuredRooms == null
                || !configuredRooms.isArray
                || configuredRooms.arraySize != sceneRooms.Length)
            {
                return;
            }

            for (var index = 0; index < sceneRooms.Length; index++)
            {
                Require(
                    configuredRooms
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue == sceneRooms[index],
                    $"map_incident_room_order_invalid index={index} " +
                    $"expected={sceneRooms[index].RoomId}",
                    errors);
            }
        }

        private static void ValidateMiniGameIndicator(
            PHSFinalMiniGameTerminal terminal,
            ICollection<string> errors)
        {
            var indicator = terminal.GetComponent<MiniGameEventStatusIndicator>();
            Require(
                indicator != null,
                $"map_minigame_indicator_missing terminal={terminal.name}",
                errors);
            if (indicator == null)
            {
                return;
            }

            var serializedIndicator = new SerializedObject(indicator);
            Require(
                serializedIndicator.FindProperty("terminalSource")?.objectReferenceValue == terminal,
                $"map_minigame_indicator_terminal_invalid terminal={terminal.name}",
                errors);

            var visualSlots = serializedIndicator.FindProperty("visualSlots");
            Require(
                visualSlots != null && visualSlots.isArray && visualSlots.arraySize > 0,
                $"map_minigame_indicator_slots_missing terminal={terminal.name}",
                errors);
            if (visualSlots == null || !visualSlots.isArray)
            {
                return;
            }

            for (var index = 0; index < visualSlots.arraySize; index++)
            {
                var slot = visualSlots.GetArrayElementAtIndex(index);
                var statusLight =
                    slot.FindPropertyRelative("statusLight")?.objectReferenceValue as Light;
                var emissiveRenderer =
                    slot.FindPropertyRelative("emissiveRenderer")?.objectReferenceValue as Renderer;
                Require(
                    statusLight != null || emissiveRenderer != null,
                    $"map_minigame_indicator_visual_missing terminal={terminal.name} slot={index}",
                    errors);
                if (emissiveRenderer == null)
                {
                    continue;
                }

                var materialIndex =
                    slot.FindPropertyRelative("materialIndex")?.intValue ?? -1;
                var materials = emissiveRenderer.sharedMaterials;
                Require(
                    materialIndex >= 0
                    && materialIndex < materials.Length
                    && materials[materialIndex] != null,
                    $"map_minigame_indicator_material_invalid terminal={terminal.name} slot={index} index={materialIndex}",
                    errors);
                if (materialIndex < 0
                    || materialIndex >= materials.Length
                    || materials[materialIndex] == null)
                {
                    continue;
                }

                var material = materials[materialIndex];
                Require(
                    material.shader != null && material.shader.isSupported,
                    $"map_minigame_indicator_shader_invalid terminal={terminal.name} slot={index}",
                    errors);
                Require(
                    material.HasProperty("_BaseColor")
                    && material.HasProperty("_EmissionColor"),
                    $"map_minigame_indicator_properties_invalid terminal={terminal.name} slot={index} material={material.name}",
                    errors);
                Require(
                    material.IsKeywordEnabled("_EMISSION"),
                    $"map_minigame_indicator_emission_disabled terminal={terminal.name} slot={index} material={material.name}",
                    errors);
            }
        }

        private static void ValidateMapDebrisPhysicsReferences(
            PHSRandomDebrisStream debrisStream,
            ICollection<string> errors)
        {
            var serializedStream = new SerializedObject(debrisStream);
            var debrisRoots = serializedStream.FindProperty("debrisRoots");
            Require(
                debrisRoots != null && debrisRoots.isArray && debrisRoots.arraySize > 0,
                "map_debris_stream_roots_missing",
                errors);
            if (debrisRoots == null || !debrisRoots.isArray)
            {
                return;
            }

            for (var index = 0; index < debrisRoots.arraySize; index++)
            {
                var seed =
                    debrisRoots.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                Require(
                    seed != null,
                    $"map_debris_stream_seed_missing index={index}",
                    errors);
                if (seed == null)
                {
                    continue;
                }

                var droppedPrefab =
                    seed.GetComponent<UtilityItemObject>()?.ItemPrefabData?.DroppedPrefab;
                var droppedPath = AssetDatabase.GetAssetPath(droppedPrefab);
                Require(
                    droppedPrefab != null,
                    $"map_debris_stream_dropped_prefab_missing seed={seed.name}",
                    errors);
                if (droppedPrefab == null)
                {
                    continue;
                }

                var physicsAuthority =
                    droppedPrefab.GetComponent<NetworkItemPhysicsAuthority>();
                Require(
                    physicsAuthority != null,
                    $"map_debris_stream_physics_authority_missing seed={seed.name} path={droppedPath}",
                    errors);
                if (physicsAuthority != null)
                {
                    RequireObject(
                        new SerializedObject(physicsAuthority),
                        "targetRigidbody",
                        $"map_debris_stream_physics_rigidbody_reference_missing seed={seed.name} path={droppedPath}",
                        errors);
                }
            }
        }

        private static void ValidateShipAccidentRuntime(
            PHSMapCatalogSO mapCatalog,
            ICollection<string> errors)
        {
            var coordinator = FindOne<PHSNetworkShipAccidentCoordinator>("map_ship_accident_coordinator", errors);
            var anchors = UnityEngine.Object.FindObjectsByType<PHSShipAccidentAnchor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(anchors.Length >= 5, $"map_ship_accident_anchors_insufficient actual={anchors.Length}", errors);

            if (coordinator != null)
            {
                Require(
                    coordinator.GetComponent<NetworkObject>() != null,
                    "map_ship_accident_network_object_missing",
                    errors);
                var serializedCoordinator = new SerializedObject(coordinator);
                var catalog = serializedCoordinator.FindProperty("accidentCatalog")?.objectReferenceValue
                    as PHSShipAccidentCatalogSO;
                Require(catalog != null, "map_ship_accident_catalog_missing", errors);
                if (catalog != null)
                {
                    Require(
                        catalog.TryValidate(out var catalogReason),
                        $"map_ship_accident_catalog_invalid detail={catalogReason}",
                        errors);
                }

                RequireArray(serializedCoordinator, "anchors", 5, "map_ship_accident_registered_anchors_insufficient", errors);
            }

            Require(
                anchors.Any(anchor => anchor != null
                    && anchor.AnchorId == "gravity_generator"
                    && anchor.ModuleId == NetworkShipModuleId.Gravity),
                "map_gravity_generator_anchor_missing",
                errors);

            if (mapCatalog == null)
            {
                return;
            }

            foreach (var profile in mapCatalog.Profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                foreach (var entry in profile.InternalAccidentWeights)
                {
                    if (entry?.Definition == null)
                    {
                        continue;
                    }

                    Require(
                        anchors.Any(anchor => anchor != null && anchor.Supports(entry.Definition)),
                        $"map_ship_accident_anchor_missing map={profile.MapId} accident={entry.Definition.Id}",
                        errors);
                }
            }
        }

        private static void ValidateGravityDuplicateComponents(
            string sceneLabel,
            ICollection<string> errors)
        {
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                foreach (var componentType in GravityDuplicateGuardTypes)
                {
                    var count = transform.GetComponents(componentType).Length;
                    Require(
                        count <= 1,
                        $"{sceneLabel}_gravity_duplicate_component object={GetHierarchyPath(transform)} type={componentType.Name} count={count}",
                        errors);
                }
            }
        }

        private static void ValidateSellStationPrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(SellStationPrefabPath);
            if (prefab == null)
            {
                errors.Add($"sell_station_prefab_missing path={SellStationPrefabPath}");
                return;
            }

            try
            {
                var sellZone = prefab.GetComponentInChildren<DebrisSellZone>(true);
                Require(sellZone != null, "sell_station_zone_missing", errors);
                if (sellZone == null)
                {
                    return;
                }

                Require(sellZone.GetComponent<NetworkObject>() != null, "sell_station_network_object_missing", errors);
                var serializedZone = new SerializedObject(sellZone);
                RequireObject(serializedZone, "sellTrigger", "sell_station_trigger_missing", errors);
                RequireArray(serializedZone, "sellableDebris", 5, "sell_station_debris_catalog_insufficient", errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidatePlayerPrefab(ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Require(prefab != null, $"player_prefab_missing path={PlayerPrefabPath}", errors);
            if (prefab == null)
            {
                return;
            }

            Require(prefab.GetComponent<NetworkObject>() != null, "player_network_object_missing", errors);
            Require(prefab.GetComponent<NetworkPlayerController>() != null, "player_controller_missing", errors);
            var playerLifeState = prefab.GetComponent<NetworkPlayerLifeState>();
            Require(playerLifeState != null, "player_life_state_missing", errors);
            var playerUpgradeState = prefab.GetComponent<NetworkPlayerUpgradeState>();
            Require(playerUpgradeState != null, "player_upgrade_state_missing", errors);
            if (playerUpgradeState != null)
            {
                var serializedUpgradeState = new SerializedObject(playerUpgradeState);
                RequireObject(
                    serializedUpgradeState,
                    "playerLifeState",
                    "player_upgrade_life_state_missing",
                    errors);
                Require(
                    serializedUpgradeState.FindProperty("playerLifeState")?.objectReferenceValue
                        == playerLifeState,
                    "player_upgrade_life_state_mismatch",
                    errors);
            }
            Require(
                prefab.GetComponent<PlayerEnemyTargetRegistration>() != null,
                "player_enemy_target_registration_missing",
                errors);
            var gravityReceiver = prefab.GetComponent<PlayerGravityReceiver>();
            Require(gravityReceiver != null, "player_gravity_receiver_missing", errors);
            if (gravityReceiver != null)
            {
                var serializedGravityReceiver = new SerializedObject(gravityReceiver);
                Require(
                    serializedGravityReceiver.FindProperty("defaultGravityMode")?.enumValueIndex
                        == (int)LastJumpCrew.Common.GravityMode.ShipGravity,
                    "player_default_gravity_mode_invalid expected=ShipGravity",
                    errors);
            }
            var combatController = prefab.GetComponent<NetworkPlayerCombatController>();
            Require(combatController != null, "player_combat_controller_missing", errors);
            if (combatController != null)
            {
                var serializedCombat = new SerializedObject(combatController);
                RequireObject(serializedCombat, "wrenchUseEffect", "player_wrench_feedback_missing", errors);
                RequireObject(serializedCombat, "batteryUseEffect", "player_battery_feedback_missing", errors);
                RequireObject(
                    serializedCombat,
                    "extinguisherSprayEffectRoot",
                    "player_extinguisher_feedback_missing",
                    errors);
            }
            Require(
                prefab.GetComponent<NetworkPlayerKnockbackReceiver>() != null,
                "player_knockback_receiver_missing",
                errors);
            var statusEffectController =
                prefab.GetComponent<StatusEffectController>();
            Require(
                statusEffectController != null,
                "player_status_effect_receiver_missing",
                errors);
            if (statusEffectController != null)
            {
                var serializedStatus =
                    new SerializedObject(statusEffectController);
                RequireObject(
                    serializedStatus,
                    "electricShockEffectRoot",
                    "player_electric_shock_effect_missing",
                    errors);
            }
            Require(prefab.GetComponent<NetworkPlayerItemRecord>() != null, "player_item_record_missing", errors);
            Require(prefab.GetComponent<TempPlayerItemHolder>() != null, "player_item_holder_missing", errors);
            var itemLifecycle = prefab.GetComponent<NetworkPlayerItemLifecycle>();
            Require(itemLifecycle != null, "player_item_lifecycle_missing", errors);
            if (itemLifecycle != null)
            {
                var serializedItemLifecycle = new SerializedObject(itemLifecycle);
                RequireObject(
                    serializedItemLifecycle,
                    "itemCatalog",
                    "player_item_lifecycle_catalog_missing",
                    errors);
            }
            Require(
                prefab.GetComponents<P0RuntimeValidationDriver>().Length == 1,
                "player_p0_validation_driver_count_invalid expected=1",
                errors);
            var validationDriver = prefab.GetComponent<P0RuntimeValidationDriver>();
            if (validationDriver != null)
            {
                var serializedDriver = new SerializedObject(validationDriver);
                RequireObject(
                    serializedDriver,
                    "validationBatteryItem",
                    "player_p0_validation_battery_missing",
                    errors);
                RequireObject(
                    serializedDriver,
                    "validationThrownItem",
                    "player_p0_validation_thrown_item_missing",
                    errors);
            }

            Require(
                prefab.GetComponent<NetworkRunFlowCoordinator>() == null,
                "player_run_flow_coordinator_must_be_session_owned",
                errors);
            Require(
                prefab.GetComponentsInChildren<NetworkRunStageClock>(true).Length == 0,
                "player_stage_clock_must_be_session_owned",
                errors);
            Require(
                prefab.GetComponentsInChildren<NetworkRunEconomyLedger>(true).Length == 0,
                "player_economy_ledger_must_be_session_owned",
                errors);
            Require(
                prefab.GetComponentsInChildren<NetworkRunRandomLedger>(true).Length == 0,
                "player_random_ledger_must_be_session_owned",
                errors);
            Require(
                prefab.GetComponentsInChildren<NetworkRunIncidentLedger>(true).Length == 0,
                "player_incident_ledger_must_be_session_owned",
                errors);
            Require(
                prefab.GetComponentsInChildren<PHSNetworkIncidentDirector>(true).Length == 0,
                "player_incident_director_must_be_session_owned",
                errors);

            ValidateCombatItemRoute(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_WrenchItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_Wrench_Held.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_Wrench_Dropped.prefab",
                "wrench",
                errors);
            ValidateCombatItemRoute(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FireExtinguisherItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_FireExtinguisher_Held.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_FireExtinguisher_Dropped.prefab",
                "fire_extinguisher",
                errors);
            ValidateCombatItemRoute(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_BatteryItemPrefabData.asset",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_BatteryPack_Held.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_BatteryPack_Dropped.prefab",
                "battery",
                errors);
        }

        private static void ValidateCombatItemRoute(
            string itemDataPath,
            string expectedHeldPath,
            string expectedDroppedPath,
            string label,
            ICollection<string> errors)
        {
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemPrefabData>(itemDataPath);
            Require(itemData != null, $"{label}_item_data_missing", errors);
            if (itemData == null)
            {
                return;
            }

            Require(
                AssetDatabase.GetAssetPath(itemData.HeldPrefab) == expectedHeldPath,
                $"{label}_held_prefab_invalid",
                errors);
            Require(
                AssetDatabase.GetAssetPath(itemData.DroppedPrefab) == expectedDroppedPath,
                $"{label}_dropped_prefab_invalid",
                errors);
        }

        private static void ValidateShipRuntimePrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(ShipRuntimePrefabPath);
            if (prefab == null)
            {
                errors.Add($"ship_runtime_prefab_missing path={ShipRuntimePrefabPath}");
                return;
            }

            try
            {
                Require(prefab.GetComponent<NetworkObject>() != null, "ship_runtime_network_object_missing", errors);
                Require(
                    prefab.GetComponent<NetworkShipSystemsState>() == null,
                    "ship_runtime_state_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponent<PHSShipEventImpactAdapter>() == null,
                    "ship_runtime_event_impact_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<NetworkRunStageClock>(true).Length == 0,
                    "ship_runtime_stage_clock_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<NetworkRunEconomyLedger>(true).Length == 0,
                    "ship_runtime_economy_ledger_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<NetworkRunRandomLedger>(true).Length == 0,
                    "ship_runtime_random_ledger_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<NetworkRunIncidentLedger>(true).Length == 0,
                    "ship_runtime_incident_ledger_must_be_session_owned",
                    errors);
                Require(
                    prefab.GetComponentsInChildren<PHSNetworkIncidentDirector>(true).Length == 0,
                    "ship_runtime_incident_director_must_be_session_owned",
                    errors);
                var coordinator = prefab.GetComponent<PHSNetworkShipAccidentCoordinator>();
                Require(coordinator != null, "ship_runtime_accident_coordinator_missing", errors);
                if (coordinator != null)
                {
                    var serializedCoordinator = new SerializedObject(coordinator);
                    RequireObject(
                        serializedCoordinator,
                        "accidentCatalog",
                        "ship_runtime_accident_catalog_missing",
                        errors);
                    RequireArray(
                        serializedCoordinator,
                        "anchors",
                        5,
                        "ship_runtime_accident_anchors_insufficient",
                        errors);
                }

                Require(
                    prefab.GetComponentsInChildren<PHSShipAccidentAnchor>(true).Length >= 5,
                    "ship_runtime_accident_anchor_children_insufficient",
                    errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateRunSessionRootPrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            if (prefab == null)
            {
                errors.Add($"run_session_root_prefab_missing path={RunSessionRootPrefabPath}");
                return;
            }

            try
            {
                Require(
                    prefab.GetComponents<NetworkRunSessionRoot>().Length == 1,
                    "run_session_root_component_count_invalid expected=1",
                    errors);
                Require(
                    prefab.GetComponents<NetworkObject>().Length == 1,
                    "run_session_root_network_object_count_invalid expected=1",
                    errors);
                var stageClocks = prefab.GetComponentsInChildren<NetworkRunStageClock>(true);
                Require(
                    stageClocks.Length == 1,
                    $"run_session_root_stage_clock_count_invalid expected=1 actual={stageClocks.Length}",
                    errors);
                var stageClock = prefab.GetComponent<NetworkRunStageClock>();
                Require(
                    stageClock != null,
                    "run_session_root_stage_clock_owner_invalid expected=root",
                    errors);
                var economyLedgers = prefab.GetComponentsInChildren<NetworkRunEconomyLedger>(true);
                Require(
                    economyLedgers.Length == 1,
                    $"run_session_root_economy_ledger_count_invalid expected=1 actual={economyLedgers.Length}",
                    errors);
                var economyLedger = prefab.GetComponent<NetworkRunEconomyLedger>();
                Require(
                    economyLedger != null,
                    "run_session_root_economy_ledger_owner_invalid expected=root",
                    errors);
                if (economyLedger != null)
                {
                    var serializedEconomy = new SerializedObject(economyLedger);
                    Require(
                        serializedEconomy.FindProperty("startingCredits")?.intValue >= 0,
                        "run_session_root_economy_starting_credits_invalid expected_non_negative",
                        errors);
                    Require(
                        serializedEconomy.FindProperty("maximumDeliveryEntries")?.intValue >= 16,
                        "run_session_root_economy_delivery_capacity_invalid",
                        errors);
                }

                var randomLedgers = prefab.GetComponentsInChildren<NetworkRunRandomLedger>(true);
                Require(
                    randomLedgers.Length == 1,
                    $"run_session_root_random_ledger_count_invalid expected=1 actual={randomLedgers.Length}",
                    errors);
                var randomLedger = prefab.GetComponent<NetworkRunRandomLedger>();
                Require(
                    randomLedger != null,
                    "run_session_root_random_ledger_owner_invalid expected=root",
                    errors);
                Require(
                    NetworkRunRandomLedger.TryValidateAlgorithmContract(
                        out var randomAlgorithmReason),
                    $"run_session_root_random_algorithm_contract_invalid reason={randomAlgorithmReason}",
                    errors);

                var incidentLedgers =
                    prefab.GetComponentsInChildren<NetworkRunIncidentLedger>(true);
                Require(
                    incidentLedgers.Length == 1,
                    $"run_session_root_incident_ledger_count_invalid " +
                    $"expected=1 actual={incidentLedgers.Length}",
                    errors);
                var incidentLedger =
                    prefab.GetComponent<NetworkRunIncidentLedger>();
                Require(
                    incidentLedger != null,
                    "run_session_root_incident_ledger_owner_invalid expected=root",
                    errors);
                if (incidentLedger != null)
                {
                    var serializedIncidentLedger =
                        new SerializedObject(incidentLedger);
                    Require(
                        serializedIncidentLedger
                            .FindProperty("defaultPressureCapacity")
                            ?.intValue == 3,
                        "run_session_root_incident_default_pressure_invalid " +
                        "expected=3",
                        errors);
                    Require(
                        serializedIncidentLedger
                            .FindProperty("defaultMaximumExternalCommands")
                            ?.intValue == 1,
                        "run_session_root_incident_default_external_cap_invalid " +
                        "expected=1",
                        errors);
                    Require(
                        serializedIncidentLedger
                            .FindProperty("defaultMaximumInternalCommands")
                            ?.intValue == 2,
                        "run_session_root_incident_default_internal_cap_invalid " +
                        "expected=2",
                        errors);
                    Require(
                        serializedIncidentLedger
                            .FindProperty("maximumCommandHistory")
                            ?.intValue >= 32,
                        "run_session_root_incident_command_history_invalid " +
                        "expected_minimum=32",
                        errors);
                }

                var incidentDirectors =
                    prefab.GetComponentsInChildren<PHSNetworkIncidentDirector>(true);
                Require(
                    incidentDirectors.Length == 1,
                    $"run_session_root_incident_director_count_invalid " +
                    $"expected=1 actual={incidentDirectors.Length}",
                    errors);
                var incidentDirector =
                    prefab.GetComponent<PHSNetworkIncidentDirector>();
                Require(
                    incidentDirector != null,
                    "run_session_root_incident_director_owner_invalid expected=root",
                    errors);
                if (incidentDirector != null)
                {
                    Require(
                        incidentDirector.enabled,
                        "run_session_root_incident_director_disabled",
                        errors);
                }

                var networkBehaviours = prefab.GetComponents<NetworkBehaviour>();
                var expectedBehaviourTypes = new[]
                {
                    typeof(NetworkRunFlowCoordinator),
                    typeof(NetworkShipSystemsState),
                    typeof(NetworkRunSessionRoot),
                    typeof(NetworkRunStageClock),
                    typeof(NetworkRunEconomyLedger),
                    typeof(NetworkRunRandomLedger),
                    typeof(NetworkRunIncidentLedger),
                    typeof(NetworkShopTransitionVoteCoordinator)
                };
                Require(
                    networkBehaviours.Length == expectedBehaviourTypes.Length,
                    $"run_session_root_network_behaviour_count_invalid expected={expectedBehaviourTypes.Length} actual={networkBehaviours.Length}",
                    errors);
                var comparableBehaviourCount = Math.Min(
                    networkBehaviours.Length,
                    expectedBehaviourTypes.Length);
                for (var index = 0; index < comparableBehaviourCount; index++)
                {
                    Require(
                        networkBehaviours[index] != null
                        && networkBehaviours[index].GetType() == expectedBehaviourTypes[index],
                        $"run_session_root_network_behaviour_order_invalid index={index} expected={expectedBehaviourTypes[index].Name} actual={networkBehaviours[index]?.GetType().Name ?? "null"}",
                        errors);
                }

                var coordinator = prefab.GetComponent<NetworkRunFlowCoordinator>();
                Require(coordinator != null, "run_session_root_run_flow_missing", errors);
                if (coordinator != null)
                {
                    var serializedCoordinator = new SerializedObject(coordinator);
                    var mapCatalog = serializedCoordinator
                        .FindProperty("mapCatalog")
                        ?.objectReferenceValue as PHSMapCatalogSO;
                    ValidateMapCatalog("run_session_root", mapCatalog, 2, errors);
                    Require(
                        stageClock != null
                        && serializedCoordinator.FindProperty("stageClock")?.objectReferenceValue
                        == stageClock,
                        "run_session_root_stage_clock_reference_invalid",
                        errors);
                    Require(
                        serializedCoordinator.FindProperty("initialMapId")?.intValue == 8001,
                        "run_session_root_initial_map_invalid expected=8001",
                        errors);
                    Require(
                        serializedCoordinator.FindProperty("requireAllConnectedAlivePlayersSafe")?.boolValue == false,
                        "run_session_root_phase_based_warp_safety_disabled",
                        errors);
                    Require(
                        serializedCoordinator.FindProperty("automaticallyLoadShop")?.boolValue == true,
                        "run_session_root_automatic_shop_load_disabled",
                        errors);
                }

                var shipSystemsState = prefab.GetComponent<NetworkShipSystemsState>();
                Require(
                    shipSystemsState != null,
                    "run_session_root_ship_systems_missing",
                    errors);

                var impactAdapter = prefab.GetComponent<PHSShipEventImpactAdapter>();
                Require(
                    impactAdapter != null,
                    "run_session_root_event_impact_adapter_missing",
                    errors);
                if (impactAdapter != null)
                {
                    var serializedImpactAdapter = new SerializedObject(impactAdapter);
                    Require(
                        serializedImpactAdapter.FindProperty("shipSystemsState")?.objectReferenceValue
                            == shipSystemsState,
                        "run_session_root_event_impact_state_invalid",
                        errors);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateEventPresentationPrefabs(ICollection<string> errors)
        {
            ValidateEventPresentationPrefab(
                $"{EventPresentationPrefabFolder}/PHS_FireEventPresentation.prefab",
                true,
                true,
                errors);
            ValidateEventPresentationPrefab(
                $"{EventPresentationPrefabFolder}/PHS_OxygenLeakEventPresentation.prefab",
                true,
                false,
                errors);
            ValidateEventPresentationPrefab(
                $"{EventPresentationPrefabFolder}/PHS_PlayerAttackEnemyPresentation.prefab",
                false,
                false,
                errors);
            ValidateEventPresentationPrefab(
                $"{EventPresentationPrefabFolder}/PHS_DeviceAttackEnemyPresentation.prefab",
                false,
                false,
                errors);
        }

        private static void ValidateEventPresentationPrefab(
            string path,
            bool requiresRepairCollider,
            bool validateRendererMaterials,
            ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(path);
            if (prefab == null)
            {
                errors.Add($"event_presentation_prefab_missing path={path}");
                return;
            }

            try
            {
                var view = prefab.GetComponent<EventEffectPresentationView>();
                Require(view != null, $"event_presentation_view_missing path={path}", errors);
                Require(prefab.GetComponentInChildren<FireEffectInstance>(true) == null, $"event_presentation_fire_gameplay_found path={path}", errors);
                Require(prefab.GetComponentInChildren<OxygenLeakEffectInstance>(true) == null, $"event_presentation_oxygen_gameplay_found path={path}", errors);
                Require(prefab.GetComponentInChildren<EnemyBase>(true) == null, $"event_presentation_enemy_gameplay_found path={path}", errors);
                Require(prefab.GetComponentInChildren<NetworkObject>(true) == null, $"event_presentation_network_object_found path={path}", errors);
                Require(prefab.GetComponentInChildren<Rigidbody>(true) == null, $"event_presentation_rigidbody_found path={path}", errors);
                Require(prefab.GetComponentInChildren<NavMeshAgent>(true) == null, $"event_presentation_navmesh_agent_found path={path}", errors);

                var colliders = prefab.GetComponentsInChildren<Collider>(true);
                Require(
                    requiresRepairCollider ? colliders.Length == 1 : colliders.Length == 0,
                    $"event_presentation_collider_count_invalid path={path} actual={colliders.Length} repairable={requiresRepairCollider}",
                    errors);
                foreach (var collider in colliders)
                {
                    Require(collider.isTrigger, $"event_presentation_collider_not_trigger path={path}", errors);
                }

                if (validateRendererMaterials)
                {
                    var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    Require(
                        renderers.Length > 0,
                        $"event_presentation_renderer_missing path={path}",
                        errors);
                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        Require(
                            materials.Length > 0,
                            $"event_presentation_material_slots_missing path={path} renderer={GetHierarchyPath(renderer.transform)}",
                            errors);
                        for (var index = 0; index < materials.Length; index++)
                        {
                            var material = materials[index];
                            Require(
                                material != null
                                && material.shader != null
                                && material.shader.isSupported,
                                $"event_presentation_material_invalid path={path} renderer={GetHierarchyPath(renderer.transform)} slot={index}",
                                errors);
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidatePlayHudPrefab(ICollection<string> errors)
        {
            var prefab = PrefabUtility.LoadPrefabContents(PlayHudPrefabPath);
            if (prefab == null)
            {
                errors.Add($"play_hud_prefab_missing path={PlayHudPrefabPath}");
                return;
            }

            try
            {
                var eventHudViews = prefab.GetComponentsInChildren<PHSNetworkEventHudView>(true);
                var eventHudBinders = prefab.GetComponentsInChildren<PHSNetworkEventHudBinder>(true);
                var shipSystemsBinders = prefab.GetComponentsInChildren<PHSShipSystemsHudBinder>(true);
                var partyCreditsBinders = prefab.GetComponentsInChildren<PartyCreditsHudBinder>(true);
                Require(
                    eventHudViews.Length == 1,
                    $"play_hud_event_view_count_invalid actual={eventHudViews.Length}",
                    errors);
                Require(
                    eventHudBinders.Length == 1,
                    $"play_hud_event_binder_count_invalid actual={eventHudBinders.Length}",
                    errors);
                Require(
                    shipSystemsBinders.Length == 1,
                    $"play_hud_ship_systems_binder_count_invalid actual={shipSystemsBinders.Length}",
                    errors);
                Require(
                    partyCreditsBinders.Length == 0,
                    $"play_hud_scene_wallet_binder_must_be_scene_owned actual={partyCreditsBinders.Length}",
                    errors);

                ValidateEventHudWiring(
                    "play_hud",
                    eventHudViews.Length == 1 ? eventHudViews[0] : null,
                    eventHudBinders.Length == 1 ? eventHudBinders[0] : null,
                    shipSystemsBinders.Length == 1 ? shipSystemsBinders[0] : null,
                    errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateGravityZone(
            GravityZone zone,
            LastJumpCrew.Common.GravityMode expectedMode,
            int expectedPriority,
            string errorPrefix,
            ICollection<string> errors)
        {
            Require(zone != null, $"{errorPrefix}_missing", errors);
            if (zone == null)
            {
                return;
            }

            var serializedZone = new SerializedObject(zone);
            Require(
                serializedZone.FindProperty("gravityMode")?.enumValueIndex == (int)expectedMode,
                $"{errorPrefix}_mode_invalid expected={expectedMode}",
                errors);
            Require(
                serializedZone.FindProperty("priority")?.intValue == expectedPriority,
                $"{errorPrefix}_priority_invalid expected={expectedPriority}",
                errors);
        }

        private static void ValidateShipPowerWiring(ICollection<string> errors)
        {
            var batterySocket = FindOne<BatteryInsertPowerStationSocket>(
                "map_battery_socket",
                errors);
            if (batterySocket != null)
            {
                Require(
                    batterySocket.GetComponent<NetworkObject>() != null,
                    "map_battery_socket_network_object_missing",
                    errors);
                var serializedSocket = new SerializedObject(batterySocket);
                Require(
                    serializedSocket.FindProperty("requiredItemId")?.stringValue == "battery_pack",
                    "map_battery_socket_item_id_invalid expected=battery_pack",
                    errors);
                RequireObject(
                    serializedSocket,
                    "installedBatteryVisual",
                    "map_battery_socket_visual_missing",
                    errors);
            }

            var gravityController = FindOne<ShipGravityZoneController>(
                "map_ship_gravity_controller",
                errors);
            Require(
                UnityEngine.Object.FindObjectsByType<PHSShipAccidentAnchor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(anchor =>
                    anchor != null
                    && anchor.AnchorId == "gravity_generator"
                    && anchor.ModuleId == NetworkShipModuleId.Gravity),
                "map_gravity_generator_anchor_missing",
                errors);

            if (gravityController != null)
            {
                var serializedController = new SerializedObject(gravityController);
                var legacyAreas = serializedController.FindProperty("shipInteriorAreas");
                var gravityZones = serializedController.FindProperty("gravityZones");
                var configuredCount =
                    (legacyAreas != null && legacyAreas.isArray ? legacyAreas.arraySize : 0) +
                    (gravityZones != null && gravityZones.isArray ? gravityZones.arraySize : 0);
                Require(
                    configuredCount > 0,
                    "map_ship_gravity_areas_missing",
                    errors);
                RequireArrayElementsAssigned(
                    legacyAreas,
                    "map_ship_gravity_area_missing",
                    errors);
                RequireArrayElementsAssigned(
                    gravityZones,
                    "map_gravity_zone_missing",
                    errors);
            }
        }

        private static void ValidatePartyCreditsWiring(
            string label,
            PartyCreditsHudBinder binder,
            ICollection<string> errors)
        {
            if (binder == null)
            {
                return;
            }

            var serializedBinder = new SerializedObject(binder);
            RequireObject(
                serializedBinder,
                "playHudPresenter",
                $"{label}_party_credits_presenter_missing",
                errors);
            RequireObject(
                serializedBinder,
                "shopWalletSource",
                $"{label}_party_credits_wallet_missing",
                errors);
        }

        private static void ValidateEventHudWiring(
            string label,
            PHSNetworkEventHudView eventHudView,
            PHSNetworkEventHudBinder eventHudBinder,
            PHSShipSystemsHudBinder shipSystemsBinder,
            ICollection<string> errors)
        {
            if (eventHudView != null)
            {
                var serializedView = new SerializedObject(eventHudView);
                RequireObject(
                    serializedView,
                    "eventAlertRoot",
                    $"{label}_event_hud_alert_root_missing",
                    errors);
                RequireObject(
                    serializedView,
                    "eventAlertText",
                    $"{label}_event_hud_alert_text_missing",
                    errors);
                RequireObject(
                    serializedView,
                    "shipMapRoot",
                    $"{label}_event_hud_ship_map_root_missing",
                    errors);

                var roomViews = serializedView.FindProperty("roomViews");
                Require(
                    roomViews != null && roomViews.isArray && roomViews.arraySize == 4,
                    $"{label}_event_hud_room_count_invalid expected=4 actual={roomViews?.arraySize ?? -1}",
                    errors);
                if (roomViews != null && roomViews.isArray)
                {
                    var roomIds = new HashSet<string>(StringComparer.Ordinal);
                    for (var index = 0; index < roomViews.arraySize; index++)
                    {
                        var roomView = roomViews.GetArrayElementAtIndex(index);
                        var roomId = roomView.FindPropertyRelative("roomId")?.stringValue;
                        Require(
                            !string.IsNullOrWhiteSpace(roomId),
                            $"{label}_event_hud_room_id_missing index={index}",
                            errors);
                        if (!string.IsNullOrWhiteSpace(roomId))
                        {
                            Require(
                                roomIds.Add(roomId),
                                $"{label}_event_hud_room_id_duplicate room={roomId}",
                                errors);
                        }

                        RequireRelativeObject(
                            roomView,
                            "roomRoot",
                            $"{label}_event_hud_room_root_missing index={index}",
                            errors);
                        RequireRelativeObject(
                            roomView,
                            "activeEventIcon",
                            $"{label}_event_hud_room_icon_missing index={index}",
                            errors);
                        RequireRelativeObject(
                            roomView,
                            "statusLabel",
                            $"{label}_event_hud_room_status_missing index={index}",
                            errors);
                    }
                }
            }

            if (eventHudBinder != null)
            {
                var serializedBinder = new SerializedObject(eventHudBinder);
                RequireObject(
                    serializedBinder,
                    "eventHudViewSource",
                    $"{label}_event_hud_binder_view_missing",
                    errors);
                Require(
                    serializedBinder.FindProperty("eventHudViewSource")?.objectReferenceValue == eventHudView,
                    $"{label}_event_hud_binder_view_mismatch",
                    errors);
                Require(
                    Mathf.Approximately(
                        serializedBinder.FindProperty("currentMapMessageSeconds")?.floatValue ?? 0f,
                        3f),
                    $"{label}_current_map_message_duration_invalid",
                    errors);
            }

            if (shipSystemsBinder != null)
            {
                var serializedShipBinder = new SerializedObject(shipSystemsBinder);
                RequireObject(
                    serializedShipBinder,
                    "presenter",
                    $"{label}_ship_systems_presenter_missing",
                    errors);
                RequireObject(
                    serializedShipBinder,
                    "optionalModuleStatusText",
                    $"{label}_ship_systems_module_text_missing",
                    errors);
            }
        }

        private static void ValidateShopCatalog(ICollection<string> errors)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogSO>(ShopCatalogPath);
            Require(catalog != null, "shop_catalog_asset_missing", errors);
            if (catalog == null)
            {
                return;
            }

            Require(catalog.Products.Count == 13, $"shop_catalog_count_invalid actual={catalog.Products.Count}", errors);
            var networkPrefabHashes = new HashSet<long>();
            foreach (var product in catalog.Products)
            {
                Require(product != null && product.IsConfigured, "shop_product_invalid", errors);
                Require(
                    product != null && product.StockPolicy == ShopStockPolicy.Unlimited,
                    $"shop_stock_policy_invalid offer={product?.OfferId}",
                    errors);
                ValidateShopDroppedPrefab(product, networkPrefabHashes, errors);
            }
        }

        private static void ValidatePurchaseDeliveryBox(ICollection<string> errors)
        {
            var deliveryBox = FindOne<PurchaseDeliveryBox>(
                "map_purchase_delivery_box",
                errors);
            if (deliveryBox == null)
            {
                return;
            }

            var serializedBox = new SerializedObject(deliveryBox);
            RequireObject(
                serializedBox,
                "catalog",
                "map_purchase_delivery_catalog_missing",
                errors);
            RequireArray(
                serializedBox,
                "deliverySlots",
                1,
                "map_purchase_delivery_slots_missing",
                errors);
            Require(
                !string.IsNullOrWhiteSpace(
                    serializedBox.FindProperty("deliveryBoxId")?.stringValue),
                "map_purchase_delivery_box_id_missing",
                errors);
        }

        private static void ValidateShopDroppedPrefab(
            ShopProductData product,
            ISet<long> networkPrefabHashes,
            ICollection<string> errors)
        {
            var itemData = product?.ItemPrefabData;
            var heldPrefab = itemData?.HeldPrefab;
            var droppedPrefab = itemData?.DroppedPrefab;
            var offerId = product?.OfferId ?? "null";
            Require(itemData != null, $"shop_item_data_missing offer={offerId}", errors);
            Require(heldPrefab != null, $"shop_held_prefab_missing offer={offerId}", errors);
            Require(droppedPrefab != null, $"shop_dropped_prefab_missing offer={offerId}", errors);
            Require(
                heldPrefab == null || droppedPrefab == null || heldPrefab != droppedPrefab,
                $"shop_held_dropped_prefab_same offer={offerId}",
                errors);
            if (heldPrefab != null)
            {
                var heldPath = AssetDatabase.GetAssetPath(heldPrefab);
                Require(
                    heldPrefab.GetComponent<UtilityItemObject>() != null,
                    $"shop_held_utility_item_missing offer={offerId} path={heldPath}",
                    errors);
                Require(
                    heldPrefab.GetComponentsInChildren<NetworkObject>(true).Length == 0,
                    $"shop_held_network_object_present offer={offerId} path={heldPath}",
                    errors);
                Require(
                    heldPrefab.GetComponentsInChildren<NetworkTransform>(true).Length == 0,
                    $"shop_held_network_transform_present offer={offerId} path={heldPath}",
                    errors);
                Require(
                    heldPrefab.GetComponentsInChildren<ThrownItemImpact>(true).Length == 0,
                    $"shop_held_impact_present offer={offerId} path={heldPath}",
                    errors);
            }

            if (droppedPrefab == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(droppedPrefab);
            Require(
                droppedPrefab.GetComponent<UtilityItemObject>() != null,
                $"shop_dropped_utility_item_missing offer={offerId} path={path}",
                errors);
            Require(
                droppedPrefab.GetComponent<Rigidbody>() != null,
                $"shop_dropped_rigidbody_missing offer={offerId} path={path}",
                errors);
            var networkObject = droppedPrefab.GetComponent<NetworkObject>();
            Require(
                networkObject != null,
                $"shop_dropped_network_object_missing offer={offerId} path={path}",
                errors);
            if (networkObject != null)
            {
                var hashProperty = new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash");
                var hash = hashProperty?.longValue ?? 0;
                Require(
                    hash != 0,
                    $"shop_dropped_network_hash_missing offer={offerId} path={path}",
                    errors);
                Require(
                    hash == 0 || networkPrefabHashes.Add(hash),
                    $"shop_dropped_network_hash_duplicate offer={offerId} hash={hash} path={path}",
                    errors);
            }

            Require(
                droppedPrefab.GetComponent<NetworkTransform>() != null,
                $"shop_dropped_network_transform_missing offer={offerId} path={path}",
                errors);
            Require(
                droppedPrefab.GetComponent<NetworkItemPhysicsAuthority>() != null,
                $"shop_dropped_physics_authority_missing offer={offerId} path={path}",
                errors);
            Require(
                droppedPrefab.GetComponent<ThrownItemImpact>() != null,
                $"shop_dropped_impact_missing offer={offerId} path={path}",
                errors);
        }

        private static void ValidateUtilityItemCatalog(ICollection<string> errors)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(UtilityItemCatalogPath);
            Require(catalog != null, "utility_item_catalog_missing", errors);
            if (catalog == null)
            {
                return;
            }

            Require(
                catalog.Items.Count == 18,
                $"utility_item_catalog_count_invalid actual={catalog.Items.Count}",
                errors);

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var networkPrefabHashes = new HashSet<long>();
            foreach (var itemData in catalog.Items)
            {
                Require(itemData != null, "utility_item_catalog_entry_null", errors);
                if (itemData == null)
                {
                    continue;
                }

                var itemId = itemData.ItemId;
                Require(
                    !string.IsNullOrWhiteSpace(itemId),
                    $"utility_item_id_missing asset={AssetDatabase.GetAssetPath(itemData)}",
                    errors);
                Require(
                    string.IsNullOrWhiteSpace(itemId) || itemIds.Add(itemId),
                    $"utility_item_id_duplicate item={itemId}",
                    errors);

                var heldPrefab = itemData.HeldPrefab;
                var droppedPrefab = itemData.DroppedPrefab;
                Require(heldPrefab != null, $"utility_held_prefab_missing item={itemId}", errors);
                Require(droppedPrefab != null, $"utility_dropped_prefab_missing item={itemId}", errors);
                Require(
                    heldPrefab == null || droppedPrefab == null || heldPrefab != droppedPrefab,
                    $"utility_held_dropped_prefab_same item={itemId}",
                    errors);

                if (heldPrefab != null)
                {
                    var heldPath = AssetDatabase.GetAssetPath(heldPrefab);
                    Require(
                        heldPrefab.GetComponent<UtilityItemObject>() != null,
                        $"utility_held_item_missing item={itemId} path={heldPath}",
                        errors);
                    Require(
                        heldPrefab.GetComponentsInChildren<NetworkObject>(true).Length == 0,
                        $"utility_held_network_object_present item={itemId} path={heldPath}",
                        errors);
                    Require(
                        heldPrefab.GetComponentsInChildren<NetworkTransform>(true).Length == 0,
                        $"utility_held_network_transform_present item={itemId} path={heldPath}",
                        errors);
                    Require(
                        heldPrefab.GetComponentsInChildren<ThrownItemImpact>(true).Length == 0,
                        $"utility_held_impact_present item={itemId} path={heldPath}",
                        errors);
                }

                if (droppedPrefab == null)
                {
                    continue;
                }

                var droppedPath = AssetDatabase.GetAssetPath(droppedPrefab);
                Require(
                    droppedPrefab.GetComponent<UtilityItemObject>() != null,
                    $"utility_dropped_item_missing item={itemId} path={droppedPath}",
                    errors);
                Require(
                    droppedPrefab.GetComponent<Rigidbody>() != null,
                    $"utility_dropped_rigidbody_missing item={itemId} path={droppedPath}",
                    errors);
                Require(
                    droppedPrefab.GetComponent<NetworkTransform>() != null,
                    $"utility_dropped_network_transform_missing item={itemId} path={droppedPath}",
                    errors);
                Require(
                    droppedPrefab.GetComponent<NetworkItemPhysicsAuthority>() != null,
                    $"utility_dropped_physics_authority_missing item={itemId} path={droppedPath}",
                    errors);

                var networkObject = droppedPrefab.GetComponent<NetworkObject>();
                Require(
                    networkObject != null,
                    $"utility_dropped_network_object_missing item={itemId} path={droppedPath}",
                    errors);
                if (networkObject == null)
                {
                    continue;
                }

                var hashProperty = new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash");
                var hash = hashProperty?.longValue ?? 0;
                Require(
                    hash != 0,
                    $"utility_dropped_network_hash_missing item={itemId} path={droppedPath}",
                    errors);
                Require(
                    hash == 0 || networkPrefabHashes.Add(hash),
                    $"utility_dropped_network_hash_duplicate item={itemId} hash={hash} path={droppedPath}",
                    errors);
            }
        }

        private static void ValidateSceneSellZones(string sceneLabel, ICollection<string> errors)
        {
            var sellZones = UnityEngine.Object.FindObjectsByType<DebrisSellZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(sellZones.Length > 0, $"{sceneLabel}_sell_zone_missing", errors);
            foreach (var sellZone in sellZones)
            {
                Require(
                    sellZone.GetComponent<NetworkObject>() != null,
                    $"{sceneLabel}_sell_zone_network_object_missing zone={sellZone.name}",
                    errors);
                var serializedZone = new SerializedObject(sellZone);
                RequireObject(
                    serializedZone,
                    "shopWalletSource",
                    $"{sceneLabel}_sell_wallet_missing zone={sellZone.name}",
                    errors);
                RequireArray(
                    serializedZone,
                    "sellableDebris",
                    5,
                    $"{sceneLabel}_sellable_debris_insufficient zone={sellZone.name}",
                    errors);
            }
        }

        private static void ValidateNetworkScenePortal(
            string label,
            string expectedObjectName,
            string expectedDestinationScene,
            ShopSceneTransitionMode expectedTransitionMode,
            ICollection<string> errors)
        {
            var portals = UnityEngine.Object.FindObjectsByType<NetworkScenePortalInteractable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(portals.Length == 1, $"{label}_count_invalid actual={portals.Length}", errors);

            var portal = portals.FirstOrDefault(candidate => candidate.name == expectedObjectName);
            Require(portal != null, $"{label}_missing expected={expectedObjectName}", errors);
            if (portal == null)
            {
                return;
            }

            Require(portal.isActiveAndEnabled, $"{label}_inactive", errors);
            Require(portal.GetComponent<Collider>() != null, $"{label}_collider_missing", errors);

            var serializedPortal = new SerializedObject(portal);
            Require(
                serializedPortal.FindProperty("destinationSceneName")?.stringValue ==
                expectedDestinationScene,
                $"{label}_destination_invalid expected={expectedDestinationScene}",
                errors);
            Require(
                serializedPortal.FindProperty("shopTransitionMode")?.enumValueIndex ==
                (int)expectedTransitionMode,
                $"{label}_transition_invalid expected={expectedTransitionMode}",
                errors);
            Require(
                serializedPortal.FindProperty("serverInteractionDistance")?.floatValue >= 0.5f,
                $"{label}_distance_invalid",
                errors);
        }

        private static void ValidateGameplayContext(string sceneLabel, ICollection<string> errors)
        {
            var context = FindOne<GameplaySceneContext>($"{sceneLabel}_gameplay_context", errors);
            if (context != null)
            {
                RequireObject(
                    new SerializedObject(context),
                    "respawnPoint",
                    $"{sceneLabel}_respawn_point_missing",
                    errors);
            }
        }

        private static void OpenAndValidateScene(string path, ICollection<string> errors)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, $"scene_open_failed path={path}", errors);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var missingScriptCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }

            Require(missingScriptCount == 0, $"scene_missing_scripts path={path} count={missingScriptCount}", errors);
        }

        private static void ValidateNoSceneOwnedStageClock(
            string sceneLabel,
            ICollection<string> errors)
        {
            var stageClocks = UnityEngine.Object.FindObjectsByType<NetworkRunStageClock>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                stageClocks.Length == 0,
                $"{sceneLabel}_stage_clock_must_be_session_owned actual={stageClocks.Length}",
                errors);
        }

        private static void ValidateNoSceneOwnedEconomyLedger(
            string sceneLabel,
            ICollection<string> errors)
        {
            var ledgers = UnityEngine.Object.FindObjectsByType<NetworkRunEconomyLedger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                ledgers.Length == 0,
                $"{sceneLabel}_economy_ledger_must_be_session_owned actual={ledgers.Length}",
                errors);
        }

        private static void ValidateNoSceneOwnedRandomLedger(
            string sceneLabel,
            ICollection<string> errors)
        {
            var ledgers = UnityEngine.Object.FindObjectsByType<NetworkRunRandomLedger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                ledgers.Length == 0,
                $"{sceneLabel}_random_ledger_must_be_session_owned actual={ledgers.Length}",
                errors);
        }

        private static void ValidateNoSceneOwnedIncidentRootComponents(
            string sceneLabel,
            ICollection<string> errors)
        {
            var ledgers =
                UnityEngine.Object.FindObjectsByType<NetworkRunIncidentLedger>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                ledgers.Length == 0,
                $"{sceneLabel}_incident_ledger_must_be_session_owned " +
                $"actual={ledgers.Length}",
                errors);
            var directors =
                UnityEngine.Object.FindObjectsByType<PHSNetworkIncidentDirector>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                directors.Length == 0,
                $"{sceneLabel}_incident_director_must_be_session_owned " +
                $"actual={directors.Length}",
                errors);
        }

        private static void ValidateNoLegacyEconomyOwner(
            string sceneLabel,
            ICollection<string> errors)
        {
            var legacyWallets = UnityEngine.Object.FindObjectsByType<SessionPartyCreditsWallet>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                legacyWallets.Length == 0,
                $"{sceneLabel}_legacy_party_wallet_present actual={legacyWallets.Length}",
                errors);

            var legacyRoots = UnityEngine.Object.FindObjectsByType<SessionPurchaseStateRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(
                legacyRoots.Length == 0,
                $"{sceneLabel}_legacy_purchase_state_root_present actual={legacyRoots.Length} " +
                $"objects={string.Join(",", legacyRoots.Select(root => GetHierarchyPath(root.transform)))} " +
                $"components={string.Join("|", legacyRoots.SelectMany(root => root.GetComponents<Component>()).Where(component => component != null).Select(component => component.GetType().Name))} " +
                $"prefabs={string.Join(",", legacyRoots.Select(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject)))}",
                errors);
        }

        private static T FindOne<T>(string label, ICollection<string> errors) where T : UnityEngine.Object
        {
            var matches = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(matches.Length == 1, $"{label}_count_invalid actual={matches.Length}", errors);
            return matches.Length == 1 ? matches[0] : null;
        }

        private static T[] FindSceneComponents<T>(
            UnityEngine.SceneManagement.Scene scene)
            where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<T>();
            }

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static MonoBehaviour[] FindAllMonoBehaviours()
        {
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void RequireObject(
            SerializedObject serializedObject,
            string propertyName,
            string error,
            ICollection<string> errors)
        {
            var property = serializedObject.FindProperty(propertyName);
            Require(property != null && property.objectReferenceValue != null, error, errors);
        }

        private static void RequireArray(
            SerializedObject serializedObject,
            string propertyName,
            int minimumCount,
            string error,
            ICollection<string> errors)
        {
            var property = serializedObject.FindProperty(propertyName);
            Require(property != null && property.isArray && property.arraySize >= minimumCount, error, errors);
        }

        private static void RequireRelativeObject(
            SerializedProperty parent,
            string propertyName,
            string error,
            ICollection<string> errors)
        {
            var property = parent.FindPropertyRelative(propertyName);
            Require(property != null && property.objectReferenceValue != null, error, errors);
        }

        private static void RequireArrayElementsAssigned(
            SerializedProperty property,
            string errorPrefix,
            ICollection<string> errors)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                Require(
                    property.GetArrayElementAtIndex(index).objectReferenceValue != null,
                    $"{errorPrefix} index={index}",
                    errors);
            }
        }

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }
    }
}
