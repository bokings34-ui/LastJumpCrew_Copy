using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkTutorialRoomAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string SequenceRootName =
            "PHS_NetworkTutorialRoomSequence";
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
        private const string InteractionStationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialInteractionStation.prefab";
        private const string InstructionFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/TutorialInstructions/";
        private const string TutorialFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const int EnvironmentLastSegmentIndex = 15;
        private const float EnvironmentModuleSize = 3.6f;
        private const float TutorialEndCapZ = 54.27f;

        private static readonly RoomSpec[] Specs =
        {
            new(
                "01_MoveJump",
                new[]
                {
                    TutorialActionKind.Move,
                    TutorialActionKind.Jump,
                    TutorialActionKind.Move,
                    TutorialActionKind.Jump
                },
                4.5f,
                9f,
                "CLEAR BOTH BARRIERS MARKED 1 AND 2",
                "PHS_Tutorial_Jump.png"),
            new(
                "02_Thruster",
                new[]
                {
                    TutorialActionKind.Thruster,
                    TutorialActionKind.Thruster
                },
                13.5f,
                18f,
                "THRUST THROUGH CYAN POINTS 1 AND 2",
                "PHS_Tutorial_Thruster.png"),
            new(
                "03_Grapple",
                new[]
                {
                    TutorialActionKind.Grapple,
                    TutorialActionKind.Grapple
                },
                22.5f,
                27f,
                "HOLD Q: GRAPPLE ANCHORS 1 AND 2",
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
                36f,
                "PLACE WRENCH IN 1 / BATTERY IN 2",
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
                45f,
                "USE WRENCH 1 / EXTINGUISHER 2",
                "PHS_Tutorial_Swap.png"),
            new(
                "06_IncidentResponse",
                new[]
                {
                    TutorialActionKind.Interaction,
                    TutorialActionKind.Interaction
                },
                49.5f,
                54f,
                "REPAIR INCIDENT TERMINALS 1 AND 2",
                "PHS_Tutorial_Interact.png")
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Tutorial Rooms")]
        public static void Author()
        {
            RequireAssets();
            ImportInstructionSprites();
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var oldRoot = FindNamedRoot(scene, SequenceRootName);
                if (oldRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldRoot);
                }

                var sequenceRoot = new GameObject(SequenceRootName);
                SceneManager.MoveGameObjectToScene(sequenceRoot, scene);
                var rooms = new NetworkTutorialRoomController[Specs.Length];
                for (var index = 0; index < Specs.Length; index++)
                {
                    rooms[index] = CreateRoom(
                        scene,
                        sequenceRoot.transform,
                        Specs[index],
                        index);
                }

                CreateInteriorShell(scene, sequenceRoot.transform);
                RemoveLegacyPracticeItems(scene);
                CreatePracticeItems(scene, sequenceRoot.transform);
                RepositionPracticeVolumes(scene, sequenceRoot.transform);
                var interactionStations = CreateInteractionStations(
                    scene,
                    sequenceRoot.transform);
                CreateAndWireRoomObjectives(
                    scene,
                    sequenceRoot.transform,
                    rooms,
                    interactionStations);
                WireDirector(scene, rooms, interactionStations);
                BindTutorialHud(scene);
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

                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static NetworkTutorialRoomController CreateRoom(
            Scene scene,
            Transform sequenceRoot,
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
                out var progressSlider);
            var gate = CreateGate(
                scene,
                roomObject.transform,
                spec.GateZ,
                index,
                out var doorVisual,
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
            serialized.FindProperty("instructionRoot").objectReferenceValue =
                poster;
            serialized.FindProperty("instructionImage").objectReferenceValue =
                instructionImage;
            serialized.FindProperty("instructionSprite").objectReferenceValue =
                instructionImage.sprite;
            serialized.FindProperty("instructionText").objectReferenceValue =
                instructionText;
            serialized.FindProperty("instruction").stringValue =
                spec.Instruction;
            serialized.FindProperty("instructionProgressSlider")
                .objectReferenceValue = progressSlider;
            serialized.FindProperty("objectiveGuidanceRoot")
                .objectReferenceValue = guidanceRoot;
            serialized.FindProperty("doorTransform").objectReferenceValue =
                doorVisual;
            serialized.FindProperty("doorCollider").objectReferenceValue =
                blocker;
            serialized.FindProperty("doorOpenLocalPosition").vector3Value =
                index == Specs.Length - 1
                    ? new Vector3(0f, 5.8f, 54.27f)
                    : new Vector3(0f, 5.8f, 0f);
            serialized.FindProperty("doorOpenLocalEulerAngles").vector3Value =
                new Vector3(0f, 180f, 0f);
            serialized.FindProperty("doorOpenDuration").floatValue = 0.65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            poster.name = $"InstructionPoster_{spec.Id}";
            gate.name = $"ExitGate_{spec.Id}";
            return room;
        }

        private static GameObject CreatePoster(
            Scene scene,
            Transform parent,
            RoomSpec spec,
            out Image instructionImage,
            out TMP_Text instructionText,
            out Slider progressSlider)
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
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var background = CreateUiImage(
                "PosterBackground",
                canvasObject.transform,
                new Vector2(0.25f, 0.035f),
                new Vector2(0.75f, 0.285f));
            background.color = new Color(0.015f, 0.04f, 0.085f, 0.98f);
            instructionImage = CreateUiImage(
                "ActionImage",
                background.transform,
                new Vector2(0.025f, 0.18f),
                new Vector2(0.34f, 0.94f));
            instructionImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                InstructionFolder + spec.SpriteFile);
            instructionImage.preserveAspect = true;
            instructionImage.color = Color.white;

            instructionText = CreateUiText(
                "InstructionText",
                background.transform,
                new Vector2(0.36f, 0.18f),
                new Vector2(0.975f, 0.94f),
                26f);
            instructionText.text =
                spec.Instruction + "  0/2";
            progressSlider = CreateProgressSlider(background.transform);
            return canvasObject;
        }

        private static GameObject CreateGate(
            Scene scene,
            Transform parent,
            float gateZ,
            int index,
            out Transform doorVisual,
            out Collider blocker)
        {
            var gate = new GameObject($"Gate_{index + 1:00}");
            SceneManager.MoveGameObjectToScene(gate, scene);
            gate.transform.SetParent(parent, false);
            gate.transform.position = new Vector3(
                0f,
                0f,
                index == Specs.Length - 1 ? gateZ + 0.27f : gateZ);

            if (index == Specs.Length - 1)
            {
                var exitDoor = FindNamed(
                    scene,
                    "PHS_NetworkTutorialExitDoor");
                exitDoor.transform.rotation = Quaternion.Euler(
                    0f,
                    180f,
                    0f);
                doorVisual = exitDoor.transform;
                var exitBlocker = gate.AddComponent<BoxCollider>();
                exitBlocker.center = new Vector3(0f, 2f, 0f);
                exitBlocker.size = new Vector3(6f, 4.4f, 0.55f);
                blocker = exitBlocker;
                return gate;
            }

            var door = InstantiatePrefab(DoorPrefabPath, scene, gate.transform);
            door.name = "DoorVisual";
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            door.transform.localScale = Vector3.one * 0.9f;
            doorVisual = door.transform;

            CreateDoubleSidedWall(
                scene,
                gate.transform,
                "GateWall_L",
                -4.5f,
                0f,
                new Vector3(0.9f, 0.9f, 0.9f));
            CreateDoubleSidedWall(
                scene,
                gate.transform,
                "GateWall_R",
                4.5f,
                0f,
                new Vector3(0.9f, 0.9f, 0.9f));
            for (var x = -1; x <= 1; x++)
            {
                CreateDoubleSidedWall(
                    scene,
                    gate.transform,
                    $"GateWall_Upper_{x + 1}",
                    x * 4f,
                    3.6f,
                    new Vector3(1.1f, 0.9f, 0.9f));
            }

            var box = gate.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 2f, 0f);
            box.size = new Vector3(6f, 4.4f, 0.55f);
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

        private static void CreatePracticeItems(Scene scene, Transform parent)
        {
            var root = new GameObject("PHS_TutorialPracticeItems");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            CreateItemPair(scene, root.transform, 30.6f, "Transfer");
            CreateToolPair(scene, root.transform, 39.6f);
            CreateDropZone(scene, root.transform, -1.35f, 33.3f, "A");
            CreateDropZone(scene, root.transform, 1.35f, 33.3f, "B");
        }

        private static void RemoveLegacyPracticeItems(Scene scene)
        {
            var targetPaths = new[]
            {
                WrenchPrefabPath,
                BatteryPrefabPath
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
            wrench.transform.position = new Vector3(-1.35f, 1.25f, z);
            var battery = InstantiatePrefab(BatteryPrefabPath, scene, parent);
            battery.name = $"PHS_TutorialBattery_{suffix}";
            battery.transform.position = new Vector3(1.35f, 1.25f, z);
        }

        private static void CreateToolPair(
            Scene scene,
            Transform parent,
            float z)
        {
            var wrench = InstantiatePrefab(WrenchPrefabPath, scene, parent);
            wrench.name = "PHS_TutorialWrench_ToolUse";
            wrench.transform.position = new Vector3(-1.35f, 1.25f, z);
            var extinguisher = InstantiatePrefab(
                FireExtinguisherPrefabPath,
                scene,
                parent);
            extinguisher.name = "PHS_TutorialExtinguisher_ToolUse";
            extinguisher.transform.position = new Vector3(1.35f, 1.25f, z);
        }

        private static void CreateDropZone(
            Scene scene,
            Transform parent,
            float x,
            float z,
            string suffix)
        {
            var pad = InstantiatePrefab(WallPrefabPath, scene, parent);
            pad.name = $"PHS_TutorialDropZone_{suffix}";
            pad.transform.position = new Vector3(x, 0.08f, z);
            pad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            pad.transform.localScale = new Vector3(0.38f, 0.38f, 0.08f);
        }

        private static void RepositionPracticeVolumes(
            Scene scene,
            Transform parent)
        {
            var zeroGravity = FindComponent<NetworkPlayerGravityArea>(scene);
            zeroGravity.transform.position = new Vector3(0f, 3f, 13.5f);
            var zeroGravityCollider = zeroGravity.GetComponent<BoxCollider>();
            if (zeroGravityCollider == null)
            {
                throw Failure("zero_gravity_collider_missing");
            }

            zeroGravityCollider.size = new Vector3(10f, 6f, 6.75f);
            var grappleTarget = FindNamedOptional(
                    scene,
                    "PHS_NetworkTutorialGrappleTarget")
                ?? FindNamed(
                    scene,
                    "PHS_NetworkTutorialGrappleTarget_A");
            foreach (var oldObjective in grappleTarget.GetComponents<
                         NetworkTutorialGrappleAnchorObjective>())
            {
                UnityEngine.Object.DestroyImmediate(oldObjective);
            }

            grappleTarget.name = "PHS_NetworkTutorialGrappleTarget_A";
            grappleTarget.transform.position = new Vector3(-2.2f, 3f, 22.5f);
            grappleTarget.transform.rotation = Quaternion.Euler(
                0f,
                180f,
                0f);
            var secondTarget = UnityEngine.Object.Instantiate(grappleTarget);
            secondTarget.name = "PHS_NetworkTutorialGrappleTarget_B";
            secondTarget.transform.SetParent(parent, true);
            secondTarget.transform.position = new Vector3(2.2f, 3.8f, 24f);

            CreateJumpBarrier(scene, parent, 3.3f, "A");
            CreateJumpBarrier(scene, parent, 6.6f, "B");
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
            NetworkTutorialInteractionStation[] interactionStations)
        {
            if (rooms.Length != 6 || interactionStations.Length != 2)
            {
                throw Failure("objective_contract_count_invalid");
            }

            var player = FindComponent<NetworkPlayerController>(scene);
            var grapple = player.GetComponent<NetworkPlayerGrappleController>();
            var itemAction = player.GetComponent<
                PHSNetworkItemUseActionController>();
            if (grapple == null || itemAction == null)
            {
                throw Failure("objective_player_component_missing");
            }

            var roomObjectives = new MonoBehaviour[6][];
            roomObjectives[0] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "move_jump_checkpoint_a",
                    new Vector3(0f, 1.25f, 4.2f),
                    new Vector3(5.5f, 2.5f, 0.7f),
                    false),
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "move_jump_checkpoint_b",
                    new Vector3(0f, 1.25f, 7.5f),
                    new Vector3(5.5f, 2.5f, 0.7f),
                    false)
            };

            roomObjectives[1] = new MonoBehaviour[]
            {
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "thruster_checkpoint_a",
                    new Vector3(-2f, 2.8f, 12.45f),
                    new Vector3(2.1f, 2.1f, 1f),
                    true),
                CreateCheckpointObjective(
                    scene,
                    parent,
                    player,
                    "thruster_checkpoint_b",
                    new Vector3(2f, 3.8f, 15.3f),
                    new Vector3(2.1f, 2.1f, 1f),
                    true)
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
                CreateDropZoneObjective(
                    scene,
                    parent,
                    "item_drop_wrench",
                    "wrench",
                    new Vector3(-1.35f, 0.65f, 33.3f)),
                CreateDropZoneObjective(
                    scene,
                    parent,
                    "item_drop_battery",
                    "battery_pack",
                    new Vector3(1.35f, 0.65f, 33.3f))
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
                interactionStations[0],
                interactionStations[1]
            };

            ConfigureInteractionObjective(
                interactionStations[0],
                "incident_terminal_fire");
            ConfigureInteractionObjective(
                interactionStations[1],
                "incident_terminal_power");
            var roomMarkers = CreateObjectiveGuidance(
                scene,
                rooms);

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
                bool requireZeroGravity)
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static GameObject[][] CreateObjectiveGuidance(
            Scene scene,
            NetworkTutorialRoomController[] rooms)
        {
            var captions = new[]
            {
                new[] { "JUMP", "JUMP" },
                new[] { "THRUST", "THRUST" },
                new[] { "GRAPPLE", "GRAPPLE" },
                new[] { "WRENCH ZONE", "BATTERY ZONE" },
                new[] { "USE WRENCH", "USE EXTINGUISHER" },
                new[] { "REPAIR", "REPAIR" }
            };
            var markerPositions = new[]
            {
                new[]
                {
                    new Vector3(-1.5f, 2.2f, 4.2f),
                    new Vector3(0.7f, 2.2f, 7.5f)
                },
                new[]
                {
                    new Vector3(-2f, 3.5f, 12.45f),
                    new Vector3(2f, 4.5f, 15.3f)
                },
                new[]
                {
                    new Vector3(-2.2f, 4f, 22.5f),
                    new Vector3(2.2f, 4.8f, 24f)
                },
                new[]
                {
                    new Vector3(-1.35f, 2f, 33.3f),
                    new Vector3(1.35f, 2f, 33.3f)
                },
                new[]
                {
                    new Vector3(-1.35f, 2.4f, 39.6f),
                    new Vector3(1.35f, 2.4f, 39.6f)
                },
                new[]
                {
                    new Vector3(-2.1f, 2.4f, 49.5f),
                    new Vector3(2.1f, 2.4f, 49.5f)
                }
            };
            var roomMarkers = new GameObject[rooms.Length][];
            for (var roomIndex = 0;
                 roomIndex < rooms.Length;
                 roomIndex++)
            {
                var parent = rooms[roomIndex].transform.Find(
                    "ObjectiveGuidance");
                if (parent == null)
                {
                    throw Failure(
                        $"guidance_root_missing room={rooms[roomIndex].RoomId}");
                }

                roomMarkers[roomIndex] = new[]
                {
                    CreateObjectiveMarker(
                        scene,
                        parent,
                        "1",
                        captions[roomIndex][0],
                        new Color(0.05f, 0.9f, 1f, 1f),
                        markerPositions[roomIndex][0]),
                    CreateObjectiveMarker(
                        scene,
                        parent,
                        "2",
                        captions[roomIndex][1],
                        new Color(1f, 0.65f, 0.08f, 1f),
                        markerPositions[roomIndex][1])
                };
            }

            return roomMarkers;
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
            markerRect.sizeDelta = new Vector2(360f, 150f);
            markerRect.localScale = Vector3.one * 0.0048f;

            var background = CreateUiImage(
                "MarkerBackground",
                marker.transform,
                new Vector2(0f, 0.25f),
                Vector2.one);
            background.color = new Color(0.005f, 0.02f, 0.045f, 0.98f);
            background.raycastTarget = false;

            var accentBar = CreateUiImage(
                "Accent",
                background.transform,
                Vector2.zero,
                new Vector2(0.055f, 1f));
            accentBar.color = accent;
            accentBar.raycastTarget = false;

            var topBar = CreateUiImage(
                "TopAccent",
                background.transform,
                new Vector2(0f, 0.92f),
                Vector2.one);
            topBar.color = accent;
            topBar.raycastTarget = false;

            var numberText = CreateUiText(
                "Number",
                background.transform,
                new Vector2(0.07f, 0.06f),
                new Vector2(0.34f, 0.9f),
                54f);
            numberText.text = number;
            numberText.color = accent;
            numberText.raycastTarget = false;

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
            captionText.raycastTarget = false;

            var pointer = CreateUiText(
                "TargetPointer",
                marker.transform,
                new Vector2(0.35f, 0f),
                new Vector2(0.65f, 0.32f),
                52f);
            pointer.text = "V";
            pointer.color = accent;
            pointer.raycastTarget = false;
            return marker;
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
                Scene scene,
                Transform parent,
                string objectiveId,
                string expectedItemId,
                Vector3 position)
        {
            var root = new GameObject($"PHS_TutorialObjective_{objectiveId}");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.2f, 1.3f, 2.2f);
            collider.isTrigger = true;
            var objective = root.AddComponent<
                NetworkTutorialItemDropZoneObjective>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("expectedItemId").stringValue =
                expectedItemId;
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
            serialized.FindProperty("requiredActionKind").enumValueIndex =
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
            NetworkTutorialRoomController[] rooms,
            NetworkTutorialInteractionStation[] interactionStations)
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

            foreach (var station in interactionStations)
            {
                var stationSerialized = new SerializedObject(station);
                stationSerialized.FindProperty("tutorialDirector")
                    .objectReferenceValue = director;
                stationSerialized.FindProperty("interactionPrompt")
                    .stringValue = "Repair Incident";
                stationSerialized.FindProperty("singleUse").boolValue = true;
                stationSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static NetworkTutorialInteractionStation[]
            CreateInteractionStations(Scene scene, Transform parent)
        {
            var firstObject = FindNamedOptional(
                    scene,
                    "PHS_NetworkTutorialInteractionStation")
                ?? FindNamed(
                    scene,
                    "PHS_TutorialIncidentTerminal_A");
            firstObject.name = "PHS_TutorialIncidentTerminal_A";
            firstObject.transform.position = new Vector3(-2.1f, 0f, 49.5f);

            var secondObject = InstantiatePrefab(
                InteractionStationPrefabPath,
                scene,
                parent);
            secondObject.name = "PHS_TutorialIncidentTerminal_B";
            secondObject.transform.position = new Vector3(2.1f, 0f, 49.5f);

            return new[]
            {
                firstObject.GetComponent<NetworkTutorialInteractionStation>(),
                secondObject.GetComponent<NetworkTutorialInteractionStation>()
            };
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

        private static Slider CreateProgressSlider(Transform parent)
        {
            var root = CreateRect(
                "Progress",
                parent,
                new Vector2(0.08f, 0.04f),
                new Vector2(0.92f, 0.11f));
            var background = root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.13f, 0.2f, 1f);
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
            fill.color = new Color(0.1f, 0.9f, 1f, 1f);
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
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
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
                         InteractionStationPrefabPath
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
                float gateZ,
                string instruction,
                string spriteFile)
            {
                Id = id;
                Actions = actions;
                CenterZ = centerZ;
                GateZ = gateZ;
                Instruction = instruction;
                SpriteFile = spriteFile;
            }

            public string Id { get; }
            public TutorialActionKind[] Actions { get; }
            public float CenterZ { get; }
            public float GateZ { get; }
            public string Instruction { get; }
            public string SpriteFile { get; }
        }
    }
}
