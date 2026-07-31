using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkLobbyCustomizationAuthoring
    {
        private const int RequiredItemCount = 6;
        private const int RequiredColorCount = 6;
        private static readonly Color PanelDeepColor =
            new Color(0.055f, 0.075f, 0.09f, 0.96f);
        private static readonly Color RowElevatedColor =
            new Color(0.095f, 0.125f, 0.145f, 0.98f);
        private static readonly Color SettingsOrangeColor =
            new Color(1f, 0.57f, 0.2f, 0.98f);
        private static readonly Color WarmCreamColor =
            new Color(1f, 0.94f, 0.82f, 1f);
        private static readonly Color LobbyCoralColor =
            new Color(1f, 0.404f, 0.282f, 1f);
        private const string RootFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string CatalogPath = RootFolder +
            "/04. Data/Customization/PHS_CosmeticCatalog.asset";
        private const string ModelPath =
            "Assets/TripoModels/cute_white_ghost_3d_model_Clone1_3/cute_white_ghost_3d_model_Clone1_3.fbx";
        private const string LobbyScenePath = RootFolder +
            "/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string MainMenuPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string PreviewFolder = RootFolder +
            "/03. Prefab/Customization";
        private const string PreviewPrefabPath = PreviewFolder +
            "/PHS_NetworkLobbyCustomizationPreviewRig.prefab";
        private const string UiFolder = RootFolder +
            "/03. Prefab/UI/Customization";
        private const string UiPrefabPath = UiFolder +
            "/PHS_NetworkLobbyCustomizationFrontend.prefab";
        private const string RenderTexturePath = RootFolder +
            "/04. Data/Customization/PHS_NetworkLobbyCustomizationPreview.renderTexture";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Lobby Customization")]
        public static void Author()
        {
            RequireLobbySceneNotLoaded();
            var catalog = RequireAsset<CosmeticCatalog>(CatalogPath);
            ValidateCatalog(catalog);
            RequireAsset<GameObject>(ModelPath);
            EnsureFolder(PreviewFolder);
            EnsureFolder(UiFolder);

            var renderTexture = CreateOrUpdateRenderTexture();
            CreatePreviewPrefab(renderTexture);
            CreateUiPrefab(catalog);
            NestFrontendInMainMenuPrefab();
            PlaceInLobbyScene(renderTexture);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"PHS_NETWORK_LOBBY_CUSTOMIZATION_AUTHORING_OK scene={LobbyScenePath}");
        }

        private static RenderTexture CreateOrUpdateRenderTexture()
        {
            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                RenderTexturePath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(1024, 1024, 24,
                    RenderTextureFormat.ARGB32)
                {
                    name = "PHS_NetworkLobbyCustomizationPreview",
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
            }
            else
            {
                renderTexture.Release();
                renderTexture.width = 1024;
                renderTexture.height = 1024;
                renderTexture.depth = 24;
                renderTexture.antiAliasing = 1;
                renderTexture.useMipMap = false;
                renderTexture.autoGenerateMips = false;
                EditorUtility.SetDirty(renderTexture);
            }

            return renderTexture;
        }

        private static void CreatePreviewPrefab(RenderTexture renderTexture)
        {
            var root = new GameObject(
                "PHS_NetworkLobbyCustomizationPreviewRig");
            try
            {
                var rotationRoot = new GameObject("RotationRoot").transform;
                rotationRoot.SetParent(root.transform, false);

                var modelAsset = RequireAsset<GameObject>(ModelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "VisualOnlyGhost";
                model.transform.SetParent(rotationRoot, false);

                RejectNetworking(root);
                var bodyRenderer = model.GetComponentInChildren<
                    SkinnedMeshRenderer>(true);
                if (bodyRenderer == null)
                {
                    throw AuthoringFailure("preview_body_renderer_missing");
                }

                var headBone = RequireChild(model.transform, "Head");
                var backBone = RequireChild(model.transform, "Spine02");
                CreateSlot(headBone, "HeadSlot", new Vector3(0f, 0.12f, 0f));
                CreateSlot(backBone, "BackSlot", new Vector3(0f, 0f, -0.12f));

                var cameraObject = new GameObject(
                    "PreviewCamera",
                    typeof(Camera));
                cameraObject.transform.SetParent(root.transform, false);
                var previewCamera = cameraObject.GetComponent<Camera>();
                previewCamera.targetTexture = renderTexture;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 0f);
                previewCamera.fieldOfView = 34f;
                previewCamera.nearClipPlane = 0.05f;
                PositionCamera(previewCamera, bodyRenderer.bounds);

                CreateLight(root.transform, "KeyLight",
                    new Vector3(2f, 3f, -3f), 1.2f);
                CreateLight(root.transform, "FillLight",
                    new Vector3(-2f, 1.5f, -2f), 0.65f);

                PrefabUtility.SaveAsPrefabAsset(root, PreviewPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateUiPrefab(CosmeticCatalog catalog)
        {
            var root = CreateRect(
                "PHS_NetworkLobbyCustomizationFrontend",
                null,
                Vector2.zero,
                new Vector2(1920f, 1080f));
            try
            {
                var controller = root.gameObject.AddComponent<
                    NetworkLobbyCustomizationFrontendController>();
                var openButton = CreateLobbyMenuButton(
                    root,
                    "OpenCustomizationButton",
                    "CUSTOMIZE",
                    0.29f);
                var panel = CreatePanel(root, "CustomizationPanel",
                    Vector2.zero, new Vector2(1480f, 900f),
                    PanelDeepColor);
                var closeButton = CreateButton(panel, "CloseButton", "X",
                    new Vector2(675f, 405f), new Vector2(60f, 60f));
                var creditsLabel = CreateText(panel, "CreditsLabel",
                    "CUSTOM CREDITS  ---", new Vector2(-505f, 405f),
                    new Vector2(380f, 54f), 28f, TextAlignmentOptions.Left);
                var statusLabel = CreateText(panel, "StatusLabel", string.Empty,
                    new Vector2(0f, -405f), new Vector2(840f, 50f), 24f,
                    TextAlignmentOptions.Center);
                creditsLabel.color = LobbyCoralColor;
                statusLabel.color = LobbyCoralColor;

                var previewImageObject = CreateRect("PreviewImage", panel,
                    new Vector2(350f, 45f), new Vector2(650f, 650f));
                var rawImage = previewImageObject.gameObject.AddComponent<RawImage>();
                rawImage.color = Color.white;
                var presenter = previewImageObject.gameObject.AddComponent<
                    LobbyCustomizationPreviewPresenter>();
                SetObject(presenter, "previewImage", rawImage);

                var itemRows = new ItemUi[RequiredItemCount];
                for (var index = 0; index < RequiredItemCount; index++)
                {
                    itemRows[index] = CreateItemRow(panel, catalog.Items[index],
                        new Vector2(-440f, 290f - index * 96f));
                }

                var colorButtons = new ColorUi[RequiredColorCount];
                for (var index = 0; index < RequiredColorCount; index++)
                {
                    colorButtons[index] = CreateColorButton(panel,
                        catalog.AllowedBodyColors[index],
                        new Vector2(145f + index * 72f, -320f));
                }

                var applyColorButton = CreateButton(panel, "ApplyColorButton",
                    "APPLY COLOR", new Vector2(580f, -320f), new Vector2(190f, 56f));
                var unequipHeadButton = CreateButton(panel, "UnequipHeadButton",
                    "CLEAR HEAD", new Vector2(-515f, -320f), new Vector2(190f, 56f));
                var unequipBackButton = CreateButton(panel, "UnequipBackButton",
                    "CLEAR BACK", new Vector2(-305f, -320f), new Vector2(190f, 56f));
                var resetPreviewButton = CreateButton(panel, "ResetPreviewButton",
                    "RESET", new Vector2(-95f, -320f), new Vector2(150f, 56f));
                var trainingButton = CreateLobbyMenuButton(
                    root,
                    "TrainingButton",
                    "TUTORIAL",
                    0.21f);

                var trainingController = root.gameObject.AddComponent<
                    LobbyTrainingSceneButtonController>();
                SetObject(trainingController, "trainingButton", trainingButton);
                SetObject(trainingController, "statusLabel", statusLabel);
                WireController(controller, catalog, panel.gameObject, openButton,
                    closeButton, creditsLabel, statusLabel, presenter, itemRows,
                    colorButtons, applyColorButton, unequipHeadButton,
                    unequipBackButton, resetPreviewButton);

                PHSUIFontAssetAuthoring.ApplyTypography(root.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, UiPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        private static void PlaceInLobbyScene(RenderTexture renderTexture)
        {
            RequireLobbySceneNotLoaded();
            var previousScene = SceneManager.GetActiveScene();
            var lobbyScene = EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Additive);
            try
            {
                var canvas = RequireCanvas(lobbyScene);
                RequireMainMenu(
                    lobbyScene,
                    out var startPanelRect,
                    out var startButtonRect,
                    out var quitButtonRect,
                    out var mainMenuButtons,
                    out var selectionIndicator);
                var ui = ResolveNestedFrontendInLobbyScene(
                    lobbyScene,
                    canvas,
                    startPanelRect);
                RemoveOwnedInstances(
                    lobbyScene,
                    "PHS_NetworkLobbyCustomizationPreviewRig",
                    PreviewPrefabPath,
                    HasPreviewRigComponents);

                AlignFrontendMenuToReference(ui, startButtonRect);
                BindMenuSelectionIndicator(ui, selectionIndicator);
                WireModalInput(ui, lobbyScene, mainMenuButtons);

                var rigAsset = RequireAsset<GameObject>(PreviewPrefabPath);
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(
                    rigAsset,
                    lobbyScene);
                rig.transform.position = new Vector3(1000f, -1000f, 1000f);

                var presenter = ui.GetComponentInChildren<
                    LobbyCustomizationPreviewPresenter>(true);
                var previewCamera = rig.GetComponentInChildren<Camera>(true);
                var bodyRenderer = rig.GetComponentInChildren<
                    SkinnedMeshRenderer>(true);
                var rotationRoot = RequireChild(rig.transform, "RotationRoot");
                var headSlot = RequireChild(rig.transform, "HeadSlot");
                var backSlot = RequireChild(rig.transform, "BackSlot");
                var rawImage = presenter.GetComponent<RawImage>();
                previewCamera.targetTexture = renderTexture;
                var serializedRawImage = new SerializedObject(rawImage);
                var textureProperty = serializedRawImage.FindProperty(
                    "m_Texture");
                if (textureProperty == null)
                {
                    throw AuthoringFailure(
                        "preview_raw_image_texture_property_missing");
                }

                textureProperty.objectReferenceValue = renderTexture;
                serializedRawImage.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    rawImage);

                var serialized = new SerializedObject(presenter);
                serialized.FindProperty("previewRigRoot").objectReferenceValue = rig.transform;
                serialized.FindProperty("rotationRoot").objectReferenceValue = rotationRoot;
                serialized.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
                serialized.FindProperty("headSlot").objectReferenceValue = headSlot;
                serialized.FindProperty("backSlot").objectReferenceValue = backSlot;
                serialized.FindProperty("previewCamera").objectReferenceValue = previewCamera;
                serialized.FindProperty("previewImage").objectReferenceValue = rawImage;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MoveQuitToFiveItemMenuSlot(quitButtonRect);

                EditorSceneManager.MarkSceneDirty(lobbyScene);
                EditorSceneManager.SaveScene(lobbyScene, LobbyScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(lobbyScene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }
        }

        private static void NestFrontendInMainMenuPrefab()
        {
            var mainMenu = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
            try
            {
                var startPanel = RequireChild(
                        mainMenu.transform,
                        "Start Panel")
                    as RectTransform;
                if (startPanel == null)
                {
                    throw AuthoringFailure("start_panel_rect_missing");
                }

                var startButton = RequireDirectChild(
                        startPanel,
                        "Start Button")
                    as RectTransform;
                var settingsButton = RequireDirectChild(
                        startPanel,
                        "Settings Button")
                    as RectTransform;
                var quitButton = RequireDirectChild(
                        startPanel,
                        "Quit Button")
                    as RectTransform;
                var mainMenuButtons = new[]
                {
                    RequireButton(startButton, "start"),
                    RequireButton(settingsButton, "settings"),
                    RequireButton(quitButton, "quit")
                };
                var indicators = mainMenu.GetComponentsInChildren<
                    ParkHanSolLobbySelectionIndicator>(true);
                if (indicators.Length != 1)
                {
                    throw AuthoringFailure(
                        $"main_menu_selection_indicator_count_invalid:actual={indicators.Length}");
                }

                RemoveOwnedNestedFrontends(mainMenu, startPanel);
                var uiAsset = RequireAsset<GameObject>(UiPrefabPath);
                var frontend = (GameObject)PrefabUtility.InstantiatePrefab(
                    uiAsset,
                    mainMenu.scene);
                frontend.transform.SetParent(startPanel, false);
                AlignFrontendMenuToReference(frontend, startButton);
                BindMenuSelectionIndicator(frontend, indicators[0]);
                WireBlockedMenuButtons(frontend, mainMenuButtons);

                var saved = PrefabUtility.SaveAsPrefabAsset(
                    mainMenu,
                    MainMenuPrefabPath);
                if (saved == null)
                {
                    throw AuthoringFailure("main_menu_prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(mainMenu);
            }
        }

        private static void RemoveOwnedNestedFrontends(
            GameObject mainMenu,
            RectTransform startPanel)
        {
            var matches = mainMenu.GetComponentsInChildren<Transform>(true)
                .Where(transform =>
                    transform.name
                        == "PHS_NetworkLobbyCustomizationFrontend")
                .Select(transform => transform.gameObject)
                .ToArray();
            foreach (var match in matches)
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(
                    match);
                var sourcePath = PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(match);
                if (instanceRoot != match
                    || sourcePath != UiPrefabPath
                    || match.transform.parent != startPanel)
                {
                    throw AuthoringFailure(
                        $"main_menu_frontend_name_owned_by_other:source={sourcePath}");
                }

                UnityEngine.Object.DestroyImmediate(match);
            }
        }

        private static GameObject ResolveNestedFrontendInLobbyScene(
            Scene scene,
            Canvas canvas,
            RectTransform startPanel)
        {
            var nestedFrontends = new System.Collections.Generic.List<
                GameObject>();
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform =>
                    transform.name
                        == "PHS_NetworkLobbyCustomizationFrontend")
                .Select(transform => transform.gameObject)
                .ToArray();
            foreach (var match in matches)
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(
                    match);
                var sourcePath = PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(match);
                if (instanceRoot != match || sourcePath != UiPrefabPath)
                {
                    throw AuthoringFailure(
                        $"scene_frontend_name_owned_by_other:source={sourcePath}");
                }

                if (match.transform.parent == startPanel)
                {
                    if (PrefabUtility.IsAddedGameObjectOverride(match))
                    {
                        UnityEngine.Object.DestroyImmediate(match);
                        continue;
                    }

                    nestedFrontends.Add(match);
                    continue;
                }

                if (match.transform.parent == canvas.transform)
                {
                    UnityEngine.Object.DestroyImmediate(match);
                    continue;
                }

                throw AuthoringFailure(
                    $"scene_frontend_parent_invalid:parent={match.transform.parent?.name}");
            }

            if (nestedFrontends.Count != 1)
            {
                throw AuthoringFailure(
                    $"nested_frontend_count_invalid:actual={nestedFrontends.Count}");
            }

            return nestedFrontends[0];
        }

        private static void WireController(
            NetworkLobbyCustomizationFrontendController controller,
            CosmeticCatalog catalog,
            GameObject panel,
            Button openButton,
            Button closeButton,
            TMP_Text creditsLabel,
            TMP_Text statusLabel,
            LobbyCustomizationPreviewPresenter presenter,
            ItemUi[] itemRows,
            ColorUi[] colors,
            Button applyColorButton,
            Button unequipHeadButton,
            Button unequipBackButton,
            Button resetPreviewButton)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;
            serialized.FindProperty("openButton").objectReferenceValue = openButton;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.FindProperty("creditsLabel").objectReferenceValue = creditsLabel;
            serialized.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serialized.FindProperty("previewPresenter").objectReferenceValue = presenter;
            serialized.FindProperty("applyColorButton").objectReferenceValue = applyColorButton;
            serialized.FindProperty("unequipHeadButton").objectReferenceValue = unequipHeadButton;
            serialized.FindProperty("unequipBackButton").objectReferenceValue = unequipBackButton;
            serialized.FindProperty("resetPreviewButton").objectReferenceValue = resetPreviewButton;

            var rowsProperty = serialized.FindProperty("itemRows");
            rowsProperty.arraySize = itemRows.Length;
            for (var index = 0; index < itemRows.Length; index++)
            {
                var row = rowsProperty.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("item").objectReferenceValue = itemRows[index].Item;
                row.FindPropertyRelative("previewButton").objectReferenceValue = itemRows[index].PreviewButton;
                row.FindPropertyRelative("itemLabel").objectReferenceValue = itemRows[index].ItemLabel;
                row.FindPropertyRelative("priceLabel").objectReferenceValue = itemRows[index].PriceLabel;
                row.FindPropertyRelative("actionButton").objectReferenceValue = itemRows[index].ActionButton;
                row.FindPropertyRelative("actionLabel").objectReferenceValue = itemRows[index].ActionLabel;
            }

            var colorsProperty = serialized.FindProperty("colorButtons");
            colorsProperty.arraySize = colors.Length;
            for (var index = 0; index < colors.Length; index++)
            {
                var color = colorsProperty.GetArrayElementAtIndex(index);
                color.FindPropertyRelative("color").colorValue = colors[index].Color;
                color.FindPropertyRelative("button").objectReferenceValue = colors[index].Button;
                color.FindPropertyRelative("swatch").objectReferenceValue = colors[index].Swatch;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ItemUi CreateItemRow(
            RectTransform parent,
            CosmeticItemData item,
            Vector2 position)
        {
            var row = CreatePanel(parent, $"ItemRow_{item.ItemId}", position,
                new Vector2(600f, 78f), RowElevatedColor);
            var previewButton = CreateButton(row, "PreviewButton", "VIEW",
                new Vector2(-235f, 0f), new Vector2(100f, 50f));
            var itemLabel = CreateText(row, "ItemLabel", item.DisplayName,
                new Vector2(-75f, 10f), new Vector2(210f, 32f), 22f,
                TextAlignmentOptions.Left);
            var priceLabel = CreateText(row, "PriceLabel", item.Price.ToString(),
                new Vector2(-75f, -20f), new Vector2(210f, 26f), 17f,
                TextAlignmentOptions.Left);
            var actionButton = CreateButton(row, "ActionButton", "BUY",
                new Vector2(220f, 0f), new Vector2(130f, 50f));
            return new ItemUi(item, previewButton, itemLabel, priceLabel,
                actionButton, actionButton.GetComponentInChildren<TMP_Text>());
        }

        private static ColorUi CreateColorButton(
            RectTransform parent,
            Color32 color,
            Vector2 position)
        {
            var button = CreateButton(parent, "ColorButton", string.Empty,
                position, new Vector2(54f, 54f));
            var swatch = button.GetComponent<Image>();
            swatch.color = color;
            return new ColorUi(color, button, swatch);
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size)
        {
            var rect = CreatePanel(parent, name, position, size,
                SettingsOrangeColor);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.62f, 1f);
            colors.pressedColor = new Color(1f, 0.66f, 0.15f, 1f);
            colors.selectedColor = new Color(1f, 0.84f, 0.32f, 1f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.07f;
            button.colors = colors;
            if (!string.IsNullOrEmpty(label))
            {
                CreateText(rect, "Label", label, Vector2.zero, size, 20f,
                    TextAlignmentOptions.Center);
            }

            return button;
        }

        private static Button CreateLobbyMenuButton(
            RectTransform parent,
            string name,
            string label,
            float anchorY)
        {
            var rect = CreateRect(name, parent, Vector2.zero,
                new Vector2(420f, 56f));
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var buttonColors = button.colors;
            buttonColors.normalColor = Color.clear;
            buttonColors.highlightedColor = new Color(0.15f, 0.82f, 1f, 1f);
            buttonColors.pressedColor = new Color(1f, 0.404f, 0.282f, 1f);
            buttonColors.selectedColor = buttonColors.highlightedColor;
            buttonColors.disabledColor = new Color(0.25f, 0.3f, 0.36f, 0.45f);
            buttonColors.fadeDuration = 0.07f;
            button.colors = buttonColors;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.clear;
            outline.effectDistance = Vector2.zero;

            var labelText = CreateText(
                rect,
                "Label",
                label,
                Vector2.zero,
                rect.sizeDelta,
                30f,
                TextAlignmentOptions.Center);
            labelText.color = LobbyCoralColor;
            PHSUIFontPaths.Apply(labelText, PHSUIFontRole.Control);
            labelText.enableAutoSizing = false;
            labelText.characterSpacing = 0f;
            labelText.raycastTarget = false;
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            var selectionTarget = rect.gameObject.AddComponent<
                ParkHanSolLobbySelectionTarget>();
            var serializedTarget = new SerializedObject(selectionTarget);
            serializedTarget.FindProperty("selectable").objectReferenceValue = button;
            serializedTarget.FindProperty("visualTarget").objectReferenceValue = rect;
            serializedTarget.FindProperty("focusGraphic").objectReferenceValue = labelText;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static RectTransform CreatePanel(
            RectTransform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(name, parent, position, size);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(
            RectTransform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = WarmCreamColor;
            PHSUIFontPaths.ApplyResolved(text);
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void PositionCamera(
            Camera previewCamera,
            Bounds bounds)
        {
            var center = bounds.center;
            var extent = Mathf.Max(0.5f, bounds.extents.magnitude);
            previewCamera.transform.position = center + new Vector3(
                0f,
                extent * 0.15f,
                -extent * 3.2f);
            previewCamera.transform.LookAt(center);
        }

        private static void CreateLight(
            Transform parent,
            string name,
            Vector3 position,
            float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            lightObject.transform.LookAt(parent.position);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        private static void CreateSlot(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            var slot = new GameObject(name).transform;
            slot.SetParent(parent, false);
            slot.localPosition = localPosition;
        }

        private static Canvas RequireCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    return canvas;
                }
            }

            throw AuthoringFailure("lobby_canvas_missing");
        }

        private static void RequireMainMenu(
            Scene scene,
            out RectTransform startPanelRect,
            out RectTransform startButtonRect,
            out RectTransform quitButtonRect,
            out Button[] mainMenuButtons,
            out ParkHanSolLobbySelectionIndicator selectionIndicator)
        {
            var namedRoots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == "PHS_NetworkStartLobbyUI")
                .Select(transform => transform.gameObject)
                .ToArray();
            if (namedRoots.Length != 1)
            {
                throw AuthoringFailure(
                    $"main_menu_count_invalid:actual={namedRoots.Length}");
            }

            var mainMenu = namedRoots[0];
            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                mainMenu);
            if (PrefabUtility.GetNearestPrefabInstanceRoot(mainMenu) != mainMenu
                || sourcePath != MainMenuPrefabPath)
            {
                throw AuthoringFailure(
                    $"main_menu_source_invalid:actual={sourcePath}");
            }

            startPanelRect = RequireChild(mainMenu.transform, "Start Panel")
                as RectTransform;
            if (startPanelRect == null)
            {
                throw AuthoringFailure("start_panel_rect_missing");
            }

            startButtonRect = RequireDirectChild(startPanelRect, "Start Button")
                as RectTransform;
            var settingsButton = RequireDirectChild(startPanelRect, "Settings Button")
                as RectTransform;
            quitButtonRect = RequireDirectChild(startPanelRect, "Quit Button")
                as RectTransform;
            ValidateMenuButtonRect(startButtonRect, "start", 0.45f);
            ValidateMenuButtonRect(settingsButton, "settings", 0.37f);
            ValidateMenuButtonRect(quitButtonRect, "quit", null);
            mainMenuButtons = new[]
            {
                RequireButton(startButtonRect, "start"),
                RequireButton(settingsButton, "settings"),
                RequireButton(quitButtonRect, "quit")
            };

            var indicators = mainMenu.GetComponentsInChildren<
                ParkHanSolLobbySelectionIndicator>(true);
            if (indicators.Length != 1)
            {
                throw AuthoringFailure(
                    $"main_menu_selection_indicator_count_invalid:actual={indicators.Length}");
            }

            selectionIndicator = indicators[0];
        }

        private static void WireModalInput(
            GameObject frontend,
            Scene scene,
            Button[] mainMenuButtons)
        {
            var eventSystems = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .ToArray();
            if (eventSystems.Length != 1)
            {
                throw AuthoringFailure(
                    $"lobby_event_system_count_invalid:actual={eventSystems.Length}");
            }

            var controller = frontend.GetComponent<
                NetworkLobbyCustomizationFrontendController>();
            var tutorialButton = RequireChild(
                    frontend.transform,
                    "TrainingButton")
                .GetComponent<Button>();
            if (controller == null || tutorialButton == null)
            {
                throw AuthoringFailure("frontend_modal_input_component_missing");
            }

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("lobbyEventSystem").objectReferenceValue =
                eventSystems[0];
            WireBlockedMenuButtons(
                serialized,
                tutorialButton,
                mainMenuButtons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBlockedMenuButtons(
            GameObject frontend,
            Button[] mainMenuButtons)
        {
            var controller = frontend.GetComponent<
                NetworkLobbyCustomizationFrontendController>();
            var tutorialButton = RequireChild(
                    frontend.transform,
                    "TrainingButton")
                .GetComponent<Button>();
            if (controller == null || tutorialButton == null)
            {
                throw AuthoringFailure("frontend_modal_input_component_missing");
            }

            var serialized = new SerializedObject(controller);
            WireBlockedMenuButtons(
                serialized,
                tutorialButton,
                mainMenuButtons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBlockedMenuButtons(
            SerializedObject serialized,
            Button tutorialButton,
            Button[] mainMenuButtons)
        {
            var blockedButtons = serialized.FindProperty(
                "blockedLobbyMenuButtons");
            blockedButtons.arraySize = 4;
            blockedButtons.GetArrayElementAtIndex(0).objectReferenceValue =
                mainMenuButtons[0];
            blockedButtons.GetArrayElementAtIndex(1).objectReferenceValue =
                mainMenuButtons[1];
            blockedButtons.GetArrayElementAtIndex(2).objectReferenceValue =
                tutorialButton;
            blockedButtons.GetArrayElementAtIndex(3).objectReferenceValue =
                mainMenuButtons[2];
        }

        private static Button RequireButton(
            RectTransform rect,
            string buttonName)
        {
            var button = rect == null ? null : rect.GetComponent<Button>();
            if (button == null)
            {
                throw AuthoringFailure(
                    $"main_menu_button_component_missing:name={buttonName}");
            }

            return button;
        }

        private static void AlignFrontendMenuToReference(
            GameObject frontend,
            RectTransform referenceButton)
        {
            var openButton = RequireChild(
                frontend.transform,
                "OpenCustomizationButton") as RectTransform;
            var tutorialButton = RequireChild(
                frontend.transform,
                "TrainingButton") as RectTransform;
            if (openButton == null || tutorialButton == null)
            {
                throw AuthoringFailure("frontend_menu_rect_missing");
            }

            Canvas.ForceUpdateCanvases();
            var referenceWorldCenter = referenceButton.TransformPoint(
                referenceButton.rect.center);
            AlignRectCenterX(openButton, referenceWorldCenter);
            AlignRectCenterX(tutorialButton, referenceWorldCenter);
        }

        private static void AlignRectCenterX(
            RectTransform target,
            Vector3 referenceWorldCenter)
        {
            var parent = target.parent as RectTransform;
            if (parent == null)
            {
                throw AuthoringFailure(
                    $"frontend_menu_parent_rect_missing:name={target.name}");
            }

            var targetWorldCenter = target.TransformPoint(target.rect.center);
            var localDelta = parent.InverseTransformVector(
                referenceWorldCenter - targetWorldCenter);
            target.anchoredPosition += new Vector2(localDelta.x, 0f);
            var alignedWorldCenter = target.TransformPoint(target.rect.center);
            if (Mathf.Abs(alignedWorldCenter.x - referenceWorldCenter.x) > 0.01f)
            {
                throw AuthoringFailure(
                    $"frontend_menu_x_alignment_failed:name={target.name}");
            }
        }

        private static void BindMenuSelectionIndicator(
            GameObject frontend,
            ParkHanSolLobbySelectionIndicator selectionIndicator)
        {
            var selectionTargets = frontend.GetComponentsInChildren<
                ParkHanSolLobbySelectionTarget>(true);
            if (selectionTargets.Length != 2)
            {
                throw AuthoringFailure(
                    $"frontend_menu_selection_target_count_invalid:actual={selectionTargets.Length}");
            }

            foreach (var selectionTarget in selectionTargets)
            {
                selectionTarget.SetIndicator(selectionIndicator);
                EditorUtility.SetDirty(selectionTarget);
            }
        }

        private static void MoveQuitToFiveItemMenuSlot(
            RectTransform quitButtonRect)
        {
            quitButtonRect.anchorMin = new Vector2(0.5f, 0.13f);
            quitButtonRect.anchorMax = new Vector2(0.5f, 0.13f);
            quitButtonRect.anchoredPosition = Vector2.zero;
        }

        private static void ValidateMenuButtonRect(
            RectTransform rect,
            string buttonName,
            float? expectedAnchorY)
        {
            if (rect == null
                || !Mathf.Approximately(rect.sizeDelta.x, 420f)
                || !Mathf.Approximately(rect.sizeDelta.y, 56f)
                || !Mathf.Approximately(rect.anchorMin.x, 0.5f)
                || !Mathf.Approximately(rect.anchorMax.x, 0.5f)
                || (expectedAnchorY.HasValue
                    && (!Mathf.Approximately(
                            rect.anchorMin.y,
                            expectedAnchorY.Value)
                        || !Mathf.Approximately(
                            rect.anchorMax.y,
                            expectedAnchorY.Value))))
            {
                throw AuthoringFailure(
                    $"main_menu_button_geometry_invalid:name={buttonName}");
            }
        }

        private static Transform RequireDirectChild(
            Transform parent,
            string name)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            throw AuthoringFailure(
                $"direct_child_missing:parent={parent.name}:name={name}");
        }

        private static Transform RequireChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            throw AuthoringFailure($"child_missing:name={name}");
        }

        private static void RemoveOwnedInstances(
            Scene scene,
            string name,
            string expectedPrefabPath,
            Func<GameObject, bool> hasExpectedComponents)
        {
            var matches = new System.Collections.Generic.List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == name)
                    {
                        matches.Add(child.gameObject);
                    }
                }
            }

            foreach (var match in matches)
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(
                    match);
                var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    match);
                if (instanceRoot != match
                    || sourcePath != expectedPrefabPath
                    || !hasExpectedComponents(match))
                {
                    throw AuthoringFailure(
                        $"scene_object_name_owned_by_other:name={name}:source={sourcePath}");
                }

                UnityEngine.Object.DestroyImmediate(match);
            }
        }

        private static bool HasFrontendComponent(GameObject candidate)
        {
            return candidate.GetComponent<
                NetworkLobbyCustomizationFrontendController>() != null;
        }

        private static bool HasPreviewRigComponents(GameObject candidate)
        {
            return candidate.GetComponentInChildren<Camera>(true) != null
                && candidate.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                && candidate.GetComponentInChildren<NetworkObject>(true) == null
                && candidate.GetComponentInChildren<NetworkBehaviour>(true) == null;
        }

        private static void RejectNetworking(GameObject root)
        {
            if (root.GetComponentInChildren<NetworkObject>(true) != null
                || root.GetComponentInChildren<NetworkBehaviour>(true) != null)
            {
                throw AuthoringFailure("preview_rig_contains_network_component");
            }
        }

        private static void ValidateCatalog(CosmeticCatalog catalog)
        {
            if (catalog.Items.Count != RequiredItemCount
                || catalog.AllowedBodyColors.Count != RequiredColorCount)
            {
                throw AuthoringFailure(
                    $"catalog_count_invalid:items={catalog.Items.Count}:colors={catalog.AllowedBodyColors.Count}");
            }

            for (var index = 0; index < catalog.Items.Count; index++)
            {
                var item = catalog.Items[index];
                if (item == null || item.VisualPrefab == null)
                {
                    throw AuthoringFailure(
                        $"catalog_visual_missing:index={index}");
                }

                RejectNetworking(item.VisualPrefab);
            }
        }

        private static void RequireLobbySceneNotLoaded()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).path == LobbyScenePath)
                {
                    throw AuthoringFailure("lobby_scene_is_loaded");
                }
            }
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw AuthoringFailure($"asset_missing:path={path}");
            }

            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            var current = "Assets";
            var segments = folderPath.Split('/');
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

        private static void SetObject(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InvalidOperationException AuthoringFailure(string reason)
        {
            return new InvalidOperationException(
                $"PHS_NETWORK_LOBBY_CUSTOMIZATION_AUTHORING_FAILED reason={reason}");
        }

        private readonly struct ItemUi
        {
            public ItemUi(
                CosmeticItemData item,
                Button previewButton,
                TMP_Text itemLabel,
                TMP_Text priceLabel,
                Button actionButton,
                TMP_Text actionLabel)
            {
                Item = item;
                PreviewButton = previewButton;
                ItemLabel = itemLabel;
                PriceLabel = priceLabel;
                ActionButton = actionButton;
                ActionLabel = actionLabel;
            }

            public CosmeticItemData Item { get; }
            public Button PreviewButton { get; }
            public TMP_Text ItemLabel { get; }
            public TMP_Text PriceLabel { get; }
            public Button ActionButton { get; }
            public TMP_Text ActionLabel { get; }
        }

        private readonly struct ColorUi
        {
            public ColorUi(Color32 color, Button button, Image swatch)
            {
                Color = color;
                Button = button;
                Swatch = swatch;
            }

            public Color32 Color { get; }
            public Button Button { get; }
            public Image Swatch { get; }
        }
    }
}
