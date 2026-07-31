using System;
using LastJumpCrew.ParkHanSol.Interaction;
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
        private const string TutorialDisplayDeskPrefabPath = TutorialPrefabFolder +
            "/PHS_NetworkTutorialDisplayDesk.prefab";
        private const string WrenchPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_Wrench.prefab";
        private const string BatteryPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_FuturisticBatteryPack.prefab";

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
            EnsureFolder(SceneFolder);
            EnsureFolder(TutorialPrefabFolder);
            CreateModularEnvironmentPrefabs();
            CreateTutorialPlayerPrefab();
            CreateInteractionStationPrefab();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"PHS_NETWORK_TUTORIAL_AUTHORING_OK scene={ScenePath}");
        }

        [MenuItem(
            "Tools/ParkHanSol/BEAVER/Migrate Tutorial Player To Canonical Variant")]
        public static void MigrateTutorialPlayerToCanonicalVariant()
        {
            RequireTutorialSceneNotLoaded();
            RequireAsset<GameObject>(SourcePlayerPrefabPath);
            RequireAsset<GameObject>(TutorialPlayerPrefabPath);
            CreateTutorialPlayerPrefab();
            RebindTutorialScenePlayerReferences();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                TutorialPlayerPrefabPath,
                ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "PHS_NETWORK_TUTORIAL_PLAYER_VARIANT_AUTHORING_OK " +
                $"source={SourcePlayerPrefabPath} target={TutorialPlayerPrefabPath}");
        }

        private static void CreateTutorialPlayerPrefab()
        {
            var sourcePrefab = RequireAsset<GameObject>(SourcePlayerPrefabPath);
            var existingTutorialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TutorialPlayerPrefabPath);
            var targetGuid = AssetDatabase.AssetPathToGUID(
                TutorialPlayerPrefabPath);
            var existingRoot = existingTutorialPrefab != null
                ? PrefabUtility.LoadPrefabContents(TutorialPlayerPrefabPath)
                : null;
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = PrefabUtility.InstantiatePrefab(
                    sourcePrefab,
                    previewScene) as GameObject;
                if (root == null)
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=canonical_player_instantiate_failed");
                }

                if (existingRoot != null)
                {
                    PreserveTutorialCompletionAudio(existingRoot, root);
                    PrefabUtility.UnloadPrefabContents(existingRoot);
                    existingRoot = null;
                }
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

                foreach (var speakingHudBinder in root.GetComponentsInChildren<
                             ParkHanSolSpeakingPlayerHudBinder>(true))
                {
                    UnityEngine.Object.DestroyImmediate(speakingHudBinder);
                }

                foreach (var voiceChatSession in root.GetComponentsInChildren<
                             ProximityVoiceChatSession>(true))
                {
                    UnityEngine.Object.DestroyImmediate(voiceChatSession);
                }

                root.name = "PHS_NetworkTutorialPlayer";
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    TutorialPlayerPrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_variant_save_failed");
                }

                var savedGuid = AssetDatabase.AssetPathToGUID(
                    TutorialPlayerPrefabPath);
                if (!string.IsNullOrWhiteSpace(targetGuid)
                    && !string.Equals(
                        targetGuid,
                        savedGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED " +
                        $"reason=tutorial_prefab_guid_changed before={targetGuid} after={savedGuid}");
                }
            }
            finally
            {
                if (existingRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(existingRoot);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void RebindTutorialScenePlayerReferences()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Additive);
            try
            {
                var playerRoot = FindSceneRoot(
                    scene,
                    "PHS_NetworkTutorialPlayer");
                var director = FindExactlyOneInScene<NetworkTutorialDirector>(
                    scene,
                    "tutorial_director");
                var controller = RequireSingle<NetworkPlayerController>(playerRoot);
                var grapple = RequireSingle<NetworkPlayerGrappleController>(playerRoot);
                var holder = RequireSingle<TempPlayerItemHolder>(playerRoot);

                RemoveTutorialVoiceOwnershipOverrides(scene);

                var serializedDirector = new SerializedObject(director);
                serializedDirector.FindProperty("playerController").objectReferenceValue =
                    controller;
                serializedDirector.FindProperty("grappleController").objectReferenceValue =
                    grapple;
                serializedDirector.FindProperty("itemHolder").objectReferenceValue = holder;
                serializedDirector.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=tutorial_scene_save_failed");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RemoveTutorialVoiceOwnershipOverrides(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var binder in root.GetComponentsInChildren<
                             ParkHanSolSpeakingPlayerHudBinder>(true))
                {
                    UnityEngine.Object.DestroyImmediate(binder);
                }

                foreach (var voiceSession in root.GetComponentsInChildren<
                             ProximityVoiceChatSession>(true))
                {
                    UnityEngine.Object.DestroyImmediate(voiceSession);
                }
            }
        }

        private static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            throw new InvalidOperationException(
                $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=scene_root_missing root={rootName}");
        }

        private static T FindExactlyOneInScene<T>(Scene scene, string role)
            where T : Component
        {
            T found = null;
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    found = component;
                    count++;
                }
            }

            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason={role}_count_invalid actual={count}");
            }

            return found;
        }

        private static void PreserveTutorialCompletionAudio(
            GameObject existingRoot,
            GameObject variantRoot)
        {
            var existingAudio = FindChild(
                existingRoot.transform,
                "PHS_NetworkTutorialCompletionAudio");
            if (existingAudio == null)
            {
                return;
            }

            var preservedAudio = UnityEngine.Object.Instantiate(
                existingAudio.gameObject,
                variantRoot.transform,
                false);
            preservedAudio.name = "PHS_NetworkTutorialCompletionAudio";
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
                CreateEnvironment(tutorialScene);
                var player = InstantiatePrefab(
                    TutorialPlayerPrefabPath,
                    tutorialScene,
                    new Vector3(0f, 1.1f, 0f));
                player.name = "PHS_NetworkTutorialPlayer";
                EnableLocalPlayerView(player);

                var controller = RequireSingle<NetworkPlayerController>(player);
                var grapple = RequireSingle<NetworkPlayerGrappleController>(player);
                var holder = RequireSingle<TempPlayerItemHolder>(player);

                CreateZeroGravityArea(tutorialScene);
                CreateGrappleTarget(tutorialScene);
                InstantiatePrefab(WrenchPrefabPath, tutorialScene,
                    new Vector3(-1.5f, 2.25f, 30.6f));
                InstantiatePrefab(BatteryPrefabPath, tutorialScene,
                    new Vector3(1.5f, 2.25f, 30.6f));

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
                directorSerialized.ApplyModifiedPropertiesWithoutUndo();

                var station = InstantiatePrefab(
                    StationPrefabPath,
                    tutorialScene,
                    new Vector3(0f, 1f, 49.5f));
                var stationComponent = RequireSingle<
                    NetworkTutorialInteractionStation>(station);
                var stationSerialized = new SerializedObject(stationComponent);
                stationSerialized.FindProperty("tutorialDirector").objectReferenceValue = director;
                stationSerialized.ApplyModifiedPropertiesWithoutUndo();

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
            var lightObject = new GameObject("PHS_NetworkTutorialKeyLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void CreateEnvironment(Scene scene)
        {
            var environment = new GameObject("PHS_NetworkTutorialEnvironment");
            SceneManager.MoveGameObjectToScene(environment, scene);

            for (var zIndex = 0; zIndex <= 15; zIndex++)
            {
                var z = zIndex * 3.6f;
                for (var xIndex = -1; xIndex <= 1; xIndex++)
                {
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        environment.transform,
                        $"PHS_NetworkTutorialFloor_{zIndex}_{xIndex + 1}",
                        new Vector3(xIndex * 3.6f, -0.513f, z + 1.8f),
                        new Vector3(-90f, 0f, 0f));
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        environment.transform,
                        $"PHS_NetworkTutorialCeiling_{zIndex}_{xIndex + 1}",
                        new Vector3(xIndex * 3.6f, 7.2f, z - 1.8f),
                        new Vector3(90f, 0f, 0f));
                }

                for (var yIndex = 0; yIndex <= 1; yIndex++)
                {
                    var y = yIndex * 3.6f;
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        environment.transform,
                        $"PHS_NetworkTutorialWall_L_{zIndex}_{yIndex}",
                        new Vector3(-5.67f, y, z),
                        new Vector3(0f, 90f, 0f));
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        environment.transform,
                        $"PHS_NetworkTutorialWall_R_{zIndex}_{yIndex}",
                        new Vector3(5.67f, y, z),
                        new Vector3(0f, 270f, 0f));
                }
            }

            CreateEnvironmentInstance(
                TutorialDoorPrefabPath,
                scene,
                environment.transform,
                "PHS_NetworkTutorialExitDoor",
                new Vector3(0f, 0f, 54.27f),
                new Vector3(0f, 180f, 0f));
            foreach (var x in new[] { -4.5f, 4.5f })
            {
                CreateEnvironmentInstance(
                    TutorialWallPrefabPath,
                    scene,
                    environment.transform,
                    x < 0f
                        ? "PHS_NetworkTutorialEndWall_L"
                        : "PHS_NetworkTutorialEndWall_R",
                    new Vector3(x, 0f, 54.27f),
                    new Vector3(0f, 180f, 0f));
            }

            for (var xIndex = -1; xIndex <= 1; xIndex++)
            {
                CreateEnvironmentInstance(
                    TutorialWallPrefabPath,
                    scene,
                    environment.transform,
                    $"PHS_NetworkTutorialEndWall_Upper_{xIndex + 1}",
                    new Vector3(xIndex * 3.6f, 3.6f, 54.27f),
                    new Vector3(0f, 180f, 0f));
            }

            for (var yIndex = 0; yIndex <= 1; yIndex++)
            {
                for (var xIndex = -1; xIndex <= 1; xIndex++)
                {
                    CreateEnvironmentInstance(
                        TutorialWallPrefabPath,
                        scene,
                        environment.transform,
                        $"PHS_NetworkTutorialStartWall_{yIndex}_{xIndex + 1}",
                        new Vector3(
                            xIndex * 3.6f,
                            yIndex * 3.6f,
                            -2.313f),
                        Vector3.zero);
                }
            }

            CreateEnvironmentInstance(
                TutorialDisplayDeskPrefabPath,
                scene,
                environment.transform,
                "PHS_NetworkTutorialItemDesk_L",
                new Vector3(-1.5f, 0f, 39.6f),
                Vector3.zero);
            CreateEnvironmentInstance(
                TutorialDisplayDeskPrefabPath,
                scene,
                environment.transform,
                "PHS_NetworkTutorialItemDesk_R",
                new Vector3(1.5f, 0f, 39.6f),
                Vector3.zero);
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

        private static void CreateZeroGravityArea(Scene scene)
        {
            var area = new GameObject("PHS_NetworkTutorialZeroGravity");
            SceneManager.MoveGameObjectToScene(area, scene);
            area.transform.position = new Vector3(0f, 3f, 13.5f);
            var collider = area.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 6f, 6.75f);
            var gravityArea = area.AddComponent<NetworkPlayerGravityArea>();
            var serialized = new SerializedObject(gravityArea);
            serialized.FindProperty("gravityMode").enumValueIndex =
                (int)NetworkPlayerGravityMode.Spacewalk;
            serialized.FindProperty("priority").intValue = 10;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateGrappleTarget(Scene scene)
        {
            var target = InstantiatePrefab(
                TutorialWallPrefabPath,
                scene,
                new Vector3(0f, 3f, 22.5f));
            target.name = "PHS_NetworkTutorialGrappleTarget";
            target.transform.localScale = Vector3.one * 0.5f;
            var markerLight = target.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.color = new Color(1f, 0.35f, 0.05f);
            markerLight.intensity = 5f;
            markerLight.range = 7f;
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
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            PHSUIFontPaths.ApplyResolved(text);
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
