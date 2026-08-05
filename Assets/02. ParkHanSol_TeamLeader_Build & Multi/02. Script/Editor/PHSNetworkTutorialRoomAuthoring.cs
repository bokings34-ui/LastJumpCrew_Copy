using System;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkTutorialRoomAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string SequenceRootName =
            "PHS_NetworkTutorialRoomSequence";
        private const string GameplayContextRootName =
            "PHS_NetworkTutorialGameplayContext";
        private const string DoorPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialDoor.prefab";
        private const string WallPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialWall.prefab";
        private const string WrenchPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_Wrench.prefab";
        private const string BatteryPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_FuturisticBatteryPack.prefab";
        private const string FireExtinguisherPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_FireExtinguisher.prefab";
        private const string LegacyDroppedWrenchPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_Wrench_Dropped.prefab";
        private const string LegacyDroppedBatteryPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Imported/ParkHanSol_BatteryPack_Dropped.prefab";
        private const string InteractionStationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialInteractionStation.prefab";
        private const string MiniGameRuntimePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_MiniGameRuntimeSystem.prefab";
        private const string WireTerminalVisualPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Art/ParkHanSol_FuturisticCableRouterDevice_Art.prefab";
        private const string PowerTerminalVisualPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Props/Prefabs/Tripo/ParkHanSol_Tripo_power_station.prefab";
        private const string TeamTutorialMapPrefabPath =
            "Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/Tutorial_Map.prefab";
        private const string GrappleAnchorPrefabPath =
            "Assets/05. TakHyunJae_Map & MiniGame/06. MyAsset/Creepy_Cat/3D Scifi Kit Vol 3/Prefabs/Props/Update 1.00-First build/Things/P_Light_Ring_01.prefab";
        private const string GrappleAnchorMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Environment/Grapple/PHS_GrappleAnchor_Test.mat";
        private const string FloorObjectivePadPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Customization/Visuals/PHS_back_circle.prefab";
        private const string FloorObjectivePadMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Integration/PHS_WarpSafeZone.mat";
        private const string DirectionLineMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Integration/PHS_TutorialDirectionLine.mat";
        private const string ObjectiveLightPillarPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_TutorialObjectiveLightPillar.prefab";
        private const string DebrisCargoPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Debris/PHS_Debris_FuturisticCargo.prefab";
        private const string DebrisCameraPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Debris/PHS_Debris_SatelliteCamera.prefab";
        private const string DebrisSellStationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/ShopCheckoutCounter/PHS_DebrisSellStation.prefab";
        private const string GameCorePrefabPath =
            "Assets/03. SeoBoGyeong_Game Economy/03. Prefab/GameCore.prefab";
        private const string TutorialSkyboxMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Environment/Tutorial/PHS_NetworkTutorialSpaceSkybox.mat";
        private const string BriefingRenderTexturePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_TutorialBriefing.renderTexture";
        private const string InstructionFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/TutorialInstructions/";
        private const string TutorialKeycapSpritePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Art/Tutorial/PHS_Tutorial_Keycap_Orange.png";
        private const string TutorialFontPath = PHSUIFontPaths.SuitRegular;
        private const string ObjectiveBodyFontPath = PHSUIFontPaths.SuitMedium;
        private const string ObjectiveNumberFontPath =
            PHSUIFontPaths.SuitSemiBold;
        private const int EnvironmentLastSegmentIndex = 15;
        private const float EnvironmentModuleSize = 3.6f;
        private const float TutorialEndCapZ = 54.27f;
        private static readonly Vector3 TeamMapAlignedPosition =
            new(22.84495f, -8.76f, 26.95981f);
        private static readonly Vector3 TutorialStartPosition =
            new(0f, -0.846f, 50f);
        private static readonly Quaternion TutorialStartRotation =
            Quaternion.Euler(0f, 180f, 0f);
        private static readonly Vector3 ExteriorEntryPosition =
            new(-53.75f, -0.253f, 8.15f);
        private static readonly Vector3 ToolUseExitDoorAnchor =
            new(-22.75f, -0.53f, 0.51f);

        private static readonly RoomSpec[] Specs =
        {
            new(
                "01_MoveJump",
                new[]
                {
                    TutorialActionKind.Move
                },
                4.5f,
                null,
                "이동과 점프",
                new[]
                {
                    "[WASD]  1번 지점으로 이동",
                    "[SPACE]  2번 지점 점프"
                },
                "PHS_Tutorial_Move.png"),
            new(
                "02_InteriorMovement",
                new[]
                {
                    TutorialActionKind.Move,
                    TutorialActionKind.Move
                },
                13.5f,
                null,
                "함선 내부 이동",
                new[]
                {
                    "[WASD]  1번 지점으로 이동",
                    "[WASD]  2번 지점으로 이동"
                },
                "PHS_Tutorial_Jump.png"),
            new(
                "03_Grapple",
                new[]
                {
                    TutorialActionKind.Grapple,
                    TutorialActionKind.Grapple
                },
                22.5f,
                new Vector3(-2.75f, -0.53f, 6.51f),
                "그래플 이동",
                new[]
                {
                    "[Q] 누른 채 1번 고정점 연결",
                    "[Q] 놓고 2번 고정점 연결"
                },
                "PHS_Tutorial_Grapple.png"),
            new(
                "04_ItemTransfer",
                new[]
                {
                    TutorialActionKind.Pickup,
                    TutorialActionKind.Drop,
                    TutorialActionKind.Pickup,
                    TutorialActionKind.Drop
                },
                31.5f,
                new Vector3(-16.75f, -0.53f, 6.51f),
                "아이템 운반",
                new[]
                {
                    "[F] 렌치 줍기 · [RMB] 내려놓기",
                    "[F] 배터리 줍기 · [RMB] 내려놓기"
                },
                "PHS_Tutorial_PickupDrop.png"),
            new(
                "05_ToolUse",
                new[]
                {
                    TutorialActionKind.Pickup,
                    TutorialActionKind.Use,
                    TutorialActionKind.Swap,
                    TutorialActionKind.Use
                },
                40.5f,
                ToolUseExitDoorAnchor,
                "도구 사용",
                new[]
                {
                    "[F] 렌치 줍기 · [LMB] 사용",
                    "[F] 소화기 교체 · [LMB] 사용"
                },
                "PHS_Tutorial_Swap.png"),
            new(
                "06_TrainingTerminals",
                new[]
                {
                    TutorialActionKind.Interaction,
                    TutorialActionKind.Interaction
                },
                49.5f,
                new Vector3(-56.75f, -0.53f, 6.51f),
                "연습 단말기",
                new[]
                {
                    "[F]  1번 단말기 작동",
                    "[F]  2번 단말기 작동"
                },
                "PHS_Tutorial_Interact.png"),
            new(
                "07_ExteriorDebris",
                new[]
                {
                    TutorialActionKind.Thruster,
                    TutorialActionKind.Drop
                },
                170f,
                null,
                "외부 무중력과 데브리 회수",
                new[]
                {
                    "[WASD] 이동  ·  [SHIFT] 위  ·  [CTRL] 아래",
                    "[F] 데브리 줍기  ·  [RMB] 패드에 놓기"
                },
                "PHS_Tutorial_Thruster.png"),
            new(
                "08_BoardShip",
                new[]
                {
                    TutorialActionKind.Thruster,
                    TutorialActionKind.Interaction
                },
                220f,
                null,
                "외부 함선 이동과 타기",
                new[]
                {
                    "[WASD] 함선 이동  ·  [SHIFT] 위  ·  [CTRL] 아래",
                    "[F]  함선 탑승"
                },
                "PHS_Tutorial_Interact.png")
        };

        private static readonly string[][] ObjectiveMarkerCaptions =
        {
            new[] { "이동 지점", "점프 지점" },
            new[] { "내부 이동 1", "내부 이동 2" },
            new[] { "후크 고정점", "후크 고정점" },
            new[] { "렌치", "배터리" },
            new[] { "렌치", "소화기" },
            new[] { "1번 단말기", "2번 단말기" },
            new[] { "외부 진입", "데브리 회수" },
            new[] { "함선 앞", "함선 문" }
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Tutorial Rooms")]
        public static void Author()
        {
            RequireAssets();
            ImportInstructionSprites();
            var previousActive = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var sceneWasLoaded = scene.IsValid() && scene.isLoaded;
            if (!sceneWasLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);
            try
            {
                var oldRoot = FindNamedRoot(scene, SequenceRootName);
                var practiceItemsLocalPosition = Vector3.zero;
                if (oldRoot != null)
                {
                    var oldPracticeItems = oldRoot.transform.Find(
                        "PHS_TutorialPracticeItems");
                    if (oldPracticeItems != null)
                    {
                        practiceItemsLocalPosition =
                            oldPracticeItems.localPosition;
                    }

                    UnityEngine.Object.DestroyImmediate(oldRoot);
                }

                var oldGameCore = FindNamedRoot(scene, "PHS_TutorialGameCore");
                if (oldGameCore != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldGameCore);
                }

                var sequenceRoot = new GameObject(SequenceRootName);
                SceneManager.MoveGameObjectToScene(sequenceRoot, scene);
                CreateTutorialGameCore(scene);
                EnsureGameplaySceneContext(scene);
                var teamMap = InstantiateTeamMap(
                    scene,
                    sequenceRoot.transform);
                ConfigureTutorialShipCollider(teamMap.transform);
                DisableLegacyEnvironment(scene);
                var player = FindComponent<NetworkPlayerController>(scene);
                player.transform.SetPositionAndRotation(
                    TutorialStartPosition,
                    TutorialStartRotation);
                EditorUtility.SetDirty(player.transform);
                var briefingPresenter = CreateBriefingPresenter(
                    scene,
                    sequenceRoot.transform,
                    player);
                var rooms = new NetworkTutorialRoomController[Specs.Length];
                var usedDoors = new System.Collections.Generic.HashSet<
                    DoorDoubleSlide>();
                for (var index = 0; index < Specs.Length; index++)
                {
                    rooms[index] = CreateRoom(
                        scene,
                        sequenceRoot.transform,
                        teamMap.transform,
                        usedDoors,
                        briefingPresenter,
                        Specs[index],
                        index);
                }

                RemoveLegacyPracticeItems(scene);
                CreatePracticeItems(
                    scene,
                    sequenceRoot.transform,
                    practiceItemsLocalPosition);
                RepositionPracticeVolumes(
                    scene,
                    sequenceRoot.transform,
                    teamMap.transform);
                var exterior = ConfigureGravityAndDebris(
                    scene,
                    sequenceRoot.transform,
                    teamMap.transform,
                    player);
                CreatePlayAreaBoundary(
                    scene,
                    sequenceRoot.transform,
                    exterior.PlayAreaBounds,
                    ExteriorEntryPosition,
                    exterior.BoardingPosition);
                var interactionStations = CreateInteractionStations(
                    scene,
                    sequenceRoot.transform,
                    exterior.BoardingPosition,
                    exterior.BoardingParent);
                WireDirector(scene, rooms);
                CreateAndWireRoomObjectives(
                    scene,
                    sequenceRoot.transform,
                    rooms,
                    interactionStations,
                    exterior);
                BindTutorialHud(scene);
                ConfigureTutorialOnlyHud(scene);
                ConfigureTutorialSkybox();
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw Failure("scene_save_failed");
                }

                Debug.Log(
                    "PHS_NETWORK_TUTORIAL_ROOMS_AUTHORING_OK " +
                    $"scene={ScenePath} rooms={rooms.Length} mode=composite_goals");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                if (!sceneWasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void EnsureGameplaySceneContext(Scene scene)
        {
            var contextRoot = FindNamedRoot(scene, GameplayContextRootName);
            if (contextRoot == null)
            {
                contextRoot = new GameObject(GameplayContextRootName);
                SceneManager.MoveGameObjectToScene(contextRoot, scene);
            }

            var spawnPointsRoot = contextRoot.transform.Find("Spawn Points");
            if (spawnPointsRoot == null)
            {
                spawnPointsRoot = new GameObject("Spawn Points").transform;
                spawnPointsRoot.SetParent(contextRoot.transform, false);
            }

            var spawnPoint = spawnPointsRoot.Find("Spawn_01");
            if (spawnPoint == null)
            {
                spawnPoint = new GameObject("Spawn_01").transform;
                spawnPoint.SetParent(spawnPointsRoot, false);
            }

            var respawnPoint = contextRoot.transform.Find("Respawn Point");
            if (respawnPoint == null)
            {
                respawnPoint = new GameObject("Respawn Point").transform;
                respawnPoint.SetParent(contextRoot.transform, false);
            }

            contextRoot.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            spawnPointsRoot.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            spawnPoint.SetLocalPositionAndRotation(
                TutorialStartPosition,
                TutorialStartRotation);
            respawnPoint.SetLocalPositionAndRotation(
                TutorialStartPosition,
                TutorialStartRotation);

            var context = contextRoot.GetComponent<GameplaySceneContext>();
            if (context == null)
            {
                context = contextRoot.AddComponent<GameplaySceneContext>();
            }

            var serialized = new SerializedObject(context);
            serialized.FindProperty("spawnPointsRoot").objectReferenceValue =
                spawnPointsRoot;
            serialized.FindProperty("respawnPoint").objectReferenceValue =
                respawnPoint;
            serialized.FindProperty("isGameplayScene").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(contextRoot);
            EditorUtility.SetDirty(context);
        }

        private static GameObject InstantiateTeamMap(
            Scene scene,
            Transform parent)
        {
            var map = InstantiatePrefab(
                TeamTutorialMapPrefabPath,
                scene,
                parent);
            map.name = "PHS_TeamTutorialMap";
            map.transform.position = TeamMapAlignedPosition;
            map.transform.rotation = Quaternion.identity;
            map.transform.localScale = Vector3.one;
            PrefabUtility.UnpackPrefabInstance(
                map,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            return map;
        }

        private static void ConfigureTutorialShipCollider(Transform teamMap)
        {
            var shipBody = FindNamedUnder(teamMap, "Cruiser_Body_02");
            var shipCollider = shipBody.GetComponent<MeshCollider>();
            if (shipCollider == null ||
                shipBody.GetComponentInParent<Rigidbody>() != null)
            {
                throw Failure("tutorial_ship_collider_contract_invalid");
            }

            shipCollider.convex = false;
            EditorUtility.SetDirty(shipCollider);
        }

        private static void DisableLegacyEnvironment(Scene scene)
        {
            foreach (var name in new[]
                     {
                         "PHS_NetworkTutorialEnvironment",
                         "PHS_TutorialInteriorShell"
                     })
            {
                var legacy = FindNamedOptional(scene, name);
                if (legacy == null)
                {
                    continue;
                }

                legacy.SetActive(false);
                EditorUtility.SetDirty(legacy);
            }
        }

        private static NetworkTutorialBriefingPresenter
            CreateBriefingPresenter(
                Scene scene,
                Transform parent,
                NetworkPlayerController player)
        {
            var root = new GameObject(
                "PHS_TutorialBriefing",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(NetworkTutorialBriefingPresenter));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            ConfigureCanvasScaler(root.GetComponent<CanvasScaler>());
            var canvasRect = root.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var popup = CreateRect(
                "Popup",
                root.transform,
                Vector2.zero,
                Vector2.one);
            var canvasGroup = popup.AddComponent<CanvasGroup>();
            var dimmer = popup.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.84f);
            var card = CreateUiImage(
                "Card",
                popup.transform,
                new Vector2(0.24f, 0.25f),
                new Vector2(0.76f, 0.75f));
            card.color = new Color(0.004f, 0.005f, 0.006f, 0.985f);
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(1f, 0.36f, 0.12f, 0.55f);
            cardOutline.effectDistance = new Vector2(2f, -2f);
            cardOutline.useGraphicAlpha = false;
            var accent = CreateUiImage(
                "Accent",
                card.transform,
                new Vector2(0.06f, 0.94f),
                new Vector2(0.22f, 0.955f));
            accent.color = new Color(1f, 0.36f, 0.12f, 1f);

            var title = CreateUiText(
                "Title",
                card.transform,
                new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.95f),
                38f);
            ApplyTutorialFont(title, ObjectiveNumberFontPath);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = new Color(1f, 0.36f, 0.12f, 1f);
            title.fontStyle = FontStyles.Bold;
            title.fontWeight = FontWeight.Bold;
            var body = CreateUiText(
                "Body",
                card.transform,
                new Vector2(0.08f, 0.24f),
                new Vector2(0.92f, 0.8f),
                25f);
            ApplyTutorialFont(body, ObjectiveBodyFontPath);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(0.90f, 0.92f, 0.95f, 1f);
            body.fontStyle = FontStyles.Normal;
            body.fontWeight = FontWeight.Regular;

            var videoRoot = CreateRect(
                "VideoRoot",
                card.transform,
                new Vector2(0.08f, 0.24f),
                new Vector2(0.92f, 0.78f));
            var videoImageObject = CreateRect(
                "VideoImage",
                videoRoot.transform,
                Vector2.zero,
                Vector2.one);
            var videoImage = videoImageObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
            var videoAspect = videoImageObject.AddComponent<AspectRatioFitter>();
            videoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            videoAspect.aspectRatio = 16f / 9f;
            var videoPlayer = videoRoot.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoRoot.SetActive(false);

            var previous = CreateBriefingButton(
                "Previous",
                card.transform,
                new Vector2(0.06f, 0.06f),
                new Vector2(0.22f, 0.17f),
                "\uC774\uC804",
                out _);
            var indicator = CreateUiText(
                "PageIndicator",
                card.transform,
                new Vector2(0.36f, 0.06f),
                new Vector2(0.64f, 0.17f),
                24f);
            ApplyTutorialFont(indicator, ObjectiveBodyFontPath);
            indicator.color = new Color(0.7f, 0.75f, 0.8f, 1f);
            var next = CreateBriefingButton(
                "Next",
                card.transform,
                new Vector2(0.78f, 0.06f),
                new Vector2(0.94f, 0.17f),
                "\uB2E4\uC74C",
                out var nextLabel);

            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                BriefingRenderTexturePath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(960, 540, 0)
                {
                    name = "PHS_TutorialBriefing"
                };
                AssetDatabase.CreateAsset(
                    renderTexture,
                    BriefingRenderTexturePath);
            }

            var presenter = root.GetComponent<
                NetworkTutorialBriefingPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("popupRoot").objectReferenceValue = popup;
            serialized.FindProperty("canvasGroup").objectReferenceValue =
                canvasGroup;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("pageIndicatorText").objectReferenceValue =
                indicator;
            serialized.FindProperty("previousButton").objectReferenceValue =
                previous;
            serialized.FindProperty("nextButton").objectReferenceValue = next;
            serialized.FindProperty("nextButtonLabel").objectReferenceValue =
                nextLabel;
            serialized.FindProperty("videoRoot").objectReferenceValue =
                videoRoot;
            serialized.FindProperty("videoImage").objectReferenceValue =
                videoImage;
            serialized.FindProperty("videoPlayer").objectReferenceValue =
                videoPlayer;
            serialized.FindProperty("videoTexture").objectReferenceValue =
                renderTexture;
            serialized.FindProperty("playerController").objectReferenceValue =
                player;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static Button CreateBriefingButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            out TMP_Text labelText)
        {
            var root = CreateRect(name, parent, anchorMin, anchorMax);
            var image = root.AddComponent<Image>();
            image.color = new Color(1f, 0.36f, 0.12f, 1f);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            labelText = CreateUiText(
                "Label",
                root.transform,
                Vector2.zero,
                Vector2.one,
                30f);
            ApplyTutorialFont(labelText, ObjectiveNumberFontPath);
            labelText.color = new Color(0.015f, 0.02f, 0.03f, 1f);
            labelText.fontStyle = FontStyles.Bold;
            labelText.fontWeight = FontWeight.Bold;
            labelText.text = label;
            return button;
        }

        private static void ConfigureBriefingPages(
            SerializedProperty pages,
            RoomSpec spec)
        {
            var overview = spec.Id switch
            {
                "01_MoveJump" =>
                    "첫 구역에서 기본 이동 감각을 확인합니다.\n\n" +
                    "앞의 빛기둥이 진행 지점입니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "02_InteriorMovement" =>
                    "이 구역은 함선 내부입니다. 플레이어와 놓인 물건 모두 내부 중력을 받습니다.\n\n" +
                    "표시된 경로를 따라 중력 상태를 확인합니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "03_Grapple" =>
                    "고정점을 화면 중앙에 두고 [Q]를 누르면 줄이 연결됩니다. [Q]를 누른 동안 연결이 유지됩니다.\n\n" +
                    "[Q]를 놓으면 줄이 해제됩니다. 첫 고정점 해제 후 다음 고정점에 다시 연결합니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "04_ItemTransfer" =>
                    "물건을 새로 집으면 손에 든 물건이 자동으로 교체됩니다.\n\n" +
                    "짧게 내려놓기와 길게 눌러 던지기를 구분합니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "05_ToolUse" =>
                    "도구도 새로 집으면 손의 장비가 자동으로 교체됩니다.\n\n" +
                    "렌치와 소화기의 사용 반응 차이를 확인합니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "06_TrainingTerminals" =>
                    "전선 연결과 전력 동기화 미니게임을 차례로 연습합니다.\n\n" +
                    "단말기 화면이 열리면 각 규칙에 맞춰 완료합니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "07_ExteriorDebris" =>
                    "외부 구역에서는 무중력 이동을 사용합니다. 마우스와 [WASD]로 방향을 잡고 [SHIFT]로 위로, [CTRL]로 아래로 움직입니다.\n\n" +
                    "데브리는 [F]로 줍습니다. [RMB]를 눌러서 내리거나 길게 누른 뒤 놓아 던집니다. 수거 유닛에 넣은 데브리는 크레딧이 됩니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                "08_BoardShip" =>
                    "앞의 함선까지 방금 익힌 무중력 이동을 이어 갑니다.\n\n" +
                    "선체 앞 상호작용 지점에서 탑승하면 튜토리얼이 끝납니다.\n\n" +
                    "화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                _ => throw Failure($"briefing_text_missing room={spec.Id}")
            };
            if (spec.Id == "01_MoveJump")
            {
                overview =
                    "첫 구역에서 기본 이동 감각을 확인합니다.\n\n" +
                    "앞의 빛기둥이 진행 지점이며 점프 입력은 검사하지 않습니다.";
            }
            overview = overview.Replace(
                "\n\n화면의 < > 버튼 또는 키보드 좌우 방향키로 앞뒤 설명을 다시 확인할 수 있습니다.",
                string.Empty);
            var firstParagraphEnd = overview.IndexOf(
                "\n\n",
                StringComparison.Ordinal);
            overview = firstParagraphEnd < 0
                ? $"<b>{overview}</b>"
                : $"<b>{overview.Substring(0, firstParagraphEnd)}</b>" +
                  overview.Substring(firstParagraphEnd);
            pages.arraySize = 1;
            SetBriefingPage(
                pages.GetArrayElementAtIndex(0),
                spec.RoomTitle,
                overview);
        }

        private static void SetBriefingPage(
            SerializedProperty page,
            string title,
            string body)
        {
            page.FindPropertyRelative("pageKind").enumValueIndex =
                (int)TutorialBriefingPageKind.Text;
            page.FindPropertyRelative("title").stringValue = title;
            page.FindPropertyRelative("body").stringValue = body;
            page.FindPropertyRelative("videoClip").objectReferenceValue = null;
        }

        private static NetworkTutorialRoomController CreateRoom(
            Scene scene,
            Transform sequenceRoot,
            Transform teamMap,
            System.Collections.Generic.HashSet<DoorDoubleSlide> usedDoors,
            NetworkTutorialBriefingPresenter briefingPresenter,
            RoomSpec spec,
            int index)
        {
            var roomObject = new GameObject($"PHS_TutorialRoom_{spec.Id}");
            SceneManager.MoveGameObjectToScene(roomObject, scene);
            roomObject.transform.SetParent(sequenceRoot, false);
            var room = roomObject.AddComponent<NetworkTutorialRoomController>();
            var guidanceRoot = new GameObject("ObjectiveGuidance");
            SceneManager.MoveGameObjectToScene(guidanceRoot, scene);
            guidanceRoot.transform.SetParent(roomObject.transform, false);
            guidanceRoot.SetActive(false);
            var poster = CreatePoster(
                scene,
                roomObject.transform,
                spec,
                out var instructionImage,
                out var instructionText,
                out var instructionKeyBadges,
                out var instructionKeyTexts,
                out var instructionCommandTexts,
                out var progressSlider,
                out var targetIndicatorText);
            var gate = CreateGate(
                scene,
                roomObject.transform,
                teamMap,
                usedDoors,
                spec.GateAnchor,
                index,
                out var doorVisual,
                out var doorSecondaryVisual,
                out var doorOpenLocalPosition,
                out var doorSecondaryOpenLocalPosition,
                out var blocker);

            var serialized = new SerializedObject(room);
            serialized.FindProperty("roomId").stringValue = spec.Id;
            serialized.FindProperty("requiredAction").enumValueIndex =
                (int)spec.Actions[0];
            serialized.FindProperty("requiredSuccessCount").intValue =
                spec.Actions.Length;
            var sequenceProperty = serialized.FindProperty(
                "requiredActionSequence");
            sequenceProperty.arraySize = spec.Actions.Length;
            for (var actionIndex = 0;
                 actionIndex < spec.Actions.Length;
                 actionIndex++)
            {
                sequenceProperty.GetArrayElementAtIndex(actionIndex)
                    .enumValueIndex = (int)spec.Actions[actionIndex];
            }
            serialized.FindProperty("roomRoot").objectReferenceValue =
                roomObject;
            serialized.FindProperty("manageRoomRootActiveState").boolValue =
                false;
            serialized.FindProperty("roomTitle").stringValue = spec.RoomTitle;
            var instructionProperty = serialized.FindProperty(
                "objectiveInstructions");
            instructionProperty.arraySize = spec.ObjectiveInstructions.Length;
            for (var instructionIndex = 0;
                 instructionIndex < spec.ObjectiveInstructions.Length;
                 instructionIndex++)
            {
                instructionProperty.GetArrayElementAtIndex(instructionIndex)
                    .stringValue = spec.ObjectiveInstructions[instructionIndex];
            }

            serialized.FindProperty("instructionRoot").objectReferenceValue =
                poster;
            serialized.FindProperty("instructionImage").objectReferenceValue =
                instructionImage;
            serialized.FindProperty("instructionSprite").objectReferenceValue =
                instructionImage.sprite;
            serialized.FindProperty("instructionText").objectReferenceValue =
                instructionText;
            SetObjectReferenceArray(
                serialized.FindProperty("instructionKeyBadges"),
                instructionKeyBadges);
            SetObjectReferenceArray(
                serialized.FindProperty("instructionKeyTexts"),
                instructionKeyTexts);
            SetObjectReferenceArray(
                serialized.FindProperty("instructionCommandTexts"),
                instructionCommandTexts);
            serialized.FindProperty("instructionProgressSlider")
                .objectReferenceValue = progressSlider;
            serialized.FindProperty("targetIndicatorText")
                .objectReferenceValue = targetIndicatorText;
            var player = FindComponent<NetworkPlayerController>(scene);
            var playerSerialized = new SerializedObject(player);
            var guidanceCamera = playerSerialized.FindProperty("playerCamera")
                .objectReferenceValue as Camera;
            if (guidanceCamera == null)
            {
                throw Failure("tutorial_guidance_camera_missing");
            }

            serialized.FindProperty("guidanceCamera").objectReferenceValue =
                guidanceCamera;
            serialized.FindProperty("objectiveGuidanceRoot")
                .objectReferenceValue = guidanceRoot;
            serialized.FindProperty("briefingPresenter").objectReferenceValue =
                briefingPresenter;
            ConfigureBriefingPages(
                serialized.FindProperty("briefingPages"),
                spec);
            serialized.FindProperty("doorTransform").objectReferenceValue =
                doorVisual;
            serialized.FindProperty("doorSecondaryTransform")
                .objectReferenceValue = doorSecondaryVisual;
            serialized.FindProperty("doorCollider").objectReferenceValue =
                blocker;
            serialized.FindProperty("doorOpenLocalPosition").vector3Value =
                doorOpenLocalPosition;
            serialized.FindProperty("doorSecondaryOpenLocalPosition")
                .vector3Value = doorSecondaryOpenLocalPosition;
            serialized.FindProperty("doorOpenLocalEulerAngles").vector3Value =
                doorVisual == null
                    ? Vector3.zero
                    : doorVisual.localEulerAngles;
            serialized.FindProperty("doorOpenDuration").floatValue = 0.65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            poster.name = $"InstructionPoster_{spec.Id}";
            if (gate != null)
            {
                gate.name = $"ExitGate_{spec.Id}";
            }
            return room;
        }

        private static GameObject CreatePoster(
            Scene scene,
            Transform parent,
            RoomSpec spec,
            out Image instructionImage,
            out TMP_Text instructionText,
            out Image[] instructionKeyBadges,
            out TMP_Text[] instructionKeyTexts,
            out TMP_Text[] instructionCommandTexts,
            out Slider progressSlider,
            out TMP_Text targetIndicatorText)
        {
            var canvasObject = new GameObject(
                "InstructionPoster",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var background = CreateUiImage(
                "PosterBackground",
                canvasObject.transform,
                new Vector2(0.10f, 0.06f),
                new Vector2(0.90f, 0.19f));
            background.color = new Color(0.004f, 0.005f, 0.006f, 0.90f);
            var backgroundOutline =
                background.gameObject.AddComponent<Outline>();
            backgroundOutline.effectColor =
                new Color(1f, 0.36f, 0.12f, 0.55f);
            backgroundOutline.effectDistance = new Vector2(2f, -2f);
            backgroundOutline.useGraphicAlpha = false;
            instructionImage = CreateUiImage(
                "ActionImage",
                background.transform,
                new Vector2(0.02f, 0.17f),
                new Vector2(0.28f, 0.94f));
            instructionImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                InstructionFolder + spec.SpriteFile);
            instructionImage.preserveAspect = true;
            instructionImage.color = Color.white;
            instructionImage.gameObject.SetActive(false);

            var commandRow = CreateRect(
                "CommandRow",
                background.transform,
                new Vector2(0f, 0.30f),
                Vector2.one);
            const int commandSlotCount = 3;
            const int keySlotsPerCommand = 4;
            instructionKeyBadges = new Image[
                commandSlotCount * keySlotsPerCommand];
            instructionKeyTexts = new TMP_Text[
                commandSlotCount * keySlotsPerCommand];
            instructionCommandTexts = new TMP_Text[commandSlotCount];
            var rowLayout = commandRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.spacing = 24f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var keyBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                TutorialKeycapSpritePath);
            if (keyBadgeSprite == null)
            {
                throw Failure("tutorial_key_badge_sprite_missing");
            }

            var lobbyOrange = new Color(1f, 0.36f, 0.12f, 1f);
            for (var slot = 0; slot < commandSlotCount; slot++)
            {
                var segment = CreateRect(
                    $"CommandSegment_{slot + 1}",
                    commandRow.transform,
                    Vector2.zero,
                    Vector2.one);
                var segmentLayout = segment.AddComponent<LayoutElement>();
                segmentLayout.preferredWidth = 300f;
                segmentLayout.preferredHeight = 76f;
                var segmentGroup =
                    segment.AddComponent<HorizontalLayoutGroup>();
                segmentGroup.childAlignment = TextAnchor.MiddleCenter;
                segmentGroup.spacing = 6f;
                segmentGroup.childControlWidth = true;
                segmentGroup.childControlHeight = true;
                segmentGroup.childForceExpandWidth = false;
                segmentGroup.childForceExpandHeight = false;

                for (var keySlot = 0;
                     keySlot < keySlotsPerCommand;
                     keySlot++)
                {
                    var badge = CreateUiImage(
                        $"KeyBadge_{keySlot + 1}",
                        segment.transform,
                        Vector2.zero,
                        Vector2.one);
                    badge.sprite = keyBadgeSprite;
                    badge.preserveAspect = true;
                    badge.color = Color.white;
                    badge.raycastTarget = false;
                    var badgeLayout =
                        badge.gameObject.AddComponent<LayoutElement>();
                    badgeLayout.preferredWidth = 64f;
                    badgeLayout.preferredHeight = 64f;

                    var keyText = CreateUiText(
                        "KeyText",
                        badge.transform,
                        Vector2.zero,
                        Vector2.one,
                        20f);
                    ApplyTutorialFont(keyText, ObjectiveNumberFontPath);
                    keyText.enableAutoSizing = true;
                    keyText.fontSizeMin = 8f;
                    keyText.fontSizeMax = 18f;
                    keyText.margin = new Vector4(8f, 4f, 8f, 4f);
                    keyText.textWrappingMode = TextWrappingModes.NoWrap;
                    keyText.fontStyle = FontStyles.Bold;
                    keyText.fontWeight = FontWeight.Bold;
                    keyText.color =
                        new Color(0.01f, 0.015f, 0.02f, 0.76f);
                    keyText.raycastTarget = false;

                    var keyIndex = slot * keySlotsPerCommand + keySlot;
                    instructionKeyBadges[keyIndex] = badge;
                    instructionKeyTexts[keyIndex] = keyText;
                }

                var commandText = CreateUiText(
                    "CommandText",
                    segment.transform,
                    Vector2.zero,
                    Vector2.one,
                    30f);
                ApplyTutorialFont(commandText, ObjectiveBodyFontPath);
                commandText.alignment = TextAlignmentOptions.MidlineLeft;
                commandText.enableAutoSizing = true;
                commandText.fontSizeMin = 14f;
                commandText.fontSizeMax = 30f;
                commandText.textWrappingMode = TextWrappingModes.NoWrap;
                commandText.fontStyle = FontStyles.Bold;
                commandText.fontWeight = FontWeight.Bold;
                commandText.color = new Color(0.90f, 0.92f, 0.95f, 1f);
                commandText.raycastTarget = false;
                AddTextOutline(commandText);
                var commandLayout =
                    commandText.gameObject.AddComponent<LayoutElement>();
                commandLayout.preferredWidth = 180f;
                commandLayout.preferredHeight = 72f;

                instructionCommandTexts[slot] = commandText;
                segment.SetActive(false);
            }

            instructionText = CreateUiText(
                "InstructionText",
                background.transform,
                Vector2.zero,
                new Vector2(1f, 0.28f),
                20f);
            ApplyTutorialFont(instructionText, ObjectiveBodyFontPath);
            instructionText.alignment = TextAlignmentOptions.Center;
            instructionText.enableAutoSizing = true;
            instructionText.fontSizeMin = 16f;
            instructionText.fontSizeMax = 20f;
            instructionText.color = lobbyOrange;
            AddTextOutline(instructionText);
            instructionText.text =
                $"{spec.RoomTitle}  0/{spec.ObjectiveInstructions.Length}";
            progressSlider = CreateProgressSlider(background.transform);
            progressSlider.gameObject.SetActive(false);

            var targetBackground = CreateUiImage(
                "TargetIndicatorBackground",
                canvasObject.transform,
                new Vector2(0.405f, 0.90f),
                new Vector2(0.595f, 0.96f));
            targetBackground.color = new Color(1f, 0.36f, 0.12f, 0.96f);
            var targetOutline =
                targetBackground.gameObject.AddComponent<Outline>();
            targetOutline.effectColor = Color.black;
            targetOutline.effectDistance = new Vector2(3f, -3f);
            targetOutline.useGraphicAlpha = false;
            targetIndicatorText = CreateUiText(
                "TargetIndicatorText",
                targetBackground.transform,
                Vector2.zero,
                Vector2.one,
                24f);
            ApplyTutorialFont(targetIndicatorText, ObjectiveNumberFontPath);
            targetIndicatorText.enableAutoSizing = true;
            targetIndicatorText.fontSizeMin = 16f;
            targetIndicatorText.fontSizeMax = 24f;
            targetIndicatorText.textWrappingMode = TextWrappingModes.NoWrap;
            targetIndicatorText.fontStyle = FontStyles.Bold;
            targetIndicatorText.fontWeight = FontWeight.Bold;
            targetIndicatorText.color = Color.black;
            targetIndicatorText.raycastTarget = false;
            targetBackground.gameObject.SetActive(false);
            canvasObject.SetActive(false);
            return canvasObject;
        }

        private static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void SetObjectReferenceArray(
            SerializedProperty property,
            UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static GameObject CreateGate(
            Scene scene,
            Transform parent,
            Transform teamMap,
            System.Collections.Generic.HashSet<DoorDoubleSlide> usedDoors,
            Vector3? gateAnchor,
            int index,
            out Transform doorVisual,
            out Transform doorSecondaryVisual,
            out Vector3 doorOpenLocalPosition,
            out Vector3 doorSecondaryOpenLocalPosition,
            out Collider blocker)
        {
            if (!gateAnchor.HasValue)
            {
                doorVisual = null;
                doorSecondaryVisual = null;
                doorOpenLocalPosition = Vector3.zero;
                doorSecondaryOpenLocalPosition = Vector3.zero;
                blocker = null;
                return null;
            }

            var gate = new GameObject($"Gate_{index + 1:00}");
            SceneManager.MoveGameObjectToScene(gate, scene);
            gate.transform.SetParent(parent, false);

            var anchor = gateAnchor.Value;
            var door = teamMap.GetComponentsInChildren<DoorDoubleSlide>(true)
                .Where(candidate => !usedDoors.Contains(candidate))
                .OrderBy(candidate =>
                    (candidate.transform.position - anchor).sqrMagnitude)
                .FirstOrDefault();
            if (door == null
                || Vector3.Distance(door.transform.position, anchor) > 8f)
            {
                throw Failure(
                    $"team_map_door_missing room={Specs[index].Id} anchor={anchor}");
            }

            usedDoors.Add(door);
            door.enabled = false;
            doorVisual = door.doorL;
            doorSecondaryVisual = door.doorR;
            if (doorVisual == null || doorSecondaryVisual == null)
            {
                throw Failure(
                    $"team_map_door_leaf_missing room={Specs[index].Id}");
            }

            var openDirection = door.directionType switch
            {
                DoorDoubleSlide.Direction.X => Vector3.right,
                DoorDoubleSlide.Direction.Y => Vector3.up,
                DoorDoubleSlide.Direction.Z => Vector3.back,
                _ => throw Failure(
                    $"team_map_door_direction_invalid room={Specs[index].Id}")
            };
            doorOpenLocalPosition = doorVisual.localPosition
                                    + openDirection * door.openDistance;
            doorSecondaryOpenLocalPosition =
                doorSecondaryVisual.localPosition
                - openDirection * door.openDistance;
            gate.transform.position = door.transform.position;
            gate.transform.rotation = door.transform.rotation;
            var renderers = door.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw Failure(
                    $"team_map_door_renderer_missing room={Specs[index].Id}");
            }

            var bounds = renderers[0].bounds;
            for (var rendererIndex = 1;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                bounds.Encapsulate(renderers[rendererIndex].bounds);
            }

            var box = gate.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = gate.transform.InverseTransformPoint(bounds.center);
            var localSize = gate.transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));
            var blockerSize = box.size;
            if (blockerSize.x <= blockerSize.y
                && blockerSize.x <= blockerSize.z)
            {
                blockerSize.x = 0.6f;
            }
            else if (blockerSize.y <= blockerSize.z)
            {
                blockerSize.y = 0.6f;
            }
            else
            {
                blockerSize.z = 0.6f;
            }

            box.size = blockerSize;
            blocker = box;
            return gate;
        }

        private static void CreateDoubleSidedWall(
            Scene scene,
            Transform parent,
            string name,
            float x,
            float y,
            Vector3 scale)
        {
            var front = InstantiatePrefab(WallPrefabPath, scene, parent);
            front.name = name + "_Front";
            front.transform.localPosition = new Vector3(x, y, 0f);
            front.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            front.transform.localScale = scale;

            var back = InstantiatePrefab(WallPrefabPath, scene, parent);
            back.name = name + "_Back";
            back.transform.localPosition = new Vector3(x, y, 0f);
            back.transform.localRotation = Quaternion.identity;
            back.transform.localScale = scale;
            foreach (var collider in back.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void CreateInteriorShell(Scene scene, Transform parent)
        {
            RepairEnvironmentSurfaces(scene);
            var shell = new GameObject("PHS_TutorialInteriorShell");
            SceneManager.MoveGameObjectToScene(shell, scene);
            shell.transform.SetParent(parent, false);
            for (var segment = 0;
                 segment <= EnvironmentLastSegmentIndex;
                 segment++)
            {
                for (var column = -1; column <= 1; column++)
                {
                    var ceilingName =
                        $"PHS_NetworkTutorialCeiling_{segment}_{column + 1}";
                    var ceiling = FindNamedOptional(scene, ceilingName)
                        ?? InstantiatePrefab(
                            WallPrefabPath,
                            scene,
                            shell.transform);
                    ceiling.name = ceilingName;
                    ceiling.transform.position = new Vector3(
                        column * EnvironmentModuleSize,
                        7.2f,
                        -1.8f + segment * EnvironmentModuleSize);
                    ceiling.transform.rotation = Quaternion.Euler(
                        90f,
                        0f,
                        0f);
                    ceiling.transform.localScale = Vector3.one * 0.9f;
                }
            }

            for (var row = 0; row <= 1; row++)
            {
                for (var column = -1; column <= 1; column++)
                {
                    var startWallName =
                        $"PHS_NetworkTutorialStartWall_{row}_{column + 1}";
                    var startWall = FindNamedOptional(scene, startWallName)
                        ?? InstantiatePrefab(
                            WallPrefabPath,
                            scene,
                            shell.transform);
                    startWall.name = startWallName;
                    startWall.transform.position = new Vector3(
                        column * EnvironmentModuleSize,
                        row * EnvironmentModuleSize,
                        -2.313f);
                    startWall.transform.rotation = Quaternion.identity;
                    startWall.transform.localScale = Vector3.one * 0.9f;
                }
            }
        }

        private static void RepairEnvironmentSurfaces(Scene scene)
        {
            var environment = FindNamed(
                scene,
                "PHS_NetworkTutorialEnvironment").transform;
            for (var zIndex = 0;
                 zIndex <= EnvironmentLastSegmentIndex;
                 zIndex++)
            {
                var z = zIndex * EnvironmentModuleSize;
                for (var xIndex = -1; xIndex <= 1; xIndex++)
                {
                    var floorName =
                        $"PHS_NetworkTutorialFloor_{zIndex}_{xIndex + 1}";
                    var floor = FindNamedOptional(scene, floorName)
                        ?? InstantiatePrefab(
                            WallPrefabPath,
                            scene,
                            environment);
                    floor.name = floorName;
                    floor.transform.position = new Vector3(
                        xIndex * EnvironmentModuleSize,
                        -0.513f,
                        z + 1.8f);
                    floor.transform.rotation = Quaternion.Euler(
                        -90f,
                        0f,
                        0f);
                    floor.transform.localScale = Vector3.one * 0.9f;
                }

                for (var yIndex = 0; yIndex <= 1; yIndex++)
                {
                    var y = yIndex * EnvironmentModuleSize;
                    var leftName =
                        $"PHS_NetworkTutorialWall_L_{zIndex}_{yIndex}";
                    var left = FindNamedOptional(scene, leftName)
                        ?? InstantiatePrefab(
                            WallPrefabPath,
                            scene,
                            environment);
                    left.name = leftName;
                    left.transform.position = new Vector3(-5.67f, y, z);
                    left.transform.rotation = Quaternion.Euler(
                        0f,
                        90f,
                        0f);
                    left.transform.localScale = Vector3.one * 0.9f;

                    var rightName =
                        $"PHS_NetworkTutorialWall_R_{zIndex}_{yIndex}";
                    var right = FindNamedOptional(scene, rightName)
                        ?? InstantiatePrefab(
                            WallPrefabPath,
                            scene,
                            environment);
                    right.name = rightName;
                    right.transform.position = new Vector3(5.67f, y, z);
                    right.transform.rotation = Quaternion.Euler(
                        0f,
                        270f,
                        0f);
                    right.transform.localScale = Vector3.one * 0.9f;
                }
            }

            var exitDoor = FindNamed(scene, "PHS_NetworkTutorialExitDoor");
            exitDoor.transform.position = new Vector3(0f, 0f, TutorialEndCapZ);
            exitDoor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var endLeft = FindNamed(scene, "PHS_NetworkTutorialEndWall_L");
            endLeft.transform.position = new Vector3(-4.5f, 0f, TutorialEndCapZ);
            endLeft.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var endRight = FindNamed(scene, "PHS_NetworkTutorialEndWall_R");
            endRight.transform.position = new Vector3(4.5f, 0f, TutorialEndCapZ);
            endRight.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            for (var index = 0; index < 3; index++)
            {
                var upper = FindNamed(
                    scene,
                    $"PHS_NetworkTutorialEndWall_Upper_{index}");
                upper.transform.position = new Vector3(
                    (index - 1) * EnvironmentModuleSize,
                    EnvironmentModuleSize,
                    TutorialEndCapZ);
                upper.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            FindNamed(scene, "PHS_NetworkTutorialItemDesk_L")
                .transform.position = new Vector3(-1.5f, 0f, 39.6f);
            FindNamed(scene, "PHS_NetworkTutorialItemDesk_R")
                .transform.position = new Vector3(1.5f, 0f, 39.6f);
        }

        private static void CreatePracticeItems(
            Scene scene,
            Transform parent,
            Vector3 localPosition)
        {
            var root = new GameObject("PHS_TutorialPracticeItems");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            CreateItemPair(scene, root.transform, 3.5f, "Transfer");
            CreateToolPair(scene, root.transform, 3.5f);
        }

        private static void RemoveLegacyPracticeItems(Scene scene)
        {
            var targetPaths = new[]
            {
                WrenchPrefabPath,
                BatteryPrefabPath,
                LegacyDroppedWrenchPrefabPath,
                LegacyDroppedBatteryPrefabPath
            };
            var prefabRoots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => PrefabUtility.GetOutermostPrefabInstanceRoot(
                    transform.gameObject))
                .Where(root => root != null)
                .Distinct()
                .ToArray();
            foreach (var prefabRoot in prefabRoots)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(
                    prefabRoot);
                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (targetPaths.Contains(sourcePath))
                {
                    UnityEngine.Object.DestroyImmediate(prefabRoot);
                }
            }
        }

        private static void CreateItemPair(
            Scene scene,
            Transform parent,
            float z,
            string suffix)
        {
            var wrench = InstantiatePrefab(WrenchPrefabPath, scene, parent);
            wrench.name = $"PHS_TutorialWrench_{suffix}";
            wrench.transform.localPosition = new Vector3(-5f, -0.51f, z);
            SetSeedItemKinematic(wrench);
            var battery = InstantiatePrefab(BatteryPrefabPath, scene, parent);
            battery.name = $"PHS_TutorialBattery_{suffix}";
            battery.transform.localPosition = new Vector3(-8f, -0.51f, z);
            SetSeedItemKinematic(battery);
        }

        private static void CreateToolPair(
            Scene scene,
            Transform parent,
            float z)
        {
            var wrench = InstantiatePrefab(WrenchPrefabPath, scene, parent);
            wrench.name = "PHS_TutorialWrench_ToolUse";
            wrench.transform.localPosition = new Vector3(-21.8f, -0.51f, z);
            SetSeedItemKinematic(wrench);
            var extinguisher = InstantiatePrefab(
                FireExtinguisherPrefabPath,
                scene,
                parent);
            extinguisher.name = "PHS_TutorialExtinguisher_ToolUse";
            extinguisher.transform.localPosition =
                new Vector3(-24.5f, -0.51f, z);
            SetSeedItemKinematic(extinguisher);
        }

        private static void SetSeedItemKinematic(GameObject item)
        {
            var body = item.GetComponent<Rigidbody>();
            if (body == null)
            {
                throw Failure($"item_rigidbody_missing item={item.name}");
            }

            body.isKinematic = true;
        }

        private static void RepositionPracticeVolumes(
            Scene scene,
            Transform parent,
            Transform teamMap)
        {
            foreach (var targetName in new[]
                     {
                         "PHS_NetworkTutorialGrappleTarget",
                         "PHS_NetworkTutorialGrappleTarget_A",
                         "PHS_NetworkTutorialGrappleTarget_B"
                     })
            {
                var oldTarget = FindNamedOptional(scene, targetName);
                if (oldTarget != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldTarget);
                }
            }

            var grappleTarget = InstantiatePrefab(
                GrappleAnchorPrefabPath,
                scene,
                parent);
            grappleTarget.name = "PHS_NetworkTutorialGrappleTarget_A";
            grappleTarget.transform.position = new Vector3(-1.6f, 2.8f, 20f);
            grappleTarget.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            grappleTarget.transform.localScale = Vector3.one * 0.35f;
            ApplyGrappleTargetMaterial(grappleTarget);
            var secondTarget = InstantiatePrefab(
                GrappleAnchorPrefabPath,
                scene,
                parent);
            secondTarget.name = "PHS_NetworkTutorialGrappleTarget_B";
            secondTarget.transform.position = new Vector3(1.8f, 3.4f, 13f);
            secondTarget.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            secondTarget.transform.localScale = Vector3.one * 0.35f;
            ApplyGrappleTargetMaterial(secondTarget);
            if (grappleTarget.GetComponentsInChildren<Collider>(true)
                    .All(collider => collider.isTrigger)
                || secondTarget.GetComponentsInChildren<Collider>(true)
                    .All(collider => collider.isTrigger))
            {
                throw Failure("grapple_anchor_nontrigger_collider_missing");
            }

            var legacyGravity = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    NetworkPlayerGravityArea>(true))
                .Where(area => !area.transform.IsChildOf(teamMap))
                .ToArray();
            foreach (var area in legacyGravity)
            {
                area.gameObject.SetActive(false);
            }
        }

        private static void ApplyGrappleTargetMaterial(GameObject target)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                GrappleAnchorMaterialPath);
            if (material == null)
            {
                throw Failure("grapple_anchor_material_missing");
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable
                    .Repeat(material, renderer.sharedMaterials.Length)
                    .ToArray();
            }
        }

        private static ExteriorLayout ConfigureGravityAndDebris(
            Scene scene,
            Transform parent,
            Transform teamMap,
            NetworkPlayerController player)
        {
            var interior = FindNamedUnder(teamMap, "P_Space_Base_01");
            var interiorBounds = CalculateRendererBounds(
                interior.transform,
                "interior_bounds_missing");
            var interiorMax = interiorBounds.max;
            interiorMax.z = Mathf.Min(
                interiorMax.z,
                ExteriorEntryPosition.z);
            interiorBounds.SetMinMax(interiorBounds.min, interiorMax);
            if (interiorBounds.size.x <= 0f || interiorBounds.size.z <= 0f)
            {
                throw Failure("interior_gravity_bounds_invalid_after_airlock_split");
            }

            ConfigureGravityVolume(
                scene,
                interior.transform,
                "PHS_TutorialInteriorGravity",
                interiorBounds,
                GravityMode.ShipGravity,
                NetworkPlayerGravityMode.ShipGravity,
                30);
            var hangar = FindNamedUnder(teamMap, "frame");
            var hangarBounds = CalculateRendererBounds(
                hangar.transform,
                "hangar_gravity_bounds_missing");
            hangarBounds.Expand(new Vector3(1f, 2f, 1f));
            ConfigureGravityVolume(
                scene,
                hangar.transform,
                "PHS_TutorialInteriorGravity_Hangar",
                hangarBounds,
                GravityMode.ShipGravity,
                NetworkPlayerGravityMode.ShipGravity,
                30);
            if (player.GetComponent<PlayerGravityReceiver>() == null)
            {
                player.gameObject.AddComponent<PlayerGravityReceiver>();
            }

            var shipDoor = FindNamedUnder(
                teamMap,
                "SpaceShip_Door_Left").transform;
            var boardingPosition = shipDoor.position + Vector3.back * 7.5f;
            var recoveryPlatform = FindNamedUnder(
                teamMap,
                "P_PlateForm_Bay_02 (2)").transform;
            var boardingPlatform = FindNamedUnder(
                teamMap,
                "P_PlateForm_Bay_02 (4)").transform;
            boardingPosition.y = FindFloorTopY(
                boardingPlatform,
                boardingPosition);
            var checkpointPosition = Vector3.Lerp(
                ExteriorEntryPosition,
                boardingPosition,
                0.22f);
            checkpointPosition.y = 2.2f;
            var recoveryCenter = Vector3.Lerp(
                ExteriorEntryPosition,
                boardingPosition,
                0.55f);
            recoveryCenter.y = FindFloorTopY(
                recoveryPlatform,
                recoveryCenter) + 0.03f;
            var approachPosition = Vector3.Lerp(
                ExteriorEntryPosition,
                boardingPosition,
                0.82f);
            var collectionCenter = recoveryCenter +
                new Vector3(0f, 2.45f, -7f);
            var playAreaBounds = new Bounds(
                ExteriorEntryPosition,
                Vector3.zero);
            playAreaBounds.Encapsulate(boardingPosition);
            playAreaBounds.Encapsulate(TutorialStartPosition);
            playAreaBounds.Expand(80f);
            ConfigureGravityVolume(
                scene,
                parent,
                "PHS_TutorialExteriorZeroGravity",
                playAreaBounds,
                GravityMode.Spacewalk,
                NetworkPlayerGravityMode.Spacewalk,
                20);
            var recoveryTrigger = CreateRecoveryStation(
                scene,
                recoveryPlatform,
                recoveryCenter,
                CreateTutorialEconomyWallet(scene, parent));
            CreateDebrisStream(scene, parent, collectionCenter);
            return new ExteriorLayout(
                checkpointPosition,
                recoveryCenter,
                recoveryCenter,
                approachPosition,
                boardingPosition,
                playAreaBounds,
                boardingPlatform,
                recoveryTrigger);
        }

        private static void CreatePlayAreaBoundary(
            Scene scene,
            Transform parent,
            Bounds bounds,
            Vector3 returnPosition,
            Vector3 lookAtPosition)
        {
            var root = new GameObject("PHS_TutorialPlayAreaBoundary");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.position = bounds.center;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = bounds.size;
            collider.isTrigger = true;
            root.AddComponent<NetworkObject>();

            var returnPoint = new GameObject("ReturnPoint").transform;
            returnPoint.SetParent(root.transform, true);
            returnPoint.position = returnPosition;
            returnPoint.rotation = Quaternion.LookRotation(
                lookAtPosition - returnPosition,
                Vector3.up);

            var boundary = root.AddComponent<
                NetworkTutorialPlayAreaBoundary>();
            var serialized = new SerializedObject(boundary);
            serialized.FindProperty("playArea").objectReferenceValue = collider;
            serialized.FindProperty("returnPoint").objectReferenceValue =
                returnPoint;
            serialized.FindProperty("warningSeconds").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGravityVolume(
            Scene scene,
            Transform parent,
            string name,
            Bounds bounds,
            GravityMode itemMode,
            NetworkPlayerGravityMode playerMode,
            int priority)
        {
            var itemRoot = new GameObject(name + "_Items");
            SceneManager.MoveGameObjectToScene(itemRoot, scene);
            itemRoot.transform.SetParent(parent, false);
            itemRoot.transform.position = bounds.center;
            var itemCollider = itemRoot.AddComponent<BoxCollider>();
            itemCollider.size = bounds.size;
            itemCollider.isTrigger = true;
            var itemZone = itemRoot.AddComponent<GravityZone>();
            var itemSerialized = new SerializedObject(itemZone);
            itemSerialized.FindProperty("gravityMode").enumValueIndex =
                (int)itemMode;
            itemSerialized.FindProperty("priority").intValue = priority;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();

            var playerRoot = new GameObject(name + "_Player");
            SceneManager.MoveGameObjectToScene(playerRoot, scene);
            playerRoot.transform.SetParent(parent, false);
            playerRoot.transform.position = bounds.center;
            var playerCollider = playerRoot.AddComponent<BoxCollider>();
            playerCollider.size = bounds.size;
            playerCollider.isTrigger = true;
            var playerArea = playerRoot.AddComponent<
                NetworkPlayerGravityArea>();
            var playerSerialized = new SerializedObject(playerArea);
            playerSerialized.FindProperty("gravityMode").enumValueIndex =
                (int)playerMode;
            playerSerialized.FindProperty("priority").intValue = priority;
            playerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTutorialGameCore(Scene scene)
        {
            var gameCore = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(GameCorePrefabPath),
                scene) as GameObject;
            if (gameCore == null)
            {
                throw Failure("tutorial_game_core_instantiate_failed");
            }

            gameCore.name = "PHS_TutorialGameCore";
        }

        private static ShopEconomyWalletAdapter CreateTutorialEconomyWallet(
            Scene scene,
            Transform parent)
        {
            var root = new GameObject("PHS_TutorialDebrisEconomyWallet");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            return root.AddComponent<ShopEconomyWalletAdapter>();
        }

        private static GameObject CreateRecoveryStation(
            Scene scene,
            Transform parent,
            Vector3 position,
            ShopEconomyWalletAdapter wallet)
        {
            var station = InstantiatePrefab(
                DebrisSellStationPrefabPath,
                scene,
                parent);
            station.name = "PHS_TutorialDebrisRecoveryStation";
            station.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(-90.764f, 0f, 0f));
            var sellZone = station.GetComponentInChildren<DebrisSellZone>(true);
            if (sellZone == null)
            {
                throw Failure("tutorial_debris_sell_station_setup_missing");
            }

            SetReference(sellZone, "shopWalletSource", wallet);
            return sellZone.gameObject;
        }

        private static float FindFloorTopY(
            Transform platform,
            Vector3 worldPosition)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(
                    new Vector3(worldPosition.x, worldPosition.y + 100f,
                        worldPosition.z),
                    Vector3.down,
                    200f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                .Where(hit => hit.transform.IsChildOf(platform))
                .OrderByDescending(hit => hit.point.y)
                .ToArray();
            if (hits.Length == 0)
            {
                throw Failure(
                    $"platform_floor_hit_missing platform={platform.name} position={worldPosition}");
            }

            return hits[0].point.y;
        }

        private static void CreateDebrisStream(
            Scene scene,
            Transform parent,
            Vector3 center)
        {
            var root = new GameObject("PHS_TutorialDebrisStream");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            var cargo = InstantiatePrefab(DebrisCargoPrefabPath, scene, root.transform);
            cargo.name = "PHS_TutorialDebris_Cargo";
            var camera = InstantiatePrefab(DebrisCameraPrefabPath, scene, root.transform);
            camera.name = "PHS_TutorialDebris_Camera";
            cargo.transform.position = center + new Vector3(-1.35f, 1.5f, -2f);
            camera.transform.position = center + new Vector3(1.35f, 0.75f, 2f);

            var stream = root.AddComponent<PHSRandomDebrisStream>();
            var serialized = new SerializedObject(stream);
            serialized.FindProperty("allowOfflineLocalSimulation").boolValue =
                true;
            var roots = serialized.FindProperty("debrisRoots");
            roots.arraySize = 2;
            roots.GetArrayElementAtIndex(0).objectReferenceValue = cargo.transform;
            roots.GetArrayElementAtIndex(1).objectReferenceValue = camera.transform;
            serialized.FindProperty("minimumDebrisCount").intValue = 6;
            serialized.FindProperty("maximumDebrisCount").intValue = 8;
            serialized.FindProperty("densityMultiplier").floatValue = 1f;
            serialized.FindProperty("spawnCenter").vector3Value = center;
            serialized.FindProperty("spawnExtents").vector3Value =
                new Vector3(1.5f, 3f, 4f);
            serialized.FindProperty("recycleWorldX").floatValue = center.x - 6f;
            serialized.FindProperty("minimumSpeed").floatValue = 0.5f;
            serialized.FindProperty("maximumSpeed").floatValue = 1.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindNamedUnder(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == name)
                .ToArray();
            if (matches.Length != 1)
            {
                throw Failure(
                    $"team_map_object_count name={name} count={matches.Length}");
            }

            return matches[0].gameObject;
        }

        private static Bounds CalculateRendererBounds(
            Transform root,
            string failureReason)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled)
                .ToArray();
            if (renderers.Length == 0)
            {
                throw Failure(failureReason);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void CreateJumpBarrier(
            Scene scene,
            Transform parent,
            float z,
            string suffix)
        {
            var barrier = InstantiatePrefab(WallPrefabPath, scene, parent);
            barrier.name = $"PHS_TutorialJumpBarrier_{suffix}";
            barrier.transform.position = new Vector3(0f, 0.55f, z);
            barrier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            barrier.transform.localScale = new Vector3(0.52f, 0.22f, 0.22f);
        }

        private static void CreateAndWireRoomObjectives(
            Scene scene,
            Transform parent,
            NetworkTutorialRoomController[] rooms,
            TutorialStations interactionStations,
            ExteriorLayout exterior)
        {
            if (rooms.Length != 8
                || interactionStations.TrainingA == null
                || interactionStations.TrainingB == null
                || interactionStations.Boarding == null)
            {
                throw Failure("objective_contract_count_invalid");
            }

            var player = FindComponent<NetworkPlayerController>(scene);
            var grapple = player.GetComponent<NetworkPlayerGrappleController>();
            var itemAction = player.GetComponent<
                PHSNetworkItemUseActionController>();
            var itemHolder = player.GetComponent<TempPlayerItemHolder>();
            var actionSource = player.GetComponent<
                NetworkTutorialActionSource>();
            if (grapple == null
                || itemAction == null
                || itemHolder == null
                || actionSource == null)
            {
                throw Failure("objective_player_component_missing");
            }

            var practiceItemsRoot = FindNamed(
                scene,
                "PHS_TutorialPracticeItems").transform;

            var roomObjectives = new MonoBehaviour[rooms.Length][];
            roomObjectives[0] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "move_checkpoint",
                    new Vector3(0f, 1.25f, 43f),
                    new Vector3(5.5f, 2.5f, 0.9f),
                    false)
            };

            roomObjectives[1] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "thruster_checkpoint_a",
                    new Vector3(0.25f, 1.25f, 34f),
                    new Vector3(2.1f, 2.1f, 1f),
                    false),
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "thruster_checkpoint_b",
                    new Vector3(0.25f, 1.25f, 26f),
                    new Vector3(2.1f, 2.1f, 1f),
                    false)
            };

            roomObjectives[2] = new MonoBehaviour[]
            {
                CreateGrappleObjective(
                    FindNamed(scene, "PHS_NetworkTutorialGrappleTarget_A"),
                    grapple,
                    "grapple_anchor_a"),
                CreateGrappleObjective(
                    FindNamed(scene, "PHS_NetworkTutorialGrappleTarget_B"),
                    grapple,
                    "grapple_anchor_b")
            };

            roomObjectives[3] = new MonoBehaviour[]
            {
                CreateHeldItemDropObjective(
                    scene,
                    practiceItemsRoot,
                    itemHolder,
                    actionSource,
                    "item_drop_wrench",
                    "wrench",
                    FindNamed(scene, "PHS_TutorialWrench_Transfer")
                        .transform.position),
                CreateHeldItemDropObjective(
                    scene,
                    practiceItemsRoot,
                    itemHolder,
                    actionSource,
                    "item_drop_battery",
                    "battery_pack",
                    FindNamed(scene, "PHS_TutorialBattery_Transfer")
                        .transform.position)
            };

            roomObjectives[4] = new MonoBehaviour[]
            {
                CreateToolUseObjective(
                    rooms[4].gameObject,
                    itemAction,
                    "tool_use_wrench",
                    PHSItemUseActionKind.Wrench),
                CreateToolUseObjective(
                    rooms[4].gameObject,
                    itemAction,
                    "tool_use_extinguisher",
                    PHSItemUseActionKind.FireExtinguisher)
            };

            roomObjectives[5] = new MonoBehaviour[]
            {
                interactionStations.TrainingA,
                interactionStations.TrainingB
            };
            roomObjectives[6] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "exterior_zero_g_checkpoint",
                    exterior.CheckpointPosition,
                    new Vector3(5f, 5f, 5f),
                    true),
                CreateDropZoneObjective(
                    exterior.RecoveryTrigger,
                    "debris_recovery",
                    string.Empty)
            };
            roomObjectives[7] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "boarding_approach_checkpoint",
                    exterior.ApproachPosition,
                    new Vector3(5f, 5f, 5f),
                    true),
                interactionStations.Boarding
            };

            ConfigureInteractionObjective(
                interactionStations.Boarding,
                "boarding_interact");
            var roomMarkers = CreateObjectiveGuidance(
                scene,
                rooms,
                roomObjectives);
            CreateToolUseExitGuidance(
                scene,
                rooms[5],
                FindNamed(scene, "PHS_TutorialExtinguisher_ToolUse")
                    .transform.position,
                interactionStations.TrainingA.transform.position,
                "PHS_ToolUseExitDirection");
            CreateToolUseExitGuidance(
                scene,
                rooms[6],
                interactionStations.TrainingB.transform.position,
                Specs[5].GateAnchor.Value,
                "PHS_MiniGameExitDirection");

            for (var roomIndex = 0;
                 roomIndex < rooms.Length;
                 roomIndex++)
            {
                var serialized = new SerializedObject(rooms[roomIndex]);
                var objectivesProperty = serialized.FindProperty(
                    "objectiveSourceBehaviours");
                objectivesProperty.arraySize =
                    roomObjectives[roomIndex].Length;
                for (var objectiveIndex = 0;
                     objectiveIndex < roomObjectives[roomIndex].Length;
                     objectiveIndex++)
                {
                    objectivesProperty.GetArrayElementAtIndex(objectiveIndex)
                        .objectReferenceValue =
                        roomObjectives[roomIndex][objectiveIndex];
                }

                var markersProperty = serialized.FindProperty(
                    "objectiveMarkerRoots");
                markersProperty.arraySize = roomMarkers[roomIndex].Length;
                for (var markerIndex = 0;
                     markerIndex < roomMarkers[roomIndex].Length;
                     markerIndex++)
                {
                    markersProperty.GetArrayElementAtIndex(markerIndex)
                        .objectReferenceValue =
                        roomMarkers[roomIndex][markerIndex];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static NetworkTutorialCheckpointObjective
            CreateCheckpointObjective(
                Scene scene,
                Transform parent,
                NetworkPlayerController player,
                string objectiveId,
                Vector3 position,
                Vector3 size,
                bool requireZeroGravity,
                bool requireJump = false)
        {
            var root = new GameObject($"PHS_TutorialObjective_{objectiveId}");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            var objective = root.AddComponent<
                NetworkTutorialCheckpointObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("playerController").objectReferenceValue =
                player;
            serialized.FindProperty("requireZeroGravity").boolValue =
                requireZeroGravity;
            serialized.FindProperty("requireJump").boolValue = requireJump;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static GameObject[][] CreateObjectiveGuidance(
            Scene scene,
            NetworkTutorialRoomController[] rooms,
            MonoBehaviour[][] roomObjectives)
        {
            var roomMarkers = new GameObject[rooms.Length][];
            var practiceItemsRoot = FindNamed(
                scene,
                "PHS_TutorialPracticeItems").transform;
            for (var roomIndex = 0;
                 roomIndex < rooms.Length;
                 roomIndex++)
            {
                var captions = roomIndex == 0
                    ? new[] { "이동 체크포인트" }
                    : ObjectiveMarkerCaptions[roomIndex];
                if (roomIndex >= ObjectiveMarkerCaptions.Length
                    || captions.Length != roomObjectives[roomIndex].Length)
                {
                    throw Failure(
                        $"objective_marker_caption_count_invalid room={roomIndex + 1}");
                }

                var parent = rooms[roomIndex].transform.Find(
                    "ObjectiveGuidance");
                if (parent == null)
                {
                    throw Failure(
                        $"guidance_root_missing room={rooms[roomIndex].RoomId}");
                }

                var objectives = roomObjectives[roomIndex];
                roomMarkers[roomIndex] = new GameObject[objectives.Length];
                for (var objectiveIndex = 0;
                     objectiveIndex < objectives.Length;
                     objectiveIndex++)
                {
                    var accent = objectiveIndex % 2 == 0
                        ? new Color(1f, 0.78f, 0.18f, 1f)
                        : new Color(1f, 0.36f, 0.12f, 1f);
                    var number = (objectiveIndex + 1).ToString();
                    if (roomIndex == 0 || roomIndex == 1)
                    {
                        Physics.SyncTransforms();
                        var objectivePosition =
                            objectives[objectiveIndex].transform.position;
                        if (!Physics.Raycast(
                                new Vector3(
                                    objectivePosition.x,
                                    objectivePosition.y + 1f,
                                    objectivePosition.z),
                                Vector3.down,
                                out var floorHit,
                                6f,
                                Physics.DefaultRaycastLayers,
                                QueryTriggerInteraction.Ignore))
                        {
                            throw Failure(
                                $"objective_floor_missing index={objectiveIndex + 1}");
                        }

                        var group = new GameObject(
                            $"FloorObjectiveGroup_{number}");
                        SceneManager.MoveGameObjectToScene(group, scene);
                        group.transform.SetParent(parent, false);
                        group.transform.position = floorHit.point;
                        var pad = InstantiatePrefab(
                            FloorObjectivePadPrefabPath,
                            scene,
                            group.transform);
                        pad.name = $"FloorObjectivePad_{number}";
                        pad.transform.position = floorHit.point
                                                 + Vector3.up * 0.1f;
                        pad.transform.rotation = Quaternion.identity;
                        pad.transform.localScale =
                            new Vector3(3.2f, 0.04f, 3.2f);
                        var padRenderer = pad.GetComponentInChildren<Renderer>(true);
                        var padMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                            FloorObjectivePadMaterialPath);
                        if (padRenderer == null || padMaterial == null)
                        {
                            throw Failure("objective_floor_visual_missing");
                        }

                        padRenderer.sharedMaterial = padMaterial;
                        padRenderer.shadowCastingMode =
                            UnityEngine.Rendering.ShadowCastingMode.Off;
                        padRenderer.receiveShadows = false;
                        var lightPillar = InstantiatePrefab(
                            ObjectiveLightPillarPrefabPath,
                            scene,
                            group.transform);
                        lightPillar.name =
                            $"ObjectiveLightPillar_{number}";
                        lightPillar.transform.position = floorHit.point
                                                         + Vector3.up * 2.04f;
                        lightPillar.transform.rotation = Quaternion.identity;
                        CreateFloorObjectiveMarker(
                            scene,
                            group.transform,
                            number,
                            captions[objectiveIndex],
                            accent,
                            floorHit.point + Vector3.up * 0.13f);
                        roomMarkers[roomIndex][objectiveIndex] = group;
                        continue;
                    }

                    if (roomIndex == 3)
                    {
                        var item = FindNamed(
                            scene,
                            objectiveIndex == 0
                                ? "PHS_TutorialWrench_Transfer"
                                : "PHS_TutorialBattery_Transfer");
                        var group = new GameObject(
                            $"ObjectiveMarkerGroup_{number}");
                        SceneManager.MoveGameObjectToScene(group, scene);
                        group.transform.SetParent(practiceItemsRoot, false);
                        group.transform.position = item.transform.position;
                        CreateObjectiveMarker(
                            scene,
                            group.transform,
                            number,
                            ObjectiveMarkerCaptions[roomIndex][objectiveIndex],
                            accent,
                            item.transform.position + Vector3.up * 1.35f);
                        roomMarkers[roomIndex][objectiveIndex] = group;
                        continue;
                    }

                    var position = objectives[objectiveIndex].transform.position
                        + Vector3.up * 2f;
                    if (roomIndex == 2)
                    {
                        position = objectives[objectiveIndex].transform.position;
                    }

                    if (roomIndex == 6 && objectiveIndex > 0)
                    {
                        position += Vector3.right *
                            (objectiveIndex == 1 ? -1.25f : 1.25f);
                    }

                    if (roomIndex == 4)
                    {
                        var item = FindNamed(
                            scene,
                            objectiveIndex == 0
                                ? "PHS_TutorialWrench_ToolUse"
                                : "PHS_TutorialExtinguisher_ToolUse");
                        position = item.transform.position +
                                   Vector3.up * 1.35f;
                    }

                    var marker = CreateObjectiveMarker(
                        scene,
                        roomIndex == 4 ? practiceItemsRoot : parent,
                        number,
                        captions[objectiveIndex],
                        accent,
                        position);
                    if (roomIndex == 2)
                    {
                        var markerRect = marker.GetComponent<RectTransform>();
                        markerRect.sizeDelta = new Vector2(250f, 110f);
                        markerRect.localScale = Vector3.one * 0.004f;
                    }

                    roomMarkers[roomIndex][objectiveIndex] = marker;
                }
            }

            return roomMarkers;
        }

        private static void CreateToolUseExitGuidance(
            Scene scene,
            NetworkTutorialRoomController nextRoom,
            Vector3 toolPosition,
            Vector3 destinationPosition,
            string rootName)
        {
            var guidanceParent = nextRoom.transform.Find("ObjectiveGuidance");
            if (guidanceParent == null)
            {
                throw Failure("tool_use_exit_guidance_parent_missing");
            }

            var material = LoadOrCreateDirectionLineMaterial();

            const float floorHeight = -0.33f;
            var start = new Vector3(
                toolPosition.x - 0.6f,
                floorHeight,
                toolPosition.z);
            var end = new Vector3(
                destinationPosition.x + 1.4f,
                floorHeight,
                destinationPosition.z);
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(guidanceParent, false);
            root.transform.SetPositionAndRotation(
                start,
                Quaternion.Euler(90f, 0f, 0f));

            var localEnd = root.transform.InverseTransformPoint(end);
            var line = root.AddComponent<LineRenderer>();
            ConfigureDirectionLine(line, material, 0.22f);
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, localEnd);

            var direction = new Vector2(localEnd.x, localEnd.y).normalized;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var distance = new Vector2(localEnd.x, localEnd.y).magnitude;
            var arrowCount = Mathf.Clamp(
                Mathf.FloorToInt(distance / 3.4f),
                4,
                7);
            for (var index = 0; index < arrowCount; index++)
            {
                var t = (index + 1f) / (arrowCount + 1f);
                var tip = Vector2.Lerp(Vector2.zero, new Vector2(
                    localEnd.x,
                    localEnd.y), t);
                var back = tip - direction * 0.72f;
                var arrow = new GameObject($"DirectionArrow_{index + 1:00}");
                SceneManager.MoveGameObjectToScene(arrow, scene);
                arrow.transform.SetParent(root.transform, false);
                var arrowLine = arrow.AddComponent<LineRenderer>();
                ConfigureDirectionLine(arrowLine, material, 0.3f);
                arrowLine.positionCount = 3;
                arrowLine.SetPosition(
                    0,
                    new Vector3(
                        back.x + perpendicular.x * 0.5f,
                        back.y + perpendicular.y * 0.5f,
                        -0.015f));
                arrowLine.SetPosition(
                    1,
                    new Vector3(tip.x, tip.y, -0.015f));
                arrowLine.SetPosition(
                    2,
                    new Vector3(
                        back.x - perpendicular.x * 0.5f,
                        back.y - perpendicular.y * 0.5f,
                        -0.015f));
            }
        }

        private static void ConfigureDirectionLine(
            LineRenderer line,
            Material material,
            float width)
        {
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Tile;
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = new Color(1f, 0.78f, 0.18f, 0.92f);
            line.endColor = new Color(1f, 0.36f, 0.12f, 0.5f);
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static Material LoadOrCreateDirectionLineMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                DirectionLineMaterialPath);
            if (material != null)
            {
                ConfigureDirectionLineMaterial(material);
                return material;
            }

            var source = AssetDatabase.LoadAssetAtPath<Material>(
                FloorObjectivePadMaterialPath);
            if (source == null)
            {
                throw Failure("tool_use_exit_guidance_material_missing");
            }

            material = new Material(source)
            {
                name = "PHS_TutorialDirectionLine"
            };
            ConfigureDirectionLineMaterial(material);

            AssetDatabase.CreateAsset(material, DirectionLineMaterialPath);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void ConfigureDirectionLineMaterial(Material material)
        {
            material.color = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            EditorUtility.SetDirty(material);
        }

        private static GameObject CreateFloorObjectiveMarker(
            Scene scene,
            Transform parent,
            string number,
            string caption,
            Color accent,
            Vector3 worldPosition)
        {
            var marker = new GameObject(
                $"FloorObjectiveMarker_{number}",
                typeof(RectTransform),
                typeof(Canvas));
            SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.SetParent(parent, false);
            marker.transform.position = worldPosition;
            marker.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var canvas = marker.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 211;
            var rect = marker.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 110f);
            rect.localScale = Vector3.one * 0.004f;

            var background = CreateUiImage(
                "FloorBackground",
                marker.transform,
                Vector2.zero,
                Vector2.one);
            background.color = new Color(1f, 0.36f, 0.12f, 0.96f);
            background.raycastTarget = false;

            var label = CreateUiText(
                "FloorLabel",
                background.transform,
                Vector2.zero,
                Vector2.one,
                48f);
            label.text = $"{number}  {caption}";
            label.color = Color.black;
            label.raycastTarget = false;
            ApplyTutorialFont(label, ObjectiveNumberFontPath);
            return marker;
        }

        private static GameObject CreateObjectiveMarker(
            Scene scene,
            Transform parent,
            string number,
            string caption,
            Color accent,
            Vector3 worldPosition)
        {
            var marker = new GameObject(
                $"ObjectiveMarker_{number}_{caption}",
                typeof(RectTransform),
                typeof(Canvas));
            SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.SetParent(parent, false);
            marker.transform.position = worldPosition;
            marker.transform.rotation = Quaternion.identity;
            var canvas = marker.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 210;
            var markerRect = marker.GetComponent<RectTransform>();
            markerRect.sizeDelta = new Vector2(330f, 125f);
            markerRect.localScale = Vector3.one * 0.0037f;

            var background = CreateUiImage(
                "MarkerBackground",
                marker.transform,
                new Vector2(0f, 0.25f),
                Vector2.one);
            background.color = new Color(1f, 0.36f, 0.12f, 0.96f);
            background.raycastTarget = false;

            var accentBar = CreateUiImage(
                "Accent",
                background.transform,
                Vector2.zero,
                new Vector2(0.055f, 1f));
            accentBar.color = Color.black;
            accentBar.raycastTarget = false;

            var topBar = CreateUiImage(
                "TopAccent",
                background.transform,
                new Vector2(0f, 0.92f),
                Vector2.one);
            topBar.color = Color.black;
            topBar.raycastTarget = false;

            var numberText = CreateUiText(
                "Number",
                background.transform,
                new Vector2(0.07f, 0.06f),
                new Vector2(0.34f, 0.9f),
                54f);
            numberText.text = number;
            numberText.color = Color.black;
            numberText.raycastTarget = false;
            ApplyTutorialFont(numberText, ObjectiveNumberFontPath);

            var captionText = CreateUiText(
                "Caption",
                background.transform,
                new Vector2(0.34f, 0.08f),
                new Vector2(0.97f, 0.88f),
                32f);
            captionText.text = caption;
            captionText.enableAutoSizing = true;
            captionText.fontSizeMin = 22f;
            captionText.fontSizeMax = 32f;
            captionText.color = Color.black;
            captionText.raycastTarget = false;
            ApplyTutorialFont(captionText, ObjectiveBodyFontPath);

            var pointer = CreateUiText(
                "TargetPointer",
                marker.transform,
                new Vector2(0.35f, 0f),
                new Vector2(0.65f, 0.32f),
                52f);
            pointer.text = "V";
            pointer.color = accent;
            pointer.raycastTarget = false;
            ApplyTutorialFont(pointer, ObjectiveBodyFontPath);
            AddTextOutline(pointer);
            return marker;
        }

        private static void AddTextOutline(TMP_Text text)
        {
            if (text == null)
            {
                throw Failure("tutorial_outline_text_missing");
            }

            var outline = text.GetComponent<Outline>()
                ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(outline);
        }

        private static NetworkTutorialGrappleAnchorObjective
            CreateGrappleObjective(
                GameObject anchor,
                NetworkPlayerGrappleController grapple,
                string objectiveId)
        {
            var objective = anchor.AddComponent<
                NetworkTutorialGrappleAnchorObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("grappleController")
                .objectReferenceValue = grapple;
            serialized.FindProperty("anchorCollider").objectReferenceValue =
                anchor.GetComponentInChildren<Collider>(true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static NetworkTutorialItemDropZoneObjective
            CreateDropZoneObjective(
                GameObject trigger,
                string objectiveId,
                string expectedItemId)
        {
            if (trigger.GetComponent<Collider>() == null)
            {
                throw Failure("tutorial_debris_sell_trigger_collider_missing");
            }

            var objective = trigger.AddComponent<
                NetworkTutorialItemDropZoneObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("expectedItemId").stringValue =
                expectedItemId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static NetworkTutorialHeldItemDropObjective
            CreateHeldItemDropObjective(
                Scene scene,
                Transform parent,
                TempPlayerItemHolder itemHolder,
                NetworkTutorialActionSource actionSource,
                string objectiveId,
                string expectedItemId,
                Vector3 position)
        {
            var root = new GameObject($"PHS_TutorialObjective_{objectiveId}");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var objective = root.AddComponent<
                NetworkTutorialHeldItemDropObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("expectedItemId").stringValue =
                expectedItemId;
            serialized.FindProperty("itemHolder").objectReferenceValue =
                itemHolder;
            serialized.FindProperty("actionSource").objectReferenceValue =
                actionSource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static NetworkTutorialToolUseObjective CreateToolUseObjective(
            GameObject room,
            PHSNetworkItemUseActionController itemAction,
            string objectiveId,
            PHSItemUseActionKind actionKind)
        {
            var objective = room.AddComponent<
                NetworkTutorialToolUseObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("actionController")
                .objectReferenceValue = itemAction;
            serialized.FindProperty("requiredActionKind").intValue =
                (int)actionKind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static void ConfigureInteractionObjective(
            NetworkTutorialInteractionStation station,
            string objectiveId)
        {
            var serialized = new SerializedObject(station);
            serialized.FindProperty("objectiveMode").boolValue = true;
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireDirector(
            Scene scene,
            NetworkTutorialRoomController[] rooms)
        {
            var director = FindComponent<NetworkTutorialDirector>(scene);
            var player = FindComponent<NetworkPlayerController>(scene);
            var actionSource = player.GetComponent<NetworkTutorialActionSource>();
            if (actionSource == null)
            {
                actionSource = player.gameObject.AddComponent<
                    NetworkTutorialActionSource>();
            }

            actionSource.Configure(
                player,
                player.GetComponent<NetworkPlayerGrappleController>(),
                player.GetComponent<TempPlayerItemHolder>(),
                1.5f);
            EditorUtility.SetDirty(actionSource);

            var serialized = new SerializedObject(director);
            serialized.FindProperty("actionSourceBehaviour")
                .objectReferenceValue = actionSource;
            var roomsProperty = serialized.FindProperty("rooms");
            roomsProperty.arraySize = rooms.Length;
            for (var index = 0; index < rooms.Length; index++)
            {
                roomsProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue = rooms[index];
            }

            serialized.FindProperty("movementDistance").floatValue = 1.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TutorialStations CreateInteractionStations(
                Scene scene,
                Transform parent,
                Vector3 boardingPosition,
                Transform boardingParent)
        {
            var runtimeObject = InstantiatePrefab(
                MiniGameRuntimePrefabPath,
                scene,
                parent);
            runtimeObject.name = "PHS_TutorialMiniGameRuntime";
            runtimeObject.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            runtimeObject.transform.localScale = Vector3.one;
            var miniGameManager = runtimeObject.GetComponentInChildren<
                PHSMiniGameManager>(true);
            if (miniGameManager == null || miniGameManager.canvasRoot == null)
            {
                throw Failure("tutorial_minigame_runtime_reference_missing");
            }

            miniGameManager.canvasRoot.transform.localScale = Vector3.one;
            var miniGameCanvas = miniGameManager.canvasRoot.GetComponent<Canvas>();
            if (miniGameCanvas == null)
            {
                throw Failure("tutorial_minigame_canvas_missing");
            }

            miniGameCanvas.sortingOrder = 600;
            EditorUtility.SetDirty(miniGameManager.canvasRoot.transform);
            EditorUtility.SetDirty(miniGameCanvas);

            var firstStation = CreateMiniGameStation(
                scene,
                parent,
                miniGameManager,
                WireTerminalVisualPrefabPath,
                "PHS_TutorialTrainingTerminal_A",
                new Vector3(-45f, -0.53f, 3.5f),
                PHSMiniGameType.WireFix,
                "training_terminal_a",
                "전선 연결 연습 시작");
            var secondStation = CreateMiniGameStation(
                scene,
                parent,
                miniGameManager,
                PowerTerminalVisualPrefabPath,
                "PHS_TutorialTrainingTerminal_B",
                new Vector3(-49f, -0.53f, 3.5f),
                PHSMiniGameType.PowerSync,
                "training_terminal_b",
                "전력 동기화 연습 시작");

            var boardingObject = InstantiatePrefab(
                InteractionStationPrefabPath,
                scene,
                boardingParent);
            boardingObject.name = "PHS_TutorialBoardingStation";
            boardingObject.transform.SetPositionAndRotation(
                boardingPosition,
                Quaternion.Euler(270f, 270f, 0f));
            boardingObject.transform.localScale = Vector3.one;
            var boardingCollider = boardingObject.GetComponent<BoxCollider>();
            if (boardingCollider == null)
            {
                throw Failure("tutorial_boarding_station_collider_missing");
            }

            boardingCollider.center = new Vector3(0f, 0f, 0.5f);
            boardingCollider.size = new Vector3(1.2f, 1.2f, 1.4f);
            boardingCollider.isTrigger = true;
            var boardingStation = boardingObject.GetComponent<
                NetworkTutorialInteractionStation>();
            if (boardingStation == null)
            {
                throw Failure("tutorial_boarding_station_component_missing");
            }

            var boardingSerialized = new SerializedObject(boardingStation);
            boardingSerialized.FindProperty("interactionPrompt").stringValue =
                "함선 타기";
            boardingSerialized.FindProperty("singleUse").boolValue = true;
            boardingSerialized.ApplyModifiedPropertiesWithoutUndo();

            return new TutorialStations(
                firstStation,
                secondStation,
                boardingStation);
        }

        private static NetworkTutorialMiniGameStation CreateMiniGameStation(
            Scene scene,
            Transform parent,
            PHSMiniGameManager miniGameManager,
            string visualPrefabPath,
            string name,
            Vector3 position,
            PHSMiniGameType miniGameType,
            string objectiveId,
            string interactionPrompt)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 180f, 0f));
            root.transform.localScale = Vector3.one;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.675f, 0f);
            collider.size = new Vector3(1.1f, 1.35f, 1.1f);

            var visual = InstantiatePrefab(visualPrefabPath, scene, root.transform);
            visual.name = "VisualSlot";
            visual.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(270f, 0f, 0f));
            visual.transform.localScale = Vector3.one * 1.35f;

            var station = root.AddComponent<NetworkTutorialMiniGameStation>();
            var serialized = new SerializedObject(station);
            serialized.FindProperty("miniGameManager").objectReferenceValue =
                miniGameManager;
            serialized.FindProperty("miniGameType").intValue =
                (int)miniGameType;
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("interactionPrompt").stringValue =
                interactionPrompt;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return station;
        }

        private static void BindTutorialHud(Scene scene)
        {
            var player = FindComponent<NetworkPlayerController>(scene);
            var presenter = FindComponent<ParkHanSolPlayHudMockPresenter>(scene);
            SetReference(player, "playHudPresenter", presenter);
            SetReference(
                player.GetComponent<TempPlayerItemHolder>(),
                "playHudPresenter",
                presenter);
            SetReference(
                player.GetComponent<TempPlayerInteractionScanner>(),
                "playHudPresenter",
                presenter);
        }

        private static void ConfigureTutorialOnlyHud(Scene scene)
        {
            var legacyInstruction = FindNamed(scene, "Instruction");
            legacyInstruction.SetActive(false);
            EditorUtility.SetDirty(legacyInstruction);

            var missionStatus = FindNamed(scene, "Mission Status Cluster");
            missionStatus.SetActive(true);
            EditorUtility.SetDirty(missionStatus);

            var timeRoot = FindNamed(scene, "Time Root");
            timeRoot.SetActive(false);
            EditorUtility.SetDirty(timeRoot);

            var warpBar = FindNamed(scene, "Warp Gauge Bar")
                .GetComponent<RectTransform>();
            warpBar.sizeDelta = new Vector2(300f, 22f);
            warpBar.anchoredPosition = new Vector2(80f, 0f);
            EditorUtility.SetDirty(warpBar);
            AddTextOutline(
                FindNamed(scene, "Ship HP Root").transform
                    .Find("Ship HP Bar/Gauge Value Text")
                    .GetComponent<TMP_Text>());
            AddTextOutline(
                FindNamed(scene, "Warp Gauge Root").transform
                    .Find("Warp Gauge Bar/Gauge Value Text")
                    .GetComponent<TMP_Text>());

            var vitals = FindNamed(scene, "Vitals Cluster");
            var vitalsBackground = vitals.GetComponent<Image>();
            if (vitalsBackground != null)
            {
                UnityEngine.Object.DestroyImmediate(vitalsBackground);
            }

            ConfigureVitalsRow(FindNamed(scene, "Health Row"), 210f, 0f);
            ConfigureVitalsRow(FindNamed(scene, "Boost Row"), 185f, -66f);
            ConfigureVitalsRow(
                FindNamed(scene, "Economy Cluster"),
                120f,
                -112f);

            var healthText = FindNamed(scene, "Health Text")
                .GetComponent<TMP_Text>();
            healthText.text = "+100";
            healthText.fontSize = 56f;
            healthText.fontStyle = FontStyles.Bold;
            healthText.fontWeight = FontWeight.Bold;
            AddTextOutline(healthText);

            var staminaText = FindNamed(scene, "Stamina Text")
                .GetComponent<TMP_Text>();
            staminaText.text = "40<size=20> BOOST</size>";
            staminaText.fontSize = 36f;
            staminaText.fontStyle = FontStyles.Bold;
            staminaText.fontWeight = FontWeight.Bold;
            AddTextOutline(staminaText);

            var bankText = FindNamed(scene, "Bank Text")
                .GetComponent<TMP_Text>();
            var bankRect = bankText.rectTransform;
            bankRect.anchorMin = Vector2.zero;
            bankRect.anchorMax = Vector2.one;
            bankRect.offsetMin = new Vector2(8f, 0f);
            bankRect.offsetMax = new Vector2(-8f, 0f);
            bankText.fontSize = 30f;
            bankText.fontStyle = FontStyles.Bold;
            bankText.fontWeight = FontWeight.Bold;
            AddTextOutline(bankText);
            ConfigureTutorialOverlayPalette(scene);
        }

        private static void ConfigureVitalsRow(
            GameObject row,
            float width,
            float y)
        {
            var plateSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd");
            if (plateSprite == null)
            {
                throw Failure("tutorial_vitals_plate_sprite_missing");
            }

            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(0f, y);
            var plate = row.GetComponent<Image>()
                ?? row.AddComponent<Image>();
            plate.sprite = plateSprite;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(0.08f, 0.09f, 0.1f, 0.58f);
            plate.raycastTarget = false;
            var outline = row.GetComponent<Outline>()
                ?? row.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(plate);
            EditorUtility.SetDirty(outline);
        }

        private static void ConfigureTutorialOverlayPalette(Scene scene)
        {
            var black = new Color(0.004f, 0.005f, 0.006f, 0.94f);
            var charcoal = new Color(0.08f, 0.09f, 0.1f, 0.94f);
            var orange = new Color(1f, 0.36f, 0.12f, 1f);
            var yellow = new Color(1f, 0.78f, 0.18f, 1f);

            var interaction = FindNamed(scene, "Interaction Prompt")
                .transform;
            ConfigureOutlinedImage(
                FindNamedUnder(interaction, "Interaction Panel")
                    .GetComponent<Image>(),
                black);
            ConfigureOutlinedImage(
                FindNamedUnder(interaction, "Input Badge")
                    .GetComponent<Image>(),
                orange);
            ConfigurePaletteText(
                FindNamedUnder(interaction, "Input Text")
                    .GetComponent<TMP_Text>(),
                Color.black);
            ConfigurePaletteText(
                FindNamedUnder(interaction, "Prompt Text")
                    .GetComponent<TMP_Text>(),
                yellow);

            var pauseMenu = FindNamed(scene, "Pause Menu").transform;
            var pauseController =
                FindComponent<ParkHanSolPauseMenuController>(scene);
            pauseController.enabled = true;
            EditorUtility.SetDirty(pauseController);
            var pauseCard = FindNamedUnder(pauseMenu, "Pause Card")
                .transform;
            var pauseCardImage = pauseCard.GetComponent<Image>()
                ?? pauseCard.gameObject.AddComponent<Image>();
            pauseCardImage.raycastTarget = false;
            ConfigureOutlinedImage(pauseCardImage, black);
            ConfigureOutlinedImage(
                FindNamedUnder(pauseCard, "Pause Selection Block")
                    .GetComponent<Image>(),
                charcoal);
            ConfigurePaletteText(
                FindNamedUnder(pauseCard, "Title").GetComponent<TMP_Text>(),
                orange);
            ConfigurePaletteText(
                FindNamedUnder(pauseCard, "Hint").GetComponent<TMP_Text>(),
                yellow);
            foreach (var buttonName in new[]
                     {
                         "Resume Button",
                         "Options Button",
                         "Exit Game Button"
                     })
            {
                var button = FindNamedUnder(pauseCard, buttonName).transform;
                ConfigureOutlinedImage(button.GetComponent<Image>(), orange);
                ConfigureButtonPalette(
                    button.GetComponent<Button>(),
                    charcoal);
                ConfigurePaletteText(
                    FindNamedUnder(button, "Label").GetComponent<TMP_Text>(),
                    Color.black);
            }

            ConfigureAlertPalette(
                FindNamed(scene, "Gravity Warning").transform,
                "Gravity Warning Panel",
                "Gravity Warning Text",
                black,
                orange,
                yellow);
            ConfigureAlertPalette(
                FindNamed(scene, "Respawn Status").transform,
                "Respawn Status Panel",
                "Respawn Status Text",
                black,
                orange,
                yellow);

            var tutorialUi = FindNamed(scene, "PHS_NetworkTutorialUI")
                .transform;
            var completionPanel = FindNamedUnder(
                tutorialUi,
                "Completion Panel").transform;
            ConfigureOutlinedImage(
                completionPanel.GetComponent<Image>(),
                black);
            ConfigurePaletteText(
                FindNamedUnder(completionPanel, "Completion Title")
                    .GetComponent<TMP_Text>(),
                yellow);
            var returnButton = FindNamedUnder(
                completionPanel,
                "Return To Lobby").transform;
            ConfigureOutlinedImage(returnButton.GetComponent<Image>(), orange);
            ConfigureButtonPalette(
                returnButton.GetComponent<Button>(),
                charcoal);
            ConfigurePaletteText(
                FindNamedUnder(returnButton, "Label")
                    .GetComponent<TMP_Text>(),
                Color.black);

            ConfigureMiniGamePalette(scene, orange, yellow);
        }

        private static void ConfigureMiniGamePalette(
            Scene scene,
            Color orange,
            Color yellow)
        {
            var runtime = FindNamed(scene, "PHS_TutorialMiniGameRuntime")
                .transform;
            foreach (var text in runtime.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "StatusText"
                    || text.name == "Timer"
                    || text.name == "TimerText"
                    || text.name == "Score Text")
                {
                    ConfigurePaletteText(text, yellow);
                }
            }

            var keypad = FindNamedUnder(runtime, "DoorKeypadGame").transform;
            foreach (var button in keypad.GetComponentsInChildren<Button>(true))
            {
                ConfigureOutlinedImage(button.GetComponent<Image>(), orange);
                ConfigureButtonPalette(
                    button,
                    new Color(0.08f, 0.09f, 0.1f, 0.94f));
                ConfigurePaletteText(
                    button.GetComponentInChildren<TMP_Text>(true),
                    Color.black);
            }
        }

        private static void ConfigureAlertPalette(
            Transform root,
            string panelName,
            string textName,
            Color background,
            Color accent,
            Color textColor)
        {
            ConfigureOutlinedImage(
                FindNamedUnder(root, panelName).GetComponent<Image>(),
                background);
            FindNamedUnder(root, "Gravity Accent Glow")
                .GetComponent<Image>().color = accent;
            ConfigurePaletteText(
                FindNamedUnder(root, textName).GetComponent<TMP_Text>(),
                textColor);
        }

        private static void ConfigureOutlinedImage(Image image, Color color)
        {
            if (image == null)
            {
                throw Failure("tutorial_palette_image_missing");
            }

            image.color = color;
            var outline = image.GetComponent<Outline>()
                ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(outline);
        }

        private static void ConfigurePaletteText(TMP_Text text, Color color)
        {
            if (text == null)
            {
                throw Failure("tutorial_palette_text_missing");
            }

            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.fontWeight = FontWeight.Bold;
            EditorUtility.SetDirty(text);
        }

        private static void ConfigureButtonPalette(
            Button button,
            Color disabled)
        {
            if (button == null)
            {
                throw Failure("tutorial_palette_button_missing");
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = disabled;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            EditorUtility.SetDirty(button);
        }

        private static void ConfigureTutorialSkybox()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(
                TutorialSkyboxMaterialPath);
            if (skybox == null)
            {
                throw Failure("tutorial_skybox_material_missing");
            }

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.65f;
            RenderSettings.reflectionIntensity = 0.55f;
        }

        private static Slider CreateProgressSlider(Transform parent)
        {
            var root = CreateRect(
                "Progress",
                parent,
                new Vector2(0.08f, 0.04f),
                new Vector2(0.92f, 0.11f));
            var background = root.AddComponent<Image>();
            background.color = new Color(0.004f, 0.005f, 0.006f, 0.94f);
            var fillArea = CreateRect(
                "FillArea",
                root.transform,
                new Vector2(0.02f, 0.18f),
                new Vector2(0.98f, 0.82f));
            var fill = CreateUiImage(
                "Fill",
                fillArea.transform,
                Vector2.zero,
                Vector2.one);
            fill.color = new Color(1f, 0.36f, 0.12f, 1f);
            var slider = root.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            slider.minValue = 0f;
            slider.maxValue = 2f;
            slider.wholeNumbers = true;
            slider.interactable = false;
            return slider;
        }

        private static Image CreateUiImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            return CreateRect(name, parent, anchorMin, anchorMax)
                .AddComponent<Image>();
        }

        private static TMP_Text CreateUiText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax)
                .AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                TutorialFontPath);
            if (text.font == null)
            {
                throw Failure($"tutorial_font_missing path={TutorialFontPath}");
            }

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            PHSUIFontPaths.ApplyResolved(text);
            return text;
        }

        private static void ApplyTutorialFont(TMP_Text text, string fontPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null)
            {
                throw Failure($"tutorial_font_missing path={fontPath}");
            }

            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            EditorUtility.SetDirty(text);
        }

        private static GameObject CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return gameObject;
        }

        private static GameObject InstantiatePrefab(
            string path,
            Scene scene,
            Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene)
                as GameObject;
            if (instance == null)
            {
                throw Failure($"prefab_instantiate_failed path={path}");
            }

            instance.transform.SetParent(parent, true);
            return instance;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (matches.Length != 1)
            {
                throw Failure(
                    $"component_count type={typeof(T).Name} count={matches.Length}");
            }

            return matches[0];
        }

        private static GameObject FindNamed(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == name)
                .ToArray();
            if (matches.Length != 1)
            {
                throw Failure($"object_count name={name} count={matches.Length}");
            }

            return matches[0].gameObject;
        }

        private static GameObject FindNamedOptional(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == name)
                .ToArray();
            if (matches.Length > 1)
            {
                throw Failure($"object_count name={name} count={matches.Length}");
            }

            return matches.Length == 0 ? null : matches[0].gameObject;
        }

        private static GameObject FindNamedRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SingleOrDefault(root => root.name == name);
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            if (target == null || value == null)
            {
                throw Failure($"reference_target_missing property={propertyName}");
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw Failure($"property_missing name={propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ImportInstructionSprites()
        {
            foreach (var spec in Specs)
            {
                if (string.IsNullOrWhiteSpace(spec.RoomTitle)
                    || spec.ObjectiveInstructions == null
                    || spec.ObjectiveInstructions.Length == 0
                    || spec.ObjectiveInstructions.Any(
                        string.IsNullOrWhiteSpace))
                {
                    throw Failure(
                        $"instruction_contract_invalid room={spec.Id}");
                }

                var path = InstructionFolder + spec.SpriteFile;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(
                        path,
                        ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }

                if (importer == null)
                {
                    throw Failure($"texture_importer_missing path={path}");
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var keycapImporter = AssetImporter.GetAtPath(
                TutorialKeycapSpritePath) as TextureImporter;
            if (keycapImporter == null)
            {
                AssetDatabase.ImportAsset(
                    TutorialKeycapSpritePath,
                    ImportAssetOptions.ForceSynchronousImport);
                keycapImporter = AssetImporter.GetAtPath(
                    TutorialKeycapSpritePath) as TextureImporter;
            }

            if (keycapImporter == null)
            {
                throw Failure(
                    $"texture_importer_missing path={TutorialKeycapSpritePath}");
            }

            keycapImporter.textureType = TextureImporterType.Sprite;
            keycapImporter.spriteImportMode = SpriteImportMode.Single;
            keycapImporter.alphaIsTransparency = true;
            keycapImporter.mipmapEnabled = false;
            keycapImporter.maxTextureSize = 256;
            keycapImporter.textureCompression =
                TextureImporterCompression.Uncompressed;
            keycapImporter.SaveAndReimport();
        }

        private static void RequireAssets()
        {
            foreach (var path in new[]
                     {
                         ScenePath,
                         DoorPrefabPath,
                         WallPrefabPath,
                         WrenchPrefabPath,
                         BatteryPrefabPath,
                         FireExtinguisherPrefabPath,
                         InteractionStationPrefabPath,
                         MiniGameRuntimePrefabPath,
                         WireTerminalVisualPrefabPath,
                         PowerTerminalVisualPrefabPath,
                         TeamTutorialMapPrefabPath,
                         GrappleAnchorPrefabPath,
                         FloorObjectivePadPrefabPath,
                         FloorObjectivePadMaterialPath,
                          ObjectiveLightPillarPrefabPath,
                          DebrisCargoPrefabPath,
                          DebrisCameraPrefabPath,
                          DebrisSellStationPrefabPath,
                          GameCorePrefabPath,
                          TutorialSkyboxMaterialPath,
                          TutorialKeycapSpritePath
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw Failure($"asset_missing path={path}");
                }
            }

            foreach (var spec in Specs)
            {
                var path = InstructionFolder + spec.SpriteFile;
                if (!System.IO.File.Exists(path))
                {
                    throw Failure($"instruction_image_missing path={path}");
                }
            }
        }

        private static InvalidOperationException Failure(string reason)
        {
            return new InvalidOperationException(
                $"PHS_NETWORK_TUTORIAL_ROOMS_AUTHORING_FAILED reason={reason}");
        }

        private readonly struct RoomSpec
        {
            public RoomSpec(
                string id,
                TutorialActionKind[] actions,
                float centerZ,
                Vector3? gateAnchor,
                string roomTitle,
                string[] objectiveInstructions,
                string spriteFile)
            {
                Id = id;
                Actions = actions;
                CenterZ = centerZ;
                GateAnchor = gateAnchor;
                RoomTitle = id == "01_MoveJump" ? "이동" : roomTitle;
                ObjectiveInstructions = id == "01_MoveJump"
                    ? new[]
                    {
                        "[WASD]  체크포인트로 이동"
                    }
                    : objectiveInstructions;
                SpriteFile = id == "01_MoveJump"
                    ? "PHS_Tutorial_Move.png"
                    : spriteFile;
            }

            public string Id { get; }
            public TutorialActionKind[] Actions { get; }
            public float CenterZ { get; }
            public Vector3? GateAnchor { get; }
            public string RoomTitle { get; }
            public string[] ObjectiveInstructions { get; }
            public string SpriteFile { get; }
        }

        private readonly struct ExteriorLayout
        {
            public ExteriorLayout(
                Vector3 checkpointPosition,
                Vector3 padAPosition,
                Vector3 padBPosition,
                Vector3 approachPosition,
                Vector3 boardingPosition,
                Bounds playAreaBounds,
                Transform boardingParent,
                GameObject recoveryTrigger)
            {
                CheckpointPosition = checkpointPosition;
                PadAPosition = padAPosition;
                PadBPosition = padBPosition;
                ApproachPosition = approachPosition;
                BoardingPosition = boardingPosition;
                PlayAreaBounds = playAreaBounds;
                BoardingParent = boardingParent;
                RecoveryTrigger = recoveryTrigger;
            }

            public Vector3 CheckpointPosition { get; }
            public Vector3 PadAPosition { get; }
            public Vector3 PadBPosition { get; }
            public Vector3 ApproachPosition { get; }
            public Vector3 BoardingPosition { get; }
            public Bounds PlayAreaBounds { get; }
            public Transform BoardingParent { get; }
            public GameObject RecoveryTrigger { get; }
        }

        private readonly struct TutorialStations
        {
            public TutorialStations(
                NetworkTutorialMiniGameStation trainingA,
                NetworkTutorialMiniGameStation trainingB,
                NetworkTutorialInteractionStation boarding)
            {
                TrainingA = trainingA;
                TrainingB = trainingB;
                Boarding = boarding;
            }

            public NetworkTutorialMiniGameStation TrainingA { get; }
            public NetworkTutorialMiniGameStation TrainingB { get; }
            public NetworkTutorialInteractionStation Boarding { get; }
        }
    }
}
