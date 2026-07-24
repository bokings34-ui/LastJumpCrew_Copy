using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkTutorialAuthoring
    {
        private const string SceneFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial";
        private const string ScenePath = SceneFolder +
            "/PHS_NetworkTutorialScene.unity";
        private const string SourcePlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string TutorialPrefabFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial";
        private const string TutorialMaterialFolder = TutorialPrefabFolder +
            "/Materials";
        private const string TutorialPlayerPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialPlayer.prefab";
        private const string StationPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialInteractionStation.prefab";
        private const string NetworkPlayHudPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab";
        private const string ShopWallSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/Imported/Ithappy/Sci-Fi_Props/Prefabs/Wall/Wall_type_C_049.prefab";
        private const string ShopDoorSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/Imported/Ithappy/Sci-Fi_Props/Prefabs/Door/Door_008.prefab";
        private const string ShopDisplayDeskSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/PHS_ShopDisplayDesk_Shared.prefab";
        private const string ShopWorkstationSourcePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Tripo/ParkHanSol_Tripo_workstation.prefab";
        private const string TutorialWallPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialWall.prefab";
        private const string TutorialDoorPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialDoor.prefab";
        private const string TutorialRoomGatePrefabPath =
            TutorialPrefabFolder +
            "/PHS_NetworkTutorialRoomGate.prefab";
        private const string TutorialDisplayDeskPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialDisplayDesk.prefab";
        private const string WrenchPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_Wrench.prefab";
        private const string BatteryPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_FuturisticBatteryPack.prefab";
        private const string TutorialCompletionAudioName =
            "PHS_NetworkTutorialCompletionAudio";
        private static readonly string TutorialCompletionAudioClipPath =
            PHSCuratedAssetSfxAuthoring.GetCuePath(
                NetworkAudioCue.TutorialComplete);
        private static readonly string TutorialDoorOpenAudioClipPath =
            PHSCuratedAssetSfxAuthoring.DoorOpenPath;
        private static readonly string TutorialUiConfirmAudioClipPath =
            PHSCuratedAssetSfxAuthoring.UiConfirmPath;

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Tutorial")]
        public static void Author()
        {
            RequireTutorialSceneNotLoaded();
            RequireAsset<GameObject>(SourcePlayerPrefabPath);
            RequireAsset<GameObject>(NetworkPlayHudPrefabPath);
            RequireAsset<GameObject>(WrenchPrefabPath);
            RequireAsset<GameObject>(BatteryPrefabPath);
            RequireAsset<GameObject>(ShopWallSourcePrefabPath);
            RequireAsset<GameObject>(ShopDoorSourcePrefabPath);
            RequireAsset<GameObject>(ShopDisplayDeskSourcePrefabPath);
            RequireAsset<GameObject>(ShopWorkstationSourcePrefabPath);
            RequireAsset<AudioClip>(TutorialCompletionAudioClipPath);
            RequireAsset<AudioClip>(TutorialDoorOpenAudioClipPath);
            RequireAsset<AudioClip>(TutorialUiConfirmAudioClipPath);
            EnsureFolder(SceneFolder);
            EnsureFolder(TutorialPrefabFolder);
            EnsureFolder(TutorialMaterialFolder);
            CreateModularEnvironmentPrefabs();
            CreateRoomGatePrefab();
            CreateTutorialPlayerPrefab();
            CreateInteractionStationPrefab();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"PHS_NETWORK_TUTORIAL_AUTHORING_OK scene={ScenePath}");
        }

        private static void CreateTutorialPlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    TutorialPlayerPrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(
                        SourcePlayerPrefabPath,
                        TutorialPlayerPrefabPath))
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=prefab_copy_failed source={SourcePlayerPrefabPath} target={TutorialPlayerPrefabPath}");
                }

                AssetDatabase.ImportAsset(
                    TutorialPlayerPrefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            var root = PrefabUtility.LoadPrefabContents(TutorialPlayerPrefabPath);
            try
            {
                foreach (var resultController in root.GetComponentsInChildren<
                             NetworkRunResultPanelController>(true))
                {
                    UnityEngine.Object.DestroyImmediate(resultController);
                }

                var resultPanel = FindChild(root.transform,
                    "PHS_NetworkRunResultPanel");
                if (resultPanel != null)
                {
                    UnityEngine.Object.DestroyImmediate(resultPanel.gameObject);
                }

                var ownerPause = FindChild(root.transform,
                    "PHS_NetworkOwnerPauseUI");
                if (ownerPause != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownerPause.gameObject);
                }

                ConfigureTutorialCompletionAudio(root);
                root.name = "PHS_NetworkTutorialPlayer";
                PrefabUtility.SaveAsPrefabAsset(root, TutorialPlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateInteractionStationPrefab()
        {
            CopyPrefabPreservingGuid(
                ShopWorkstationSourcePrefabPath,
                StationPrefabPath,
                "PHS_NetworkTutorialInteractionStation");
            var root = PrefabUtility.LoadPrefabContents(StationPrefabPath);
            try
            {
                if (root.GetComponentInChildren<Collider>(true) == null)
                {
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0f, 0.5f, 0f);
                    collider.size = new Vector3(1.2f, 1.2f, 0.8f);
                }

                if (root.GetComponent<NetworkTutorialInteractionStation>() == null)
                {
                    root.AddComponent<NetworkTutorialInteractionStation>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, StationPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateModularEnvironmentPrefabs()
        {
            CopyPrefabPreservingGuid(
                ShopWallSourcePrefabPath,
                TutorialWallPrefabPath,
                "PHS_NetworkTutorialWall");
            CopyPrefabPreservingGuid(
                ShopDoorSourcePrefabPath,
                TutorialDoorPrefabPath,
                "PHS_NetworkTutorialDoor");
            CopyPrefabPreservingGuid(
                ShopDisplayDeskSourcePrefabPath,
                TutorialDisplayDeskPrefabPath,
                "PHS_NetworkTutorialDisplayDesk");
        }

        private static void CreateRoomGatePrefab()
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            var prefabScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            prefabScene.name = "PHS_NetworkTutorialRoomGateAuthoring";
            SceneManager.SetActiveScene(prefabScene);
            try
            {
                var root = new GameObject("PHS_NetworkTutorialRoomGate");
                SceneManager.MoveGameObjectToScene(root, prefabScene);

                var doorPanel = InstantiatePrefab(
                    TutorialDoorPrefabPath,
                    prefabScene,
                    Vector3.zero);
                doorPanel.name = "DoorPanel";
                doorPanel.transform.SetParent(root.transform, true);
                doorPanel.transform.localScale = Vector3.one * 0.9f;

                var barrierObject = new GameObject("GateBarrier");
                barrierObject.transform.SetParent(root.transform, false);
                barrierObject.transform.localPosition =
                    new Vector3(0f, 1.8f, 0f);
                var barrier = barrierObject.AddComponent<BoxCollider>();
                barrier.size = new Vector3(3.4f, 3.6f, 0.65f);

                var monitorFrame = CreateCube(
                    prefabScene,
                    "RoomMonitorFrame",
                    new Vector3(0f, 4.65f, -0.28f),
                    new Vector3(4.2f, 2.15f, 0.16f));
                monitorFrame.transform.SetParent(root.transform, true);
                UnityEngine.Object.DestroyImmediate(
                    monitorFrame.GetComponent<Collider>());
                monitorFrame.GetComponent<Renderer>().sharedMaterial =
                    CreateOrUpdateLitMaterial(
                        "PHS_NetworkTutorialRoomMonitor_Material",
                        new Color(0.008f, 0.006f, 0.003f),
                        new Color(0.42f, 0.11f, 0.005f));

                var monitorObject = new GameObject(
                    "RoomMonitor",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                monitorObject.transform.SetParent(root.transform, false);
                var monitorRect = monitorObject.GetComponent<RectTransform>();
                monitorRect.localPosition =
                    new Vector3(0f, 4.65f, -0.38f);
                monitorRect.localRotation = Quaternion.identity;
                monitorRect.localScale = Vector3.one * 0.005f;
                monitorRect.sizeDelta = new Vector2(780f, 390f);
                var canvas = monitorObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 40;
                var scaler = monitorObject.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 14f;

                var monitorPanel = CreatePanel(
                    "MonitorPanel",
                    monitorObject.transform,
                    Vector2.zero,
                    Vector2.one);
                monitorPanel.GetComponent<Image>().color =
                    new Color(0.006f, 0.004f, 0.002f, 0.98f);
                var title = CreateText(
                    "RoomTitle",
                    monitorPanel.transform,
                    new Vector2(0.04f, 0.72f),
                    new Vector2(0.96f, 0.96f),
                    42f);
                title.alignment = TextAlignmentOptions.Left;
                title.color = new Color(1f, 0.78f, 0.18f, 1f);
                var description = CreateText(
                    "RoomDescription",
                    monitorPanel.transform,
                    new Vector2(0.04f, 0.42f),
                    new Vector2(0.96f, 0.72f),
                    25f);
                description.alignment = TextAlignmentOptions.Left;
                description.color = new Color(1f, 0.42f, 0.05f, 1f);
                var status = CreateText(
                    "RoomStatus",
                    monitorPanel.transform,
                    new Vector2(0.04f, 0.24f),
                    new Vector2(0.96f, 0.43f),
                    22f);
                status.alignment = TextAlignmentOptions.Left;
                status.color = new Color(1f, 0.42f, 0.05f, 1f);
                var nextButton = CreateButton(
                    "NextRoomButton",
                    monitorPanel.transform,
                    new Vector2(0.52f, 0.04f),
                    new Vector2(0.96f, 0.23f),
                    "LOCKED · CLEAR ROOM");
                var buttonLabel = RequireSingle<TMP_Text>(
                    nextButton.gameObject);
                var buttonImage = nextButton.GetComponent<Image>();
                buttonLabel.color = Color.black;
                buttonImage.color = new Color(0.28f, 0.1f, 0.01f, 1f);

                var gate = root.AddComponent<NetworkTutorialRoomGate>();
                var audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0.65f;
                audioSource.dopplerLevel = 0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 18f;
                var serializedGate = new SerializedObject(gate);
                serializedGate.FindProperty("doorPanel").objectReferenceValue =
                    doorPanel.transform;
                serializedGate.FindProperty("gateBarrier").objectReferenceValue =
                    barrier;
                serializedGate.FindProperty("nextRoomButton").objectReferenceValue =
                    nextButton;
                serializedGate.FindProperty("titleText").objectReferenceValue =
                    title;
                serializedGate.FindProperty("descriptionText").objectReferenceValue =
                    description;
                serializedGate.FindProperty("statusText").objectReferenceValue =
                    status;
                serializedGate.FindProperty("buttonLabelText").objectReferenceValue =
                    buttonLabel;
                serializedGate.FindProperty("buttonImage").objectReferenceValue =
                    buttonImage;
                serializedGate.FindProperty("audioSource").objectReferenceValue =
                    audioSource;
                serializedGate.FindProperty("doorOpenClip")
                    .objectReferenceValue = RequireAsset<AudioClip>(
                    TutorialDoorOpenAudioClipPath);
                serializedGate.FindProperty("uiConfirmClip")
                    .objectReferenceValue = RequireAsset<AudioClip>(
                    TutorialUiConfirmAudioClipPath);
                serializedGate.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        TutorialRoomGatePrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=room_gate_prefab_save_failed");
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
            finally
            {
                if (previousActiveScene.IsValid()
                    && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                EditorSceneManager.CloseScene(prefabScene, true);
            }
        }

        private static void ConfigureTutorialCompletionAudio(GameObject root)
        {
            var namedRoots = 0;
            GameObject completionAudioObject = null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(
                        child.name,
                        TutorialCompletionAudioName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                namedRoots++;
                completionAudioObject = child.gameObject;
            }

            if (namedRoots > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_audio_root_count count={namedRoots}");
            }

            if (completionAudioObject == null)
            {
                completionAudioObject = new GameObject(
                    TutorialCompletionAudioName);
                completionAudioObject.transform.SetParent(root.transform, false);
            }

            var audioSources = completionAudioObject.GetComponents<AudioSource>();
            if (audioSources.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_audio_source_count count={audioSources.Length}");
            }

            var audioSource = audioSources.Length == 1
                ? audioSources[0]
                : completionAudioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 25f;

            var emitters = completionAudioObject.GetComponents<
                NetworkAudioCueEmitter>();
            if (emitters.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_emitter_on_root_count count={emitters.Length}");
            }

            var emitter = emitters.Length == 1
                ? emitters[0]
                : completionAudioObject.AddComponent<NetworkAudioCueEmitter>();
            var serializedEmitter = new SerializedObject(emitter);
            var audioSourceProperty = serializedEmitter.FindProperty("audioSource");
            var cueBindingsProperty = serializedEmitter.FindProperty("cueBindings");
            if (audioSourceProperty == null || cueBindingsProperty == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_emitter_contract_missing");
            }

            audioSourceProperty.objectReferenceValue = audioSource;
            cueBindingsProperty.arraySize = 1;
            var binding = cueBindingsProperty.GetArrayElementAtIndex(0);
            var cueProperty = binding.FindPropertyRelative("cue");
            var clipProperty = binding.FindPropertyRelative("clip");
            var volumeProperty = binding.FindPropertyRelative("volumeScale");
            var cooldownProperty = binding.FindPropertyRelative(
                "cooldownSeconds");
            if (cueProperty == null
                || clipProperty == null
                || volumeProperty == null
                || cooldownProperty == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_binding_contract_missing");
            }

            cueProperty.intValue = (int)NetworkAudioCue.TutorialComplete;
            clipProperty.objectReferenceValue =
                RequireAsset<AudioClip>(TutorialCompletionAudioClipPath);
            volumeProperty.floatValue = 0.85f;
            cooldownProperty.floatValue = 0.2f;
            serializedEmitter.ApplyModifiedPropertiesWithoutUndo();

            RequireSingleTutorialCompletionEmitter(root);
        }

        private static NetworkAudioCueEmitter
            RequireSingleTutorialCompletionEmitter(GameObject root)
        {
            NetworkAudioCueEmitter result = null;
            var matchingEmitterCount = 0;
            foreach (var emitter in root.GetComponentsInChildren<
                         NetworkAudioCueEmitter>(true))
            {
                var serializedEmitter = new SerializedObject(emitter);
                var cueBindings = serializedEmitter.FindProperty("cueBindings");
                if (cueBindings == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=cue_bindings_property_missing emitter={emitter.name}");
                }

                for (var index = 0; index < cueBindings.arraySize; index++)
                {
                    var binding = cueBindings.GetArrayElementAtIndex(index);
                    var cue = binding.FindPropertyRelative("cue");
                    if (cue == null)
                    {
                        throw new InvalidOperationException(
                            $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=cue_property_missing emitter={emitter.name} index={index}");
                    }

                    if (cue.intValue != (int)NetworkAudioCue.TutorialComplete)
                    {
                        continue;
                    }

                    matchingEmitterCount++;
                    result = emitter;
                }
            }

            if (matchingEmitterCount != 1 || result == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_cue_emitter_count count={matchingEmitterCount}");
            }

            var resultSerialized = new SerializedObject(result);
            var resultBindings = resultSerialized.FindProperty("cueBindings");
            var resultAudioSource = resultSerialized.FindProperty("audioSource");
            var resultClip = resultBindings?.arraySize == 1
                ? resultBindings.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("clip")
                : null;
            if (!string.Equals(
                    result.name,
                    TutorialCompletionAudioName,
                    StringComparison.Ordinal)
                || resultBindings == null
                || resultBindings.arraySize != 1
                || resultAudioSource == null
                || resultAudioSource.objectReferenceValue == null
                || resultClip == null
                || resultClip.objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_completion_emitter_contract_invalid emitter={result.name}");
            }

            return result;
        }

        private static void CopyPrefabPreservingGuid(
            string sourcePath,
            string targetPath,
            string rootName)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
            {
                FileUtil.ReplaceFile(sourcePath, targetPath);
                AssetDatabase.ImportAsset(
                    targetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
            else if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=prefab_copy_failed source={sourcePath} target={targetPath}");
            }

            var root = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                root.name = rootName;
                PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateScene()
        {
            RequireTutorialSceneNotLoaded();
            var previousActiveScene = SceneManager.GetActiveScene();
            var tutorialScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            tutorialScene.name = "PHS_NetworkTutorialScene";
            SceneManager.SetActiveScene(tutorialScene);
            try
            {
                CreateLighting(tutorialScene);
                var zoneRoots = CreateEnvironment(tutorialScene);
                var player = InstantiatePrefab(
                    TutorialPlayerPrefabPath,
                    tutorialScene,
                    new Vector3(0f, 1.1f, 0f));
                player.name = "PHS_NetworkTutorialPlayer";
                EnableLocalPlayerView(player);

                var controller = RequireSingle<NetworkPlayerController>(player);
                var grapple = RequireSingle<NetworkPlayerGrappleController>(player);
                var holder = RequireSingle<TempPlayerItemHolder>(player);
                var completionAudioEmitter =
                    RequireSingleTutorialCompletionEmitter(player);

                CreateZeroGravityArea(tutorialScene, zoneRoots.ZeroGravity);
                CreateGrappleTarget(tutorialScene, zoneRoots.Grapple);
                var pickupItem = InstantiatePrefab(
                    WrenchPrefabPath,
                    tutorialScene,
                    new Vector3(0f, 2.25f, 21.6f));
                pickupItem.name = "PHS_NetworkTutorialPickupItem";
                pickupItem.transform.SetParent(zoneRoots.ItemPickup, true);
                var swapItemA = InstantiatePrefab(
                    WrenchPrefabPath,
                    tutorialScene,
                    new Vector3(-1.5f, 2.25f, 36f));
                swapItemA.name = "PHS_NetworkTutorialSwapItem_A";
                swapItemA.transform.SetParent(zoneRoots.ItemSwap, true);
                var swapItemB = InstantiatePrefab(
                    BatteryPrefabPath,
                    tutorialScene,
                    new Vector3(1.5f, 2.25f, 36f));
                swapItemB.name = "PHS_NetworkTutorialSwapItem_B";
                swapItemB.transform.SetParent(zoneRoots.ItemSwap, true);

                CreateTutorialUi(
                    tutorialScene,
                    out var instructionText,
                    out var completionPanel,
                    out var returnButton);
                var playHud = InstantiatePrefab(
                    NetworkPlayHudPrefabPath,
                    tutorialScene,
                    Vector3.zero);
                RemoveTutorialOnlyVoiceHudBinding(playHud);
                playHud.name = "PHS_NetworkPlayHudUI";
                var hudPresenter = RequireSingle<ParkHanSolPlayHudMockPresenter>(
                    playHud);
                BindTutorialHud(player, controller, holder, hudPresenter);
                var directorObject = new GameObject("PHS_NetworkTutorialDirector");
                SceneManager.MoveGameObjectToScene(directorObject, tutorialScene);
                var director = directorObject.AddComponent<NetworkTutorialDirector>();
                var directorSerialized = new SerializedObject(director);
                directorSerialized.FindProperty("playerController").objectReferenceValue = controller;
                directorSerialized.FindProperty("grappleController").objectReferenceValue = grapple;
                directorSerialized.FindProperty("itemHolder").objectReferenceValue = holder;
                directorSerialized.FindProperty("instructionText").objectReferenceValue = instructionText;
                directorSerialized.FindProperty("completionPanel").objectReferenceValue = completionPanel;
                directorSerialized.FindProperty("returnToLobbyButton").objectReferenceValue = returnButton;
                directorSerialized.FindProperty("audioCuePlayerSource").objectReferenceValue =
                    completionAudioEmitter;
                directorSerialized.ApplyModifiedPropertiesWithoutUndo();

                CreateRoomGates(tutorialScene, zoneRoots, director);

                var station = InstantiatePrefab(
                    StationPrefabPath,
                    tutorialScene,
                    new Vector3(0f, 1f, 44.2f));
                var stationComponent = RequireSingle<
                    NetworkTutorialInteractionStation>(station);
                station.name = "PHS_NetworkTutorialInteractionStation";
                station.transform.SetParent(zoneRoots.Interaction, true);
                var stationSerialized = new SerializedObject(stationComponent);
                stationSerialized.FindProperty("tutorialDirector").objectReferenceValue = director;
                stationSerialized.ApplyModifiedPropertiesWithoutUndo();

                CreateCompletionRoomTrigger(
                    tutorialScene,
                    zoneRoots.Complete,
                    director,
                    controller);

                RequireTutorialSceneNotLoaded();
                if (!EditorSceneManager.SaveScene(tutorialScene, ScenePath))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=scene_save_failed");
                }
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                EditorSceneManager.CloseScene(tutorialScene, true);
            }
        }

        private static void RequireTutorialSceneNotLoaded()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && string.Equals(
                        scene.path,
                        ScenePath,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_scene_already_loaded");
                }
            }
        }

        private static void CreateLighting(Scene scene)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.018f, 0.035f, 0.055f);
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.09f, 0.14f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.07f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.022f, 0.032f);
            RenderSettings.ambientIntensity = 0.72f;

            var lightObject = new GameObject("PHS_NetworkTutorialKeyLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.7f, 0.86f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.Soft;
        }

        private static TutorialZoneRoots CreateEnvironment(Scene scene)
        {
            var environment = new GameObject("PHS_NetworkTutorialEnvironment");
            SceneManager.MoveGameObjectToScene(environment, scene);
            var zoneRoots = CreateZoneRoots(scene, environment.transform);

            for (var zIndex = 0; zIndex < 16; zIndex++)
            {
                var z = zIndex * 3.6f;
                var zoneRoot = zoneRoots.ForSegment(zIndex);
                for (var xIndex = -1; xIndex <= 1; xIndex++)
                {
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        zoneRoot,
                        $"PHS_NetworkTutorialFloor_{zIndex}_{xIndex + 1}",
                        new Vector3(xIndex * 3.6f, 0f, z - 1.8f),
                        new Vector3(90f, 0f, 0f));
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        zoneRoot,
                        $"PHS_NetworkTutorialCeiling_{zIndex}_{xIndex + 1}",
                        new Vector3(xIndex * 3.6f, 7.2f, z - 1.8f),
                        new Vector3(270f, 0f, 0f));
                }

                for (var yIndex = 0; yIndex <= 1; yIndex++)
                {
                    var y = yIndex * 3.6f;
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        zoneRoot,
                        $"PHS_NetworkTutorialWall_L_{zIndex}_{yIndex}",
                        new Vector3(-5.67f, y, z),
                        new Vector3(0f, 90f, 0f));
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        zoneRoot,
                        $"PHS_NetworkTutorialWall_R_{zIndex}_{yIndex}",
                        new Vector3(5.67f, y, z),
                        new Vector3(0f, 270f, 0f));
                }
            }

            foreach (var boundary in new[]
                     {
                         (Root: zoneRoots.Movement, Name: "Movement", Z: 3.6f),
                         (Root: zoneRoots.ZeroGravity, Name: "ZeroG", Z: 10.8f),
                         (Root: zoneRoots.Grapple, Name: "Grapple", Z: 18f),
                         (Root: zoneRoots.ItemPickup, Name: "ItemPickup", Z: 25.2f),
                         (Root: zoneRoots.ItemDrop, Name: "ItemDrop", Z: 32.4f),
                         (Root: zoneRoots.ItemSwap, Name: "ItemSwap", Z: 39.6f),
                         (Root: zoneRoots.Interaction, Name: "Interaction", Z: 46.8f),
                         (Root: zoneRoots.Complete, Name: "Complete", Z: 54f)
                     })
            {
                CreateRoomBoundaryWalls(
                    scene,
                    boundary.Root,
                    boundary.Name,
                    boundary.Z);
            }

            CreateClosedRoomWall(
                scene,
                zoneRoots.Movement,
                "Start",
                -3.6f);

            CreateEnvironmentInstance(
                TutorialDisplayDeskPrefabPath,
                scene,
                zoneRoots.ItemPickup,
                "PHS_NetworkTutorialItemPickupDesk",
                new Vector3(0f, 0f, 21.6f),
                Vector3.zero);
            CreateEnvironmentInstance(
                TutorialDisplayDeskPrefabPath,
                scene,
                zoneRoots.ItemSwap,
                "PHS_NetworkTutorialItemSwapDesk_L",
                new Vector3(-1.5f, 0f, 36f),
                Vector3.zero);
            CreateEnvironmentInstance(
                TutorialDisplayDeskPrefabPath,
                scene,
                zoneRoots.ItemSwap,
                "PHS_NetworkTutorialItemSwapDesk_R",
                new Vector3(1.5f, 0f, 36f),
                Vector3.zero);

            var dropTarget = CreateCube(
                scene,
                "PHS_NetworkTutorialItemDropTarget",
                new Vector3(0f, 0.16f, 28.8f),
                new Vector3(4.2f, 0.18f, 4.2f));
            dropTarget.transform.SetParent(zoneRoots.ItemDrop, true);
            UnityEngine.Object.DestroyImmediate(
                dropTarget.GetComponent<Collider>());
            dropTarget.GetComponent<Renderer>().sharedMaterial =
                CreateOrUpdateLitMaterial(
                    "PHS_NetworkTutorialItemDropTarget_Material",
                    new Color(0.12f, 0.025f, 0.16f),
                    new Color(1.8f, 0.16f, 2.2f));

            CreateZoneVisuals(
                scene,
                zoneRoots.Movement,
                "Movement",
                new Vector3(0f, 3.6f, 0f),
                new Color(0.18f, 0.72f, 0.9f),
                LightType.Spot);
            CreateZoneVisuals(
                scene,
                zoneRoots.ZeroGravity,
                "ZeroG",
                new Vector3(0f, 3.6f, 7.2f),
                new Color(0.22f, 0.42f, 1f),
                LightType.Point);
            CreateZoneVisuals(
                scene,
                zoneRoots.Grapple,
                "Grapple",
                new Vector3(0f, 3.6f, 14.4f),
                new Color(1f, 0.28f, 0.04f),
                LightType.Point);
            CreateZoneVisuals(
                scene,
                zoneRoots.ItemPickup,
                "ItemPickup",
                new Vector3(0f, 3.6f, 21.6f),
                new Color(0.2f, 0.92f, 0.52f),
                LightType.Spot);
            CreateZoneVisuals(
                scene,
                zoneRoots.ItemDrop,
                "ItemDrop",
                new Vector3(0f, 3.6f, 28.8f),
                new Color(0.82f, 0.25f, 0.9f),
                LightType.Point);
            CreateZoneVisuals(
                scene,
                zoneRoots.ItemSwap,
                "ItemSwap",
                new Vector3(0f, 3.6f, 36f),
                new Color(0.18f, 0.86f, 0.78f),
                LightType.Spot);
            CreateZoneVisuals(
                scene,
                zoneRoots.Interaction,
                "Interaction",
                new Vector3(0f, 3.6f, 43.2f),
                new Color(1f, 0.72f, 0.12f),
                LightType.Point);
            CreateZoneVisuals(
                scene,
                zoneRoots.Complete,
                "Complete",
                new Vector3(0f, 3.6f, 50.4f),
                new Color(0.4f, 1f, 0.56f),
                LightType.Spot);

            CreateReflectionProbe(
                scene,
                environment.transform,
                "PHS_NetworkTutorialReflectionProbe_Entry",
                new Vector3(0f, 3.5f, 13.5f),
                new Vector3(10.5f, 7f, 27f));
            CreateReflectionProbe(
                scene,
                environment.transform,
                "PHS_NetworkTutorialReflectionProbe_Exit",
                new Vector3(0f, 3.5f, 40.5f),
                new Vector3(10.5f, 7f, 27f));

            return zoneRoots;
        }

        private static TutorialZoneRoots CreateZoneRoots(
            Scene scene,
            Transform environment)
        {
            return new TutorialZoneRoots(
                CreateZoneRoot(scene, environment, "Movement"),
                CreateZoneRoot(scene, environment, "ZeroG"),
                CreateZoneRoot(scene, environment, "Grapple"),
                CreateZoneRoot(scene, environment, "ItemPickup"),
                CreateZoneRoot(scene, environment, "ItemDrop"),
                CreateZoneRoot(scene, environment, "ItemSwap"),
                CreateZoneRoot(scene, environment, "Interaction"),
                CreateZoneRoot(scene, environment, "Complete"));
        }

        private static Transform CreateZoneRoot(
            Scene scene,
            Transform parent,
            string zoneName)
        {
            var zone = new GameObject($"PHS_NetworkTutorialZone_{zoneName}");
            SceneManager.MoveGameObjectToScene(zone, scene);
            zone.transform.SetParent(parent, false);
            return zone.transform;
        }

        private static void CreateZoneVisuals(
            Scene scene,
            Transform zoneRoot,
            string zoneName,
            Vector3 center,
            Color color,
            LightType lightType)
        {
            var landmark = CreateCube(
                scene,
                $"PHS_NetworkTutorialLandmark_{zoneName}",
                new Vector3(-4.65f, 2.4f, center.z),
                new Vector3(0.28f, 3.8f, 0.45f));
            landmark.transform.SetParent(zoneRoot, true);
            UnityEngine.Object.DestroyImmediate(landmark.GetComponent<Collider>());
            landmark.GetComponent<Renderer>().sharedMaterial =
                CreateOrUpdateLitMaterial(
                    $"PHS_NetworkTutorialLandmark_{zoneName}_Material",
                    color * 0.28f,
                    color * 2.4f);

            var lightObject = new GameObject(
                $"PHS_NetworkTutorialZoneLight_{zoneName}");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.SetParent(zoneRoot, true);
            lightObject.transform.position = new Vector3(center.x, 5.9f, center.z);
            if (lightType == LightType.Spot)
            {
                lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            var light = lightObject.AddComponent<Light>();
            light.type = lightType;
            light.color = color;
            light.intensity = lightType == LightType.Spot ? 11f : 7f;
            light.range = 9f;
            light.shadows = LightShadows.Soft;
            if (lightType == LightType.Spot)
            {
                light.spotAngle = 72f;
                light.innerSpotAngle = 42f;
            }
        }

        private static void CreateReflectionProbe(
            Scene scene,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size)
        {
            var probeObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(probeObject, scene);
            probeObject.transform.SetParent(parent, true);
            probeObject.transform.position = position;
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.boxProjection = true;
            probe.size = size;
            probe.blendDistance = 2f;
            probe.intensity = 0.9f;
            probe.resolution = 128;
        }

        private static GameObject CreateEnvironmentInstance(
            string prefabPath,
            Scene scene,
            Transform parent,
            string instanceName,
            Vector3 position,
            Vector3 eulerAngles)
        {
            var instance = InstantiatePrefab(prefabPath, scene, position);
            instance.name = instanceName;
            instance.transform.SetParent(parent, true);
            instance.transform.rotation = Quaternion.Euler(eulerAngles);
            instance.transform.localScale = Vector3.one * 0.9f;
            return instance;
        }

        private static void CreateRoomBoundaryWalls(
            Scene scene,
            Transform parent,
            string roomName,
            float z)
        {
            foreach (var x in new[] { -4.5f, 4.5f })
            {
                CreateEnvironmentInstance(
                    TutorialWallPrefabPath,
                    scene,
                    parent,
                    $"PHS_NetworkTutorialBoundary_{roomName}_" +
                    (x < 0f ? "L" : "R"),
                    new Vector3(x, 0f, z),
                    Vector3.zero);
            }

            for (var xIndex = -1; xIndex <= 1; xIndex++)
            {
                CreateEnvironmentInstance(
                    TutorialWallPrefabPath,
                    scene,
                    parent,
                    $"PHS_NetworkTutorialBoundary_{roomName}_" +
                    $"Upper_{xIndex + 1}",
                    new Vector3(xIndex * 3.6f, 3.6f, z),
                    Vector3.zero);
            }
        }

        private static void CreateClosedRoomWall(
            Scene scene,
            Transform parent,
            string wallName,
            float z)
        {
            for (var yIndex = 0; yIndex <= 1; yIndex++)
            {
                for (var xIndex = -1; xIndex <= 1; xIndex++)
                {
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        parent,
                        $"PHS_NetworkTutorialWall_{wallName}_" +
                        $"{yIndex}_{xIndex + 1}",
                        new Vector3(
                            xIndex * 3.6f,
                            yIndex * 3.6f,
                            z),
                        Vector3.zero);
                }
            }
        }

        private static void CreateRoomGates(
            Scene scene,
            TutorialZoneRoots zoneRoots,
            NetworkTutorialDirector director)
        {
            var definitions = new[]
            {
                (Room: NetworkTutorialRoom.Movement,
                    Parent: zoneRoots.Movement,
                    Name: "Movement",
                    Z: 3.6f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.ZeroGravity,
                    Parent: zoneRoots.ZeroGravity,
                    Name: "ZeroGravity",
                    Z: 10.8f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.Grapple,
                    Parent: zoneRoots.Grapple,
                    Name: "Grapple",
                    Z: 18f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.ItemPickup,
                    Parent: zoneRoots.ItemPickup,
                    Name: "ItemPickup",
                    Z: 25.2f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.ItemDrop,
                    Parent: zoneRoots.ItemDrop,
                    Name: "ItemDrop",
                    Z: 32.4f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.ItemSwap,
                    Parent: zoneRoots.ItemSwap,
                    Name: "ItemSwap",
                    Z: 39.6f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.Interaction,
                    Parent: zoneRoots.Interaction,
                    Name: "Interaction",
                    Z: 46.8f,
                    Terminal: false),
                (Room: NetworkTutorialRoom.Complete,
                    Parent: zoneRoots.Complete,
                    Name: "Complete",
                    Z: 54f,
                    Terminal: true)
            };

            foreach (var definition in definitions)
            {
                var instance = InstantiatePrefab(
                    TutorialRoomGatePrefabPath,
                    scene,
                    new Vector3(0f, 0f, definition.Z));
                instance.name =
                    $"PHS_NetworkTutorialRoomGate_{definition.Name}";
                instance.transform.SetParent(definition.Parent, true);
                var gate = RequireSingle<NetworkTutorialRoomGate>(instance);
                var serializedGate = new SerializedObject(gate);
                serializedGate.FindProperty("tutorialDirector")
                    .objectReferenceValue = director;
                serializedGate.FindProperty("room").enumValueIndex =
                    (int)definition.Room;
                serializedGate.FindProperty("isTerminal").boolValue =
                    definition.Terminal;
                serializedGate.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateCompletionRoomTrigger(
            Scene scene,
            Transform parent,
            NetworkTutorialDirector director,
            NetworkPlayerController playerController)
        {
            var triggerObject = new GameObject(
                "PHS_NetworkTutorialCompleteRoomTrigger");
            SceneManager.MoveGameObjectToScene(triggerObject, scene);
            triggerObject.transform.SetParent(parent, true);
            triggerObject.transform.position =
                new Vector3(0f, 3f, 50.4f);
            var collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 6f, 6.4f);
            var trigger = triggerObject.AddComponent<
                NetworkTutorialCompletionRoomTrigger>();
            var serializedTrigger = new SerializedObject(trigger);
            serializedTrigger.FindProperty("tutorialDirector")
                .objectReferenceValue = director;
            serializedTrigger.FindProperty("playerController")
                .objectReferenceValue = playerController;
            serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateZeroGravityArea(Scene scene, Transform parent)
        {
            var area = new GameObject("PHS_NetworkTutorialZeroGravity");
            SceneManager.MoveGameObjectToScene(area, scene);
            area.transform.SetParent(parent, true);
            area.transform.position = new Vector3(0f, 3f, 7.2f);
            var collider = area.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 6f, 6.4f);
            var gravityArea = area.AddComponent<NetworkPlayerGravityArea>();
            var serialized = new SerializedObject(gravityArea);
            serialized.FindProperty("gravityMode").enumValueIndex =
                (int)NetworkPlayerGravityMode.Spacewalk;
            serialized.FindProperty("priority").intValue = 10;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateGrappleTarget(Scene scene, Transform parent)
        {
            var target = InstantiatePrefab(
                TutorialWallPrefabPath,
                scene,
                new Vector3(0f, 3f, 14.4f));
            target.name = "PHS_NetworkTutorialGrappleTarget";
            target.transform.SetParent(parent, true);
            target.transform.localScale = Vector3.one * 0.5f;
            target.GetComponent<Renderer>().sharedMaterial =
                CreateOrUpdateLitMaterial(
                    "PHS_NetworkTutorialGrappleTarget_Material",
                    new Color(0.45f, 0.07f, 0.01f),
                    new Color(3.5f, 0.48f, 0.04f));
            var markerLight = target.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.color = new Color(1f, 0.35f, 0.05f);
            markerLight.intensity = 5f;
            markerLight.range = 7f;
        }

        private static Material CreateOrUpdateLitMaterial(
            string name,
            Color baseColor,
            Color emissionColor)
        {
            const string shaderName = "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=shader_missing shader={shaderName}");
            }

            if (shader.FindPropertyIndex("_BaseColor") < 0
                || shader.FindPropertyIndex("_EmissionColor") < 0)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=shader_property_missing shader={shaderName}");
            }

            var materialPath = $"{TutorialMaterialFolder}/{name}.mat";
            var existingAsset = AssetDatabase.LoadMainAssetAtPath(materialPath);
            if (existingAsset != null && existingAsset is not Material)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=material_asset_type_invalid path={materialPath}");
            }

            var material = existingAsset as Material;
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = name
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        private sealed class TutorialZoneRoots
        {
            public TutorialZoneRoots(
                Transform movement,
                Transform zeroGravity,
                Transform grapple,
                Transform itemPickup,
                Transform itemDrop,
                Transform itemSwap,
                Transform interaction,
                Transform complete)
            {
                Movement = movement;
                ZeroGravity = zeroGravity;
                Grapple = grapple;
                ItemPickup = itemPickup;
                ItemDrop = itemDrop;
                ItemSwap = itemSwap;
                Interaction = interaction;
                Complete = complete;
            }

            public Transform Movement { get; }
            public Transform ZeroGravity { get; }
            public Transform Grapple { get; }
            public Transform ItemPickup { get; }
            public Transform ItemDrop { get; }
            public Transform ItemSwap { get; }
            public Transform Interaction { get; }
            public Transform Complete { get; }

            public Transform ForSegment(int segmentIndex)
            {
                if (segmentIndex < 0 || segmentIndex > 15)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(segmentIndex),
                        segmentIndex,
                        "Tutorial segment index must be between 0 and 15.");
                }

                if (segmentIndex <= 1)
                {
                    return Movement;
                }

                if (segmentIndex <= 3)
                {
                    return ZeroGravity;
                }

                if (segmentIndex <= 5)
                {
                    return Grapple;
                }

                if (segmentIndex <= 7)
                {
                    return ItemPickup;
                }

                if (segmentIndex <= 9)
                {
                    return ItemDrop;
                }

                if (segmentIndex <= 11)
                {
                    return ItemSwap;
                }

                return segmentIndex <= 13 ? Interaction : Complete;
            }
        }

        private static void CreateTutorialUi(
            Scene scene,
            out TMP_Text instructionText,
            out GameObject completionPanel,
            out Button returnButton)
        {
            var canvasObject = new GameObject(
                "PHS_NetworkTutorialUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            instructionText = CreateText(
                "Instruction",
                canvasObject.transform,
                new Vector2(0.15f, 0.82f),
                new Vector2(0.85f, 0.96f),
                34f);

            completionPanel = CreatePanel(
                "Completion Panel",
                canvasObject.transform,
                new Vector2(0.3f, 0.3f),
                new Vector2(0.7f, 0.7f));
            CreateText(
                "Completion Title",
                completionPanel.transform,
                new Vector2(0.1f, 0.55f),
                new Vector2(0.9f, 0.88f),
                46f).text = "TRAINING COMPLETE";
            returnButton = CreateButton(
                "Return To Lobby",
                completionPanel.transform,
                new Vector2(0.25f, 0.15f),
                new Vector2(0.75f, 0.38f),
                "RETURN TO LOBBY");
            completionPanel.SetActive(false);
        }

        private static void BindTutorialHud(
            GameObject player,
            NetworkPlayerController controller,
            TempPlayerItemHolder holder,
            ParkHanSolPlayHudMockPresenter presenter)
        {
            SetObjectReference(controller, "playHudPresenter", presenter);
            SetObjectReference(holder, "playHudPresenter", presenter);
            SetObjectReference(
                RequireSingle<TempPlayerInteractionScanner>(player),
                "playHudPresenter",
                presenter);
        }

        private static void RemoveTutorialOnlyVoiceHudBinding(GameObject playHud)
        {
            var binder = RequireSingle<ParkHanSolSpeakingPlayerHudBinder>(playHud);
            UnityEngine.Object.DestroyImmediate(binder);
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=serialized_property_missing target={target.name} property={propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateCube(
            Scene scene,
            string name,
            Vector3 position,
            Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            SceneManager.MoveGameObjectToScene(cube, scene);
            return cube;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var panel = CreateRect(name, parent, anchorMin, anchorMax);
            panel.AddComponent<Image>().color =
                new Color(0.02f, 0.06f, 0.09f, 0.96f);
            return panel;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize)
        {
            var textObject = CreateRect(name, parent, anchorMin, anchorMax);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label)
        {
            var buttonObject = CreateRect(name, parent, anchorMin, anchorMax);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.62f, 0.76f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText("Label", buttonObject.transform, Vector2.zero,
                Vector2.one, 24f).text = label;
            return button;
        }

        private static GameObject CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            var rect = result.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
        }

        private static GameObject InstantiatePrefab(
            string path,
            Scene scene,
            Vector3 position)
        {
            var prefab = RequireAsset<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = position;
            return instance;
        }

        private static void EnableLocalPlayerView(GameObject player)
        {
            var cameras = player.GetComponentsInChildren<Camera>(true);
            var listeners = player.GetComponentsInChildren<AudioListener>(true);
            if (cameras.Length != 1 || listeners.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=player_view_count cameras={cameras.Length} listeners={listeners.Length}");
            }

            cameras[0].enabled = true;
            listeners[0].enabled = true;
        }

        private static T RequireSingle<T>(GameObject root) where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=component_count type={typeof(T).Name} actual={matches.Length}");
            }

            return matches[0];
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=asset_missing path={path}");
            }

            return asset;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    return child;
                }

                var nested = FindChild(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
