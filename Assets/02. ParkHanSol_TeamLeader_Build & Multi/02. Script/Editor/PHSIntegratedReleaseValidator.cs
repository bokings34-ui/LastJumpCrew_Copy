using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using SM;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSIntegratedReleaseValidator
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string HandheldMapPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Maps/PHS_HandheldShipMap.prefab";
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";
        private const string MapProfileFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps";

        private static readonly string[] BuildScenePaths =
        {
            LobbyScenePath,
            TutorialScenePath,
            MapScenePath,
            ShopScenePath
        };

        [MenuItem("Tools/ParkHanSol/Validate Integrated Release")]
        public static void Validate()
        {
            ValidateBuildSettings();
            ValidatePlayerPrefab();
            ValidateHandheldMap();
            ValidateLobbyAndItems();
            ValidateSceneReferences();
            ValidateMapRuntime();
            ValidateProductionFeatureGates();
            PHSTeamEventStabilityAuthoring.ValidateMapRuntimeReferences();
            PHSPlayerHealthHudValidator.Validate();
            PHSShipMapReadabilityAuthoring.Validate();
            Debug.Log(
                "PHS_INTEGRATED_RELEASE_VALIDATION_PASS scenes=4 items=3 " +
                "missingPrefabs=0 missingScripts=0");
        }

        private static void ValidateProductionFeatureGates()
        {
            PHSRuntimeEditorOnlyComponentCleanup.ValidateRuntimeNetworkPrefabs();
            PHSCanonicalSpecialItemAuthoring.Validate();
            PHSNetworkAudioWiringAuthoring.ValidateSchedulerEventAudio();
            PHSNetworkAudioWiringAuthoring.ValidateMiniGameSuccessAudio();
            PHSFireSmokeColumnAuthoring.Validate();
            PHSOxygenContinuousSprayAuthoring.Validate();
            PHSHullBreachTeamSiteAuthoring.Validate();
            PHSToolBoxPersistenceAuthoring.Validate();
            LastJumpCrew.ParkHanSol.EditorTools.PHS0723OxygenZoneAuthoring.ValidateRuntimePrefab();
        }

        private static void ValidateBuildSettings()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            Require(
                enabledScenes.SequenceEqual(BuildScenePaths),
                $"build_scenes_invalid:{string.Join(",", enabledScenes)}");
        }

        private static void ValidatePlayerPrefab()
        {
            Require(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null,
                "player_prefab_missing");
            var player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Require(player.GetComponent<NetworkObject>() != null, "player_network_object_missing");
                Require(
                    player.GetComponentInChildren<NetworkPlayerPetOrbitFollower>(true) != null,
                    "player_pet_orbit_follower_missing");
                Require(
                    player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Any(renderer => renderer.sharedMesh != null),
                    "player_body_mesh_missing");
                Require(
                    player.GetComponentsInChildren<Animator>(true)
                        .Any(animator => animator.avatar != null && animator.runtimeAnimatorController != null),
                    "player_animator_assets_missing");

                var cameraWallProtection = player.GetComponentInChildren<PHSFirstPersonCameraWallProtection>(true);
                Require(cameraWallProtection != null, "player_camera_wall_protection_missing");
                var cameraWallProtectionData = new SerializedObject(cameraWallProtection);
                RequireReferences(cameraWallProtectionData, "cameraTransform");
                Require(
                    cameraWallProtectionData.FindProperty("collisionLayers")?.intValue != 0
                    && cameraWallProtectionData.FindProperty("probeRadius")?.floatValue > 0f
                    && cameraWallProtectionData.FindProperty("wallClearance")?.floatValue >= 0f,
                    "player_camera_wall_protection_settings_invalid");

                var audioEmitters = player.GetComponentsInChildren<NetworkAudioCueEmitter>(true);
                Require(audioEmitters.Length > 0, "player_audio_emitters_missing");
                Require(
                    audioEmitters.All(emitter => emitter.HasRequiredReferences),
                    "player_audio_emitter_references_missing");
                var resultControllers = player
                    .GetComponentsInChildren<NetworkRunResultPanelController>(true);
                Require(resultControllers.Length == 1, $"player_result_controller_count:{resultControllers.Length}");
                var resultController = new SerializedObject(resultControllers[0]);
                RequireReferences(
                    resultController,
                    "playerController",
                    "panelView",
                    "audioCuePlayerSource");
                var resultView = resultController.FindProperty("panelView")
                    .objectReferenceValue as NetworkRunResultPanelView;
                Require(resultView != null && resultView.HasRequiredReferences, "player_result_view_invalid");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void ValidateHandheldMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandheldMapPrefabPath);
            Require(prefab != null, "handheld_map_prefab_missing");

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var playerViews = player == null
                ? Array.Empty<PHSHandheldShipMapView>()
                : player.GetComponentsInChildren<PHSHandheldShipMapView>(true);
            Require(playerViews.Length == 2, $"player_handheld_map_view_count:{playerViews.Length}");
            for (var index = 0; index < playerViews.Length; index++)
            {
                ValidateHandheldMapView(playerViews[index], $"player:{index}");
            }
        }

        private static void ValidateHandheldMapView(
            PHSHandheldShipMapView view,
            string label)
        {
            Require(view != null, $"handheld_map_view_missing:{label}");
            var root = view.transform;
            var mapCanvas = FindChild(root, "MapCanvas")?.GetComponent<RectTransform>();
            var title = FindChild(root, "Title")?.GetComponent<RectTransform>();
            Require(mapCanvas != null && title != null, $"handheld_map_frame_missing:{label}");
            var viewData = new SerializedObject(view);
            RequireReferences(
                viewData,
                "mapImage",
                "markerRoot",
                "markerTemplate",
                "markerGlyphTemplate",
                "markerLabelTemplate");
            var canvasTop = mapCanvas.rect.height * 0.5f;
            var titleTop = title.anchoredPosition.y + title.rect.height * 0.5f;
            Require(titleTop <= canvasTop, $"handheld_title_clipped:{label}:{titleTop}>{canvasTop}");
        }

        private static void ValidateMicDestroyProfiles()
        {
            for (var mapId = 8001; mapId <= 8004; mapId++)
            {
                var matches = AssetDatabase.FindAssets(
                        $"t:{nameof(PHSMapProfileSO)}",
                        new[] { MapProfileFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>)
                    .Where(profile => profile != null && profile.MapId == mapId)
                    .ToArray();
                Require(matches.Length == 1, $"mic_destroy_profile_count:{mapId}:{matches.Length}");
                Require(matches[0].ExternalThreatWeights.Any(
                        entry => entry != null && entry.EventId == EventId.MicDestroy),
                    $"mic_destroy_profile_weight_missing:{mapId}");
            }
        }

        private static void ValidateLobbyAndItems()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var networkManagers = roots
                .SelectMany(root => root.GetComponentsInChildren<NetworkManager>(true))
                .ToArray();
            Require(networkManagers.Length == 1, $"lobby_network_manager_count:{networkManagers.Length}");
            var networkManager = networkManagers[0];
            Require(networkManager.NetworkConfig != null, "lobby_network_config_missing");
            Require(
                AssetDatabase.GetAssetPath(networkManager.NetworkConfig.PlayerPrefab) == PlayerPrefabPath,
                "lobby_player_prefab_invalid");
            Require(
                networkManager.NetworkConfig.Prefabs?.NetworkPrefabsLists?.Count > 0,
                "lobby_network_prefab_list_missing");
            var menuControllers = roots
                .SelectMany(root => root.GetComponentsInChildren<ParkHanSolLobbyMenuController>(true))
                .ToArray();
            Require(menuControllers.Length == 1, "lobby_menu_controller_invalid");
            var menuController = menuControllers[0];
            var lobbyCanvas = menuController.GetComponentInParent<Canvas>();
            Require(
                lobbyCanvas != null
                && lobbyCanvas.enabled
                && lobbyCanvas.gameObject.activeInHierarchy,
                "lobby_canvas_inactive");
            var canvasScale = lobbyCanvas.transform.localScale;
            Require(
                Mathf.Abs(canvasScale.x) > 0.001f
                && Mathf.Abs(canvasScale.y) > 0.001f
                && Mathf.Abs(canvasScale.z) > 0.001f,
                $"lobby_canvas_scale_invalid:{canvasScale}");
            var menu = new SerializedObject(menuController);
            RequireReferences(
                menu,
                "startPanel",
                "lobbyPanel",
                "roomPanel",
                "settingsPanel",
                "startButton");
            var startPanel = menu.FindProperty("startPanel").objectReferenceValue as GameObject;
            Require(startPanel != null && startPanel.activeSelf, "lobby_start_panel_inactive");
            var startTitle = startPanel.transform.Find("Title");
            var startButtons = startPanel.transform.Find("lobby");
            Require(
                startTitle != null && startTitle.gameObject.activeSelf,
                "lobby_start_title_inactive");
            Require(
                startButtons != null && startButtons.gameObject.activeSelf,
                "lobby_start_buttons_inactive");

            var frontends = roots
                .SelectMany(root => root.GetComponentsInChildren<NetworkLobbyCustomizationFrontendController>(true))
                .ToArray();
            Require(frontends.Length == 1, $"lobby_customization_frontend_count:{frontends.Length}");
            var frontend = new SerializedObject(frontends[0]);
            RequireReferences(
                frontend,
                "catalog",
                "panelRoot",
                "openButton",
                "closeButton",
                "creditsLabel",
                "statusLabel",
                "localService",
                "previewPresenter",
                "lobbyEventSystem",
                "itemContent",
                "itemRowTemplate",
                "allItemsButton",
                "headItemsButton",
                "backItemsButton",
                "petItemsButton",
                "applyColorButton",
                "unequipHeadButton",
                "unequipBackButton",
                "resetPreviewButton");

            var presenter = frontend.FindProperty("previewPresenter").objectReferenceValue as MonoBehaviour;
            Require(presenter != null, "lobby_preview_presenter_missing");
            var preview = new SerializedObject(presenter);
            RequireReferences(
                preview,
                "previewRigRoot",
                "rotationRoot",
                "bodyRenderer",
                "headSlot",
                "backSlot",
                "petSlot",
                "frontSlot",
                "previewCamera",
                "previewImage");
            var previewRoot = preview.FindProperty("previewRigRoot").objectReferenceValue as Transform;
            Require(
                previewRoot != null
                && previewRoot.GetComponentsInChildren<NetworkObject>(true).Length == 0
                && previewRoot.GetComponentsInChildren<NetworkBehaviour>(true).Length == 0,
                "lobby_preview_rig_networked");

            ValidateItem(
                networkManager,
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_WrenchItemPrefabData.asset",
                "wrench",
                ItemUseType.Melee,
                "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Wrench/ParHanSol_WrenchItem_00.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_Wrench_Held.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_Wrench_Dropped.prefab");
            ValidateItem(
                networkManager,
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_BatteryItemPrefabData.asset",
                "battery_pack",
                ItemUseType.Throwable,
                "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Battery/ParkHanSol_BatteryPack_00.prefab",
                "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Battery/ParkHanSol_BatteryPack_00_Hand.prefab",
                "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Battery/ParkHanSol_BatteryPack_00_Dropped.prefab");
            ValidateItem(
                networkManager,
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FireExtinguisherItemPrefabData.asset",
                "fire_extinguisher",
                ItemUseType.Spray,
                "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/FireExtinguisher/ParkHanSol_FireExtinguisherItem_00.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_FireExtinguisher_Held.prefab",
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_FireExtinguisher_Dropped.prefab");
        }

        private static void ValidateItem(
            NetworkManager networkManager,
            string dataPath,
            string itemId,
            ItemUseType useType,
            string rootPath,
            string handPath,
            string droppedPath)
        {
            var data = AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(dataPath);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(rootPath);
            var hand = AssetDatabase.LoadAssetAtPath<GameObject>(handPath);
            var dropped = AssetDatabase.LoadAssetAtPath<GameObject>(droppedPath);
            Require(data != null, $"item_data_missing:{itemId}");
            Require(data.ItemId == itemId && data.UseType == useType, $"item_data_invalid:{itemId}");
            Require(data.TargetLayers.value != 0, $"item_target_layers_missing:{itemId}");
            Require(data.HandPrefab == hand && data.DroppedPrefab == dropped, $"item_prefab_wiring_invalid:{itemId}");
            Require(root?.GetComponent<UtilityItemObject>()?.ItemData == data, $"item_root_data_invalid:{itemId}");
            Require(dropped?.GetComponent<UtilityItemObject>()?.ItemData == data, $"item_dropped_data_invalid:{itemId}");
            Require(dropped.GetComponent<NetworkObject>() != null, $"item_network_object_missing:{itemId}");
            Require(
                dropped.GetComponent<NetworkItemPhysicsAuthority>() != null,
                $"item_physics_authority_missing:{itemId}");
            Require(
                dropped.GetComponent<NetworkUtilityItemDurabilityState>() != null,
                $"item_durability_state_missing:{itemId}");
            Require(
                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists
                    .Any(list => list != null && list.Contains(dropped)),
                $"item_network_prefab_unregistered:{itemId}");
        }

        private static void ValidateSceneReferences()
        {
            foreach (var scenePath in BuildScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                var missingScripts = transforms.Sum(transform =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
                Require(missingScripts == 0, $"scene_missing_scripts:{scenePath}:{missingScripts}");

                var missingPrefabRoots = transforms
                    .Where(transform =>
                        PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) == PrefabInstanceStatus.MissingAsset)
                    .Select(transform => PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject) ?? transform.gameObject)
                    .Distinct()
                    .Count();
                Require(missingPrefabRoots == 0, $"scene_missing_prefabs:{scenePath}:{missingPrefabRoots}");
            }
        }

        private static void ValidateMapRuntime()
        {
            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var gameplay = RequireOne<GameplaySceneContext>(scene, "map_gameplay_context");
            var gameplayData = new SerializedObject(gameplay);
            RequireReferences(gameplayData, "spawnPointsRoot", "respawnPoint");

            var teamCoordinator = ValidatePersistentTeamEventAuthority(scene);
            ValidateCanonicalEnemySpawnRuntime(scene, teamCoordinator);

            var runtime = RequireOne<PHSMapRuntimeContext>(scene, "map_runtime_context");
            var runtimeData = new SerializedObject(runtime);
            RequireReferences(
                runtimeData,
                "mapCatalog",
                "warpMaintenanceProfile",
                "shopPortalProfile",
                "environmentRoot",
                "warpTransitionPresenter",
                "debrisStream",
                "shopPortalRoot");
            Require(
                runtimeData.FindProperty("externalThreatScheduler") == null,
                "map_runtime_legacy_scheduler_reference_present");
            ValidateCanonicalMapPresentation(scene);

            var debrisStream = RequireOne<PHSRandomDebrisStream>(scene, "map_debris_stream");
            var debrisRoots = new SerializedObject(debrisStream).FindProperty("debrisRoots");
            Require(debrisRoots != null && debrisRoots.isArray && debrisRoots.arraySize > 0, "map_debris_roots_missing");
            for (var index = 0; index < debrisRoots.arraySize; index++)
            {
                var seed = debrisRoots.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                var item = seed?.GetComponent<UtilityItemObject>();
                Require(
                    seed != null && item != null && item.ItemData != null && item.ItemData.DroppedPrefab != null,
                    $"map_debris_source_invalid:{index}");
            }
        }

        private static void ValidateCanonicalMapPresentation(Scene mapScene)
        {
            var shipMap = RequireOne<PHSShipMapWorldLayout>(mapScene, "map_ship_layout");
            var shipMapData = new SerializedObject(shipMap);
            RequireReferences(shipMapData, "mapRenderRig");
            var anchorReferences = shipMapData.FindProperty("objectAnchors");
            Require(
                anchorReferences != null && anchorReferences.isArray && anchorReferences.arraySize >= 6,
                "map_object_anchors_missing");
            if (anchorReferences == null || !anchorReferences.isArray)
            {
                return;
            }

            var anchors = new List<PHSShipMapObjectAnchor>(anchorReferences.arraySize);
            for (var index = 0; index < anchorReferences.arraySize; index++)
            {
                var anchor = anchorReferences.GetArrayElementAtIndex(index).objectReferenceValue
                    as PHSShipMapObjectAnchor;
                Require(anchor != null && anchor.TryValidate(out _), $"map_object_anchor_invalid:{index}");
                if (anchor != null)
                {
                    anchors.Add(anchor);
                }
            }

            var vendingAnchors = anchors
                .Where(anchor => anchor.Kind == ShipMapObjectKind.Vending)
                .ToArray();
            Require(vendingAnchors.Length == 6, $"map_vending_anchor_count:{vendingAnchors.Length}");
            Require(
                vendingAnchors.Count(anchor => anchor.IconId == ShipMapIconId.Wrench) == 2,
                "map_vending_wrench_count_invalid");
            Require(
                vendingAnchors.Count(anchor => anchor.IconId == ShipMapIconId.FireExtinguisher) == 2,
                "map_vending_extinguisher_count_invalid");
            Require(
                vendingAnchors.Count(anchor => anchor.IconId == ShipMapIconId.Battery) == 2,
                "map_vending_battery_count_invalid");
            foreach (var anchor in vendingAnchors)
            {
                var vending = anchor.GetComponentInParent<UtilityVendingMachineInteractable>()
                    ?? anchor.GetComponentInChildren<UtilityVendingMachineInteractable>(true);
                var itemId = vending?.VendingMachineData?.ItemPrefabData?.ItemId;
                var expectedIcon = itemId switch
                {
                    "wrench" => ShipMapIconId.Wrench,
                    "fire_extinguisher" => ShipMapIconId.FireExtinguisher,
                    "battery_pack" => ShipMapIconId.Battery,
                    _ => ShipMapIconId.None
                };
                Require(expectedIcon != ShipMapIconId.None, $"map_vending_item_invalid:{anchor.name}");
                Require(anchor.IconId == expectedIcon, $"map_vending_icon_mismatch:{anchor.name}");
                Require(
                    vending != null
                    && (anchor.transform.position - vending.transform.position).sqrMagnitude <= 0.0001f,
                    $"map_vending_marker_position_mismatch:{anchor.name}");
            }

            var rig = RequireOne<PHSShipMapRenderRig>(mapScene, "map_render_rig");
            var rigData = new SerializedObject(rig);
            RequireReferences(rigData, "mapCamera", "mapTexture", "schematicRoot");
            var mapCamera = rigData.FindProperty("mapCamera")?.objectReferenceValue as Camera;
            var mapTexture = rigData.FindProperty("mapTexture")?.objectReferenceValue as RenderTexture;
            Require(
                mapCamera != null
                && mapTexture != null
                && mapTexture.width == 240
                && mapTexture.height == 720
                && mapCamera.orthographic
                && mapCamera.targetTexture == mapTexture,
                "map_render_rig_projection_contract_invalid");
        }

        private static NetworkEventCoordinator ValidatePersistentTeamEventAuthority(Scene mapScene)
        {
            Require(
                FindSceneComponents<EventManager>(mapScene).Length == 0,
                $"map_event_manager_count:{FindSceneComponents<EventManager>(mapScene).Length}");
            Require(
                FindSceneComponents<NetworkEventCoordinator>(mapScene).Length == 0,
                $"map_event_coordinator_count:{FindSceneComponents<NetworkEventCoordinator>(mapScene).Length}");
            Require(
                FindSceneComponents<PHSNetworkEventScheduler>(mapScene).Length == 0,
                $"map_event_scheduler_count:{FindSceneComponents<PHSNetworkEventScheduler>(mapScene).Length}");

            var runRoot = AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPrefabPath);
            Require(runRoot != null, "persistent_event_run_root_missing");
            var sessionRoot = runRoot.GetComponent<NetworkRunSessionRoot>();
            Require(sessionRoot != null, "persistent_event_session_root_missing");

            var coordinators = runRoot.GetComponentsInChildren<NetworkEventCoordinator>(true);
            var schedulers = runRoot.GetComponentsInChildren<PHSNetworkEventScheduler>(true);
            Require(coordinators.Length == 1, $"persistent_event_coordinator_count:{coordinators.Length}");
            Require(schedulers.Length == 1, $"persistent_event_scheduler_count:{schedulers.Length}");
            if (coordinators.Length != 1 || schedulers.Length != 1 || sessionRoot == null)
            {
                return null;
            }

            var coordinator = coordinators[0];
            var scheduler = schedulers[0];
            Require(
                coordinator.gameObject == runRoot
                    && coordinator.GetComponent<NetworkObject>() == runRoot.GetComponent<NetworkObject>(),
                "persistent_event_coordinator_not_on_session_root");
            Require(
                runRoot.GetComponent<RoomRegistry>() != null
                    && runRoot.GetComponentsInChildren<RoomRegistry>(true).Length == 1,
                "persistent_event_room_registry_not_root_owned");
            Require(
                new SerializedObject(sessionRoot).FindProperty("eventCoordinator")?.objectReferenceValue == coordinator,
                "persistent_event_session_root_coordinator_mismatch");
            Require(
                new SerializedObject(sessionRoot).FindProperty("eventScheduler")?.objectReferenceValue == scheduler,
                "persistent_event_session_root_scheduler_mismatch");

            var coordinatorData = new SerializedObject(coordinator);
            RequireReferences(
                coordinatorData,
                "eventManager",
                "eventScheduler",
                "roomRegistry",
                "effectMirrorPresenter");
            Require(
                coordinatorData.FindProperty("eventScheduler")?.objectReferenceValue == scheduler,
                "persistent_event_scheduler_mismatch");
            Require(
                coordinatorData.FindProperty("roomRegistry")?.objectReferenceValue
                    == runRoot.GetComponent<RoomRegistry>(),
                "persistent_event_room_registry_mismatch");
            var presenter = coordinatorData.FindProperty("effectMirrorPresenter")?.objectReferenceValue
                as NetworkEventEffectMirrorPresenter;
            Require(presenter != null && presenter.ValidateConfiguration(), "persistent_event_presenter_invalid");
            var micVoicePresenter = runRoot.GetComponentInChildren<MicDestroyVoiceEffectPresenter>(true);
            Require(
                micVoicePresenter != null
                    && new SerializedObject(micVoicePresenter).FindProperty("eventCoordinator")?.objectReferenceValue
                        == coordinator,
                "persistent_event_mic_voice_presenter_mismatch");
            return coordinator;
        }

        private static void ValidateCanonicalEnemySpawnRuntime(
            Scene mapScene,
            NetworkEventCoordinator coordinator)
        {
            Require(
                FindSceneComponents<EnemySpawnSetting>(mapScene).Length == 0,
                $"map_legacy_enemy_spawn_setting_count:{FindSceneComponents<EnemySpawnSetting>(mapScene).Length}");
            Require(coordinator != null, "enemy_spawn_persistent_coordinator_missing");

            var coordinatorData = new SerializedObject(coordinator);
            var eventManager = coordinatorData.FindProperty("eventManager")?.objectReferenceValue
                as EventManager;
            Require(eventManager != null, "enemy_spawn_event_manager_missing");
            if (eventManager == null)
            {
                return;
            }

            var registry = new SerializedObject(eventManager).FindProperty("registry")?.objectReferenceValue
                as EventRegistrySO;
            Require(registry != null, "enemy_spawn_event_registry_missing");
            var enemySpawnData = registry?.GetData(EventId.EnemySpawn) as EnemySpawnDataSO;
            Require(enemySpawnData != null, "enemy_spawn_registry_entry_missing");
            Require(
                enemySpawnData != null
                && enemySpawnData.playerAttackEnemyPrefab != null
                && enemySpawnData.deviceAttackEnemyPrefab != null,
                "enemy_spawn_pool_prefab_references_missing");
            if (enemySpawnData != null)
            {
                Require(
                    enemySpawnData.playerAttackEnemyPrefab.GetComponentInChildren<EnemyBase>(true) != null,
                    "enemy_spawn_player_pool_prefab_invalid");
                Require(
                    enemySpawnData.deviceAttackEnemyPrefab.GetComponentInChildren<EnemyBase>(true) != null,
                    "enemy_spawn_device_pool_prefab_invalid");
            }

            var spawnConfigs = FindSceneComponents<ShipSpawnPointConfig>(mapScene);
            Require(spawnConfigs.Length == 1, $"enemy_spawn_point_config_count:{spawnConfigs.Length}");
            if (spawnConfigs.Length != 1)
            {
                return;
            }

            var spawnPoints = new SerializedObject(spawnConfigs[0]).FindProperty("spawnPoints");
            Require(
                spawnPoints != null && spawnPoints.isArray && spawnPoints.arraySize > 0,
                "enemy_spawn_point_references_missing");
            if (spawnPoints == null || !spawnPoints.isArray)
            {
                return;
            }

            for (var index = 0; index < spawnPoints.arraySize; index++)
            {
                var spawnPoint = spawnPoints.GetArrayElementAtIndex(index).objectReferenceValue
                    as ShipSpawnPoint;
                Require(
                    spawnPoint != null && spawnPoint.gameObject.scene == mapScene,
                    $"enemy_spawn_point_reference_invalid:{index}");
            }
        }

        private static T RequireOne<T>(Scene scene, string label) where T : Component
        {
            var matches = FindSceneComponents<T>(scene);
            Require(matches.Length == 1, $"{label}_count:{matches.Length}");
            return matches[0];
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
            foreach (Transform child in root)
            {
                var match = FindChild(child, name);
                if (match != null) return match;
            }

            return null;
        }

        private static bool RectanglesTouch(
            RectTransform left,
            RectTransform right,
            float tolerance)
        {
            if (left == null || right == null) return false;
            var leftRect = new Rect(
                left.anchoredPosition - left.rect.size * 0.5f,
                left.rect.size);
            var rightRect = new Rect(
                right.anchoredPosition - right.rect.size * 0.5f,
                right.rect.size);
            return leftRect.xMin <= rightRect.xMax + tolerance
                && leftRect.xMax + tolerance >= rightRect.xMin
                && leftRect.yMin <= rightRect.yMax + tolerance
                && leftRect.yMax + tolerance >= rightRect.yMin;
        }

        private static bool EnumArrayContains(SerializedProperty property, int value)
        {
            if (property == null || !property.isArray) return false;
            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).enumValueIndex == value) return true;
            }

            return false;
        }

        private static bool IntArrayContains(SerializedProperty property, int value)
        {
            if (property == null || !property.isArray) return false;
            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).intValue == value) return true;
            }

            return false;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void RequireArrayReferences(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            Require(
                property != null && property.isArray && property.arraySize > 0,
                $"inspector_array_missing:{serialized.targetObject.name}:{propertyName}");
            for (var index = 0; index < property.arraySize; index++)
            {
                Require(
                    property.GetArrayElementAtIndex(index).objectReferenceValue != null,
                    $"inspector_array_reference_missing:{serialized.targetObject.name}:{propertyName}:{index}");
            }
        }

        private static void RequireReferences(SerializedObject serialized, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var property = serialized.FindProperty(propertyName);
                Require(
                    property != null && property.objectReferenceValue != null,
                    $"inspector_reference_missing:{serialized.targetObject.name}:{propertyName}");
            }
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_INTEGRATED_RELEASE_VALIDATION_FAILED reason={reason}");
            }
        }
    }
}
