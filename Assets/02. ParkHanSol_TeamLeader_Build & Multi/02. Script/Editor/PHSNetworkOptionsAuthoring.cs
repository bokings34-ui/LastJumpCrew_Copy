#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PHSNetworkOptionsAuthoring
{
    private const string UiFolder =
        "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI";
    private const string OriginalLobbyUi = UiFolder + "/ParkHanSol_StartLobbyUI.prefab";
    private const string BeaverLobbySceneSourceUi =
        "Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/UI/ParkHanSol_StartLobbyUI.prefab";
    private const string OriginalPlayHud = UiFolder + "/ParkHanSol_PlayHudUI.prefab";
    private const string NetworkLobbyUi = UiFolder + "/PHS_NetworkStartLobbyUI.prefab";
    private const string NetworkPlayHud = UiFolder + "/PHS_NetworkPlayHudUI.prefab";
    private const string NetworkOwnerPauseUi = UiFolder + "/PHS_NetworkOwnerPauseUI.prefab";
    private const string PlayerPrefab =
        "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
    private const string BeaverSceneFolder =
        "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026";
    private const string BeaverLobbyScene = BeaverSceneFolder + "/ParkHanSol_LobbyScene.unity";

    private static readonly Color Charcoal = new(0.055f, 0.075f, 0.09f, 0.96f);
    private static readonly Color Surface = new(0.095f, 0.125f, 0.145f, 0.98f);
    private static readonly Color Accent = new(1f, 0.57f, 0.20f, 1f);
    private static readonly Color Cream = new(1f, 0.94f, 0.82f, 1f);

    [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Options UI")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=play_mode_active");
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.name.Contains("Map", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=map_scene_active scene={activeScene.path} " +
                "Close the Map without saving before authoring.");
            return;
        }

        CopyWithNewGuid(OriginalLobbyUi, NetworkLobbyUi);
        CopyWithNewGuid(OriginalPlayHud, NetworkPlayHud);
        ConfigureLobbyUi();
        ConfigureCanonicalPlayHud();
        ConfigurePlayHud();
        CreateOwnerPauseUi();
        AttachOwnerPauseUiToPlayer();
        ReplaceUiInScene(BeaverLobbyScene, BeaverLobbySceneSourceUi, NetworkLobbyUi);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "PHS_NETWORK_OPTIONS_AUTHOR_OK lobby=PHS_NetworkStartLobbyUI " +
            "playHud=PHS_NetworkPlayHudUI ownerPause=PHS_NetworkOwnerPauseUI " +
            "playerConnected=true mapModified=false");
    }

    [MenuItem("Tools/ParkHanSol/BEAVER/Sync Lobby Settings To Player HUD")]
    public static void SyncLobbySettingsToPlayerHud()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "PHS_LOBBY_SETTINGS_HUD_SYNC_FAILED reason=play_mode_active");
            return;
        }

        ConfigureCanonicalPlayHud();
        ConfigurePlayHud();
        CreateOwnerPauseUi();
        AttachOwnerPauseUiToPlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "PHS_LOBBY_SETTINGS_HUD_SYNC_OK " +
            "tabs=graphics,audio,voice,controls apply=true back_cancel=true");
    }

    private static void CopyWithNewGuid(string sourcePath, string targetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=source_prefab_missing path={sourcePath}");
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=prefab_copy_failed target={targetPath}");
        }
    }

    private static void ConfigureLobbyUi()
    {
        var root = PrefabUtility.LoadPrefabContents(NetworkLobbyUi);
        try
        {
            root.name = "PHS_NetworkStartLobbyUI";
            ConfigureCanvasScalers(root);
            StyleImages(root, false);
            StyleTexts(root, false);
            ConfigureDropdownTemplates(root);
            StretchNamedPanel(root, "Settings Panel_R");
            PrefabUtility.SaveAsPrefabAsset(root, NetworkLobbyUi);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayHud()
    {
        ConfigurePlayHudPrefab(NetworkPlayHud, "PHS_NetworkPlayHudUI");
    }

    private static void ConfigureCanonicalPlayHud()
    {
        ConfigurePlayHudPrefab(OriginalPlayHud, "ParkHanSol_PlayHudUI");
    }

    private static void ConfigurePlayHudPrefab(
        string hudPath,
        string rootName)
    {
        var lobbySource = PrefabUtility.LoadPrefabContents(NetworkLobbyUi);
        var root = PrefabUtility.LoadPrefabContents(hudPath);
        try
        {
            root.name = rootName;
            ConfigureCanvasScalers(root);
            var pausePanel = FindTransform(root.transform, "Pause Menu")?.gameObject;
            var pauseController = root.GetComponentInChildren<ParkHanSolPauseMenuController>(true);
            if (pausePanel == null || pauseController == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=play_hud_reference_missing");
            }

            var pauseParent = pausePanel.transform.parent;
            if (pauseParent == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=pause_parent_missing");
            }

            var legacyOptions = root.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == "Options Panel")
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToArray();
            foreach (var legacyOption in legacyOptions)
            {
                UnityEngine.Object.DestroyImmediate(legacyOption);
            }

            var optionsPanel = CreateLobbySettingsPanel(
                lobbySource,
                pauseParent,
                "Options Panel");
            optionsPanel.transform.SetAsLastSibling();
            StyleImages(pausePanel, true);
            StyleTexts(pausePanel, true);

            var backButton = FindTransform(optionsPanel.transform, "Back Button")
                ?.GetComponent<Button>();
            var rebindPanel = optionsPanel.GetComponentInChildren<
                PlayerControlRebindPanel>(true);
            var gameSettingsController = optionsPanel.GetComponentInChildren<
                ParkHanSolGameSettingsController>(true);
            if (backButton == null || rebindPanel == null
                || gameSettingsController == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=lobby_settings_reference_missing");
            }

            var sharedControllers = root.GetComponents<
                NetworkSharedOptionsPanelController>();
            var sharedController = sharedControllers.FirstOrDefault(
                    controller => !PrefabUtility.IsAddedComponentOverride(controller))
                ?? sharedControllers.FirstOrDefault();
            if (sharedController == null)
            {
                sharedController = root.AddComponent<NetworkSharedOptionsPanelController>();
            }

            foreach (var duplicate in sharedControllers.Where(
                         controller => controller != sharedController))
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }

            SetObjectReference(sharedController, "panelRoot", optionsPanel);
            SetObjectReference(sharedController, "rebindPanel", rebindPanel);
            SetObjectReference(sharedController, "windowModeDropdown", null);
            SetObjectReference(sharedController, "resolutionDropdown", null);
            SetObjectReference(
                sharedController,
                "gameSettingsController",
                gameSettingsController);
            SetObjectReference(sharedController, "closeButton", backButton);
            SetObjectReference(pauseController, "optionsPanel", optionsPanel);
            SetObjectReference(pauseController, "optionsBackButton", backButton);
            SetObjectReference(pauseController, "sharedOptionsPanel", sharedController);
            ConfigureDropdownTemplates(root);
            PrefabUtility.SaveAsPrefabAsset(root, hudPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            PrefabUtility.UnloadPrefabContents(lobbySource);
        }
    }

    public static void ConfigurePlayHudForCanonicalVariant()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayHud) == null)
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=network_hud_missing path={NetworkPlayHud}");
        }

        ConfigurePlayHud();
    }

    private static void CreateOwnerPauseUi()
    {
        var sourceRoot = PrefabUtility.LoadPrefabContents(NetworkPlayHud);
        var host = new GameObject(
            "PHS_NetworkOwnerPauseUI",
            typeof(RectTransform),
            typeof(NetworkOwnerUiRoot));
        try
        {
            var sourcePause = FindTransform(sourceRoot.transform, "Pause Menu");
            var sourceOptions = FindTransform(sourceRoot.transform, "Options Panel");
            if (sourcePause == null || sourceOptions == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=pause_source_missing");
            }

            var presentation = new GameObject(
                "PHS_NetworkOwnerPausePresentation",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(ParkHanSolPauseMenuController),
                typeof(NetworkSharedOptionsPanelController));
            presentation.transform.SetParent(host.transform, false);
            StretchRect(presentation.GetComponent<RectTransform>());
            presentation.transform.localScale = Vector3.one;
            var canvas = presentation.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = presentation.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var pausePanel = UnityEngine.Object.Instantiate(sourcePause.gameObject, presentation.transform, false);
            pausePanel.name = "PHS_NetworkPausePanel";
            var nestedOptionsPanel = FindTransform(pausePanel.transform, "Options Panel");
            if (nestedOptionsPanel != null)
            {
                UnityEngine.Object.DestroyImmediate(nestedOptionsPanel.gameObject);
            }

            var optionsPanel = UnityEngine.Object.Instantiate(sourceOptions.gameObject, presentation.transform, false);
            optionsPanel.name = "PHS_NetworkOptionsPanel";
            var resumeButton = FindTransform(pausePanel.transform, "Resume Button")?.GetComponent<Button>();
            var optionsButton = FindTransform(pausePanel.transform, "Options Button")?.GetComponent<Button>();
            var exitButton = FindTransform(pausePanel.transform, "Exit Game Button")?.GetComponent<Button>();
            var backButton = FindTransform(optionsPanel.transform, "Back Button")?.GetComponent<Button>();
            var rebindPanel = optionsPanel.GetComponentInChildren<PlayerControlRebindPanel>(true);
            var gameSettingsController = optionsPanel.GetComponentInChildren<
                ParkHanSolGameSettingsController>(true);
            if (resumeButton == null || optionsButton == null || exitButton == null
                || backButton == null || rebindPanel == null
                || gameSettingsController == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=owner_pause_reference_missing");
            }

            var sharedOptions = presentation.GetComponent<NetworkSharedOptionsPanelController>();
            SetObjectReference(sharedOptions, "panelRoot", optionsPanel);
            SetObjectReference(sharedOptions, "rebindPanel", rebindPanel);
            SetObjectReference(sharedOptions, "windowModeDropdown", null);
            SetObjectReference(sharedOptions, "resolutionDropdown", null);
            SetObjectReference(
                sharedOptions,
                "gameSettingsController",
                gameSettingsController);
            SetObjectReference(sharedOptions, "closeButton", backButton);

            var pauseController = presentation.GetComponent<ParkHanSolPauseMenuController>();
            SetObjectReference(pauseController, "pausePanel", pausePanel);
            SetObjectReference(pauseController, "optionsPanel", optionsPanel);
            SetObjectReference(pauseController, "resumeButton", resumeButton);
            SetObjectReference(pauseController, "optionsButton", optionsButton);
            SetObjectReference(pauseController, "optionsBackButton", backButton);
            SetObjectReference(pauseController, "exitGameButton", exitButton);
            SetObjectReference(pauseController, "sharedOptionsPanel", sharedOptions);
            SetObjectReference(host.GetComponent<NetworkOwnerUiRoot>(), "presentationRoot", presentation);
            ConfigureDropdownTemplates(host);
            PrefabUtility.SaveAsPrefabAsset(host, NetworkOwnerPauseUi);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static GameObject CreateLobbySettingsPanel(
        GameObject lobbySource,
        Transform parent,
        string panelName)
    {
        var backdropSource = lobbySource.transform.Cast<Transform>()
            .FirstOrDefault(child => child.name == "RawImage");
        var settingsSource = lobbySource.transform.Cast<Transform>()
            .FirstOrDefault(child => child.name == "Settings Panel_R");
        if (backdropSource == null || settingsSource == null)
        {
            throw new InvalidOperationException(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=lobby_settings_source_missing");
        }

        var panel = new GameObject(
            panelName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        StretchRect(panelRect);
        var panelBackground = panel.GetComponent<Image>();
        panelBackground.color = new Color(0.008f, 0.012f, 0.016f, 1f);
        panelBackground.raycastTarget = true;
        var panelCanvas = panel.GetComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 500;

        var backdrop = UnityEngine.Object.Instantiate(
            backdropSource.gameObject,
            panel.transform,
            false);
        backdrop.name = "Settings Backdrop";
        var leftMenu = FindTransform(backdrop.transform, "Left Menu");
        if (leftMenu == null)
        {
            UnityEngine.Object.DestroyImmediate(panel);
            throw new InvalidOperationException(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=lobby_settings_left_menu_missing");
        }

        foreach (var child in backdrop.transform.Cast<Transform>().ToArray())
        {
            if (child != leftMenu)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        var settingsPanel = UnityEngine.Object.Instantiate(
            settingsSource.gameObject,
            panel.transform,
            false);
        settingsPanel.name = "Settings Panel_R";
        leftMenu.gameObject.SetActive(true);
        settingsPanel.SetActive(true);
        ConfigureDropdownTemplates(settingsPanel);

        var categoryController = leftMenu.GetComponent<
            ParkHanSolSettingsCategoryMenuController>();
        var gameSettingsController = settingsPanel.GetComponent<
            ParkHanSolGameSettingsController>();
        if (categoryController == null || gameSettingsController == null)
        {
            UnityEngine.Object.DestroyImmediate(panel);
            throw new InvalidOperationException(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=lobby_settings_controller_missing");
        }

        var gameplayPanel = FindTransform(
            settingsPanel.transform,
            "Gameplay Options Panel")?.gameObject;
        var graphicsPanel = FindTransform(
            settingsPanel.transform,
            "Graphics Options Panel")?.gameObject;
        var audioPanel = FindTransform(
            settingsPanel.transform,
            "Audio Options Panel")?.gameObject;
        var voicePanel = FindTransform(
            settingsPanel.transform,
            "Voice Chat Options Panel")?.gameObject;
        var controlsPanel = FindTransform(
            settingsPanel.transform,
            "Controls Options Panel")?.gameObject;
        if (gameplayPanel == null || graphicsPanel == null
            || audioPanel == null || voicePanel == null
            || controlsPanel == null)
        {
            UnityEngine.Object.DestroyImmediate(panel);
            throw new InvalidOperationException(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=lobby_settings_category_panel_missing");
        }

        SetObjectReference(
            categoryController,
            "gameplayPanel",
            gameplayPanel);
        SetObjectReference(
            categoryController,
            "graphicsPanel",
            graphicsPanel);
        SetObjectReference(
            categoryController,
            "audioPanel",
            audioPanel);
        SetObjectReference(
            categoryController,
            "voicePanel",
            voicePanel);
        SetObjectReference(
            categoryController,
            "controlsPanel",
            controlsPanel);

        panel.SetActive(false);
        return panel;
    }

    private static void AttachOwnerPauseUiToPlayer()
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var existing = FindTransform(player.transform, "PHS_NetworkOwnerPauseUI");
            var modified = false;
            if (existing == null)
            {
                var ownerPausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkOwnerPauseUi);
                if (ownerPausePrefab == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=owner_pause_prefab_missing path={NetworkOwnerPauseUi}");
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ownerPausePrefab, player.transform);
                instance.name = "PHS_NetworkOwnerPauseUI";
                modified = true;
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefab);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static void ReplaceUiInScene(string scenePath, string originalPath, string networkPath)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=scene_missing path={scenePath}");
        }

        if (scenePath.Contains("Map", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=map_write_forbidden path={scenePath}");
        }

        var scene = SceneManager.GetSceneByPath(scenePath);
        var wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasAlreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }
        try
        {
            var current = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))
                .Where(instance => instance != null)
                .Distinct()
                .FirstOrDefault(instance =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == originalPath);

            if (current == null)
            {
                var alreadyNetwork = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))
                    .Any(instance => instance != null
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == networkPath);
                if (!alreadyNetwork)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=ui_instance_missing scene={scenePath}");
                }

                return;
            }

            var parent = current.transform.parent;
            var siblingIndex = current.transform.GetSiblingIndex();
            var localPosition = current.transform.localPosition;
            var localRotation = current.transform.localRotation;
            var localScale = current.transform.localScale;
            var networkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(networkPath);
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(networkPrefab, scene);
            replacement.transform.SetParent(parent, false);
            replacement.transform.SetSiblingIndex(siblingIndex);
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(current);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasAlreadyLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureCanvasScalers(GameObject root)
    {
        foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private static void StretchNamedPanel(GameObject root, string name)
    {
        var transform = FindTransform(root.transform, name);
        if (transform != null)
        {
            StretchRect(transform.GetComponent<RectTransform>());
        }
    }

    private static void StretchRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StyleImages(GameObject root, bool forcePanels)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            var color = image.color;
            var blueDominant = color.b > color.r * 1.12f && color.b > color.g * 1.04f;
            if (!blueDominant && !forcePanels)
            {
                continue;
            }

            if (image.GetComponent<Button>() != null)
            {
                image.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Max(0.9f, color.a));
            }
            else
            {
                image.color = image.name.Contains("Background", StringComparison.OrdinalIgnoreCase)
                    ? Charcoal
                    : Surface;
            }
        }
    }

    private static void StyleTexts(GameObject root, bool force)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (force || text.color.b > text.color.r * 1.1f)
            {
                text.color = Cream;
            }
        }
    }

    private static void ConfigureDropdownTemplates(GameObject root)
    {
        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var template = dropdown.template;
            if (template == null)
            {
                continue;
            }

            var viewport = FindTransform(template, "Viewport") as RectTransform;
            var content = viewport == null
                ? null
                : FindTransform(viewport, "Content") as RectTransform;
            var item = content == null ? null : FindTransform(content, "Item") as RectTransform;
            if (viewport == null || content == null || item == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=dropdown_template_invalid dropdown={dropdown.name}");
            }

            template.sizeDelta = new Vector2(template.sizeDelta.x, 180f);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -4f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);

            var layout = content.GetComponent<VerticalLayoutGroup>()
                ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;
            layout.padding = new RectOffset();

            var fitter = content.GetComponent<ContentSizeFitter>()
                ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var itemLayout = item.GetComponent<LayoutElement>()
                ?? item.gameObject.AddComponent<LayoutElement>();
            itemLayout.minHeight = 30f;
            itemLayout.preferredHeight = 30f;
            item.sizeDelta = new Vector2(item.sizeDelta.x, 30f);

            var itemText = item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (itemText == null || dropdown.captionText == null)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=dropdown_item_text_missing dropdown={dropdown.name}");
            }

            var itemTextObject = itemText.gameObject;
            UnityEngine.Object.DestroyImmediate(itemText);
            itemText = itemTextObject.AddComponent<TextMeshProUGUI>();
            itemText.font = dropdown.captionText.font;
            itemText.fontSharedMaterial = dropdown.captionText.fontSharedMaterial;
            itemText.fontSize = dropdown.captionText.fontSize;
            itemText.alignment = TextAlignmentOptions.MidlineLeft;
            itemText.color = Cream;
            itemText.raycastTarget = false;
            var itemTextRect = itemText.rectTransform;
            itemTextRect.anchorMin = Vector2.zero;
            itemTextRect.anchorMax = Vector2.one;
            itemTextRect.offsetMin = new Vector2(10f, 0f);
            itemTextRect.offsetMax = new Vector2(-10f, 0f);
            dropdown.itemText = itemText;
            itemText.transform.SetAsLastSibling();
            foreach (var text in template.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = Cream;
            }
        }
    }

    private static Transform FindTransform(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == name);
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=serialized_property_missing " +
                $"target={target.GetType().Name} property={propertyName}");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
