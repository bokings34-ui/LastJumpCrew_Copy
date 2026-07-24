#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
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
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.name.Contains("Map", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=map_scene_active scene={activeScene.path} " +
                "Close the Map without saving before authoring.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(NetworkLobbyUi) != null)
        {
            Debug.LogError(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=direct_prefab_source_exists " +
                "message=edit_PHS_NetworkStartLobbyUI_directly");
            return;
        }

        CopyWithNewGuid(OriginalLobbyUi, NetworkLobbyUi);
        CopyWithNewGuid(OriginalPlayHud, NetworkPlayHud);
        ConfigureLobbyUi();
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
            StretchNamedPanel(root, "Settings Panel_R");
            StretchNamedPanel(root, "Controls Options Panel");
            PrefabUtility.SaveAsPrefabAsset(root, NetworkLobbyUi);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayHud()
    {
        var lobbySource = PrefabUtility.LoadPrefabContents(NetworkLobbyUi);
        var root = PrefabUtility.LoadPrefabContents(NetworkPlayHud);
        try
        {
            root.name = "PHS_NetworkPlayHudUI";
            ConfigureCanvasScalers(root);
            var pausePanel = FindTransform(root.transform, "Pause Menu")?.gameObject;
            var optionsPanel = FindTransform(root.transform, "Options Panel")?.gameObject;
            var optionsCard = FindTransform(root.transform, "Options Card");
            var backButton = FindTransform(root.transform, "Back Button")?.GetComponent<Button>();
            var rebindPanel = root.GetComponentInChildren<PlayerControlRebindPanel>(true);
            var pauseController = root.GetComponentInChildren<ParkHanSolPauseMenuController>(true);
            var dropdownSource = lobbySource.GetComponentsInChildren<TMP_Dropdown>(true).FirstOrDefault();

            if (pausePanel == null || optionsPanel == null || optionsCard == null
                || backButton == null || rebindPanel == null || pauseController == null
                || dropdownSource == null)
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

            optionsPanel.transform.SetParent(pauseParent, false);
            optionsPanel.transform.SetSiblingIndex(
                pausePanel.transform.GetSiblingIndex() + 1);
            optionsPanel.transform.localScale = Vector3.one;

            StyleImages(pausePanel, true);
            StyleImages(optionsPanel, true);
            StyleTexts(pausePanel, true);
            StyleTexts(optionsPanel, true);
            StretchRect(optionsPanel.GetComponent<RectTransform>());
            ConfigureResponsiveCard(optionsCard.GetComponent<RectTransform>());
            var videoOptionsRow = EnsureVideoOptionsRow(optionsCard);
            ReserveControlsPanelTopInset(optionsCard, 150f);

            var existingDropdown = FindTransform(optionsCard, "Window Mode Dropdown");
            TMP_Dropdown windowModeDropdown;
            if (existingDropdown == null)
            {
                var dropdownObject = UnityEngine.Object.Instantiate(
                    dropdownSource.gameObject,
                    videoOptionsRow,
                    false);
                dropdownObject.name = "Window Mode Dropdown";
                windowModeDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                RemovePersistentDropdownListeners(windowModeDropdown);
            }
            else
            {
                existingDropdown.SetParent(videoOptionsRow, false);
                windowModeDropdown = existingDropdown.GetComponent<TMP_Dropdown>();
            }
            ConfigureVideoDropdownRect(
                windowModeDropdown.GetComponent<RectTransform>(),
                0.02f,
                0.48f);

            var existingResolutionDropdown = FindTransform(optionsCard, "Resolution Dropdown");
            TMP_Dropdown resolutionDropdown;
            if (existingResolutionDropdown == null)
            {
                var dropdownObject = UnityEngine.Object.Instantiate(
                    dropdownSource.gameObject,
                    videoOptionsRow,
                    false);
                dropdownObject.name = "Resolution Dropdown";
                resolutionDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                RemovePersistentDropdownListeners(resolutionDropdown);
            }
            else
            {
                existingResolutionDropdown.SetParent(videoOptionsRow, false);
                resolutionDropdown = existingResolutionDropdown.GetComponent<TMP_Dropdown>();
            }
            ConfigureVideoDropdownRect(
                resolutionDropdown.GetComponent<RectTransform>(),
                0.52f,
                0.98f);

            var sharedController = root.GetComponent<NetworkSharedOptionsPanelController>();
            if (sharedController == null)
            {
                sharedController = root.AddComponent<NetworkSharedOptionsPanelController>();
            }

            SetObjectReference(sharedController, "panelRoot", optionsPanel);
            SetObjectReference(sharedController, "rebindPanel", rebindPanel);
            SetObjectReference(sharedController, "windowModeDropdown", windowModeDropdown);
            SetObjectReference(sharedController, "resolutionDropdown", resolutionDropdown);
            SetObjectReference(sharedController, "closeButton", backButton);
            SetObjectReference(pauseController, "sharedOptionsPanel", sharedController);
            PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayHud);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            PrefabUtility.UnloadPrefabContents(lobbySource);
        }
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
            var windowModeDropdown = optionsPanel.GetComponentsInChildren<TMP_Dropdown>(true)
                .FirstOrDefault(dropdown => dropdown.name == "Window Mode Dropdown");
            var resolutionDropdown = optionsPanel.GetComponentsInChildren<TMP_Dropdown>(true)
                .FirstOrDefault(dropdown => dropdown.name == "Resolution Dropdown");
            if (resumeButton == null || optionsButton == null || exitButton == null
                || backButton == null || rebindPanel == null || windowModeDropdown == null
                || resolutionDropdown == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=owner_pause_reference_missing");
            }

            var sharedOptions = presentation.GetComponent<NetworkSharedOptionsPanelController>();
            SetObjectReference(sharedOptions, "panelRoot", optionsPanel);
            SetObjectReference(sharedOptions, "rebindPanel", rebindPanel);
            SetObjectReference(sharedOptions, "windowModeDropdown", windowModeDropdown);
            SetObjectReference(sharedOptions, "resolutionDropdown", resolutionDropdown);
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
            PrefabUtility.SaveAsPrefabAsset(host, NetworkOwnerPauseUi);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void AttachOwnerPauseUiToPlayer()
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var existing = FindTransform(player.transform, "PHS_NetworkOwnerPauseUI");
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
            }

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefab);
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

    private static void ConfigureResponsiveCard(RectTransform card)
    {
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(960f, 760f);
        var fitter = card.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            UnityEngine.Object.DestroyImmediate(fitter);
        }
    }

    private static Transform EnsureVideoOptionsRow(Transform optionsCard)
    {
        var existing = FindTransform(optionsCard, "PHS_NetworkVideoOptionsRow");
        var row = existing != null
            ? existing.GetComponent<RectTransform>()
            : new GameObject(
                    "PHS_NetworkVideoOptionsRow",
                    typeof(RectTransform))
                .GetComponent<RectTransform>();
        row.SetParent(optionsCard, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -96f);
        row.sizeDelta = new Vector2(-72f, 78f);
        return row;
    }

    private static void ReserveControlsPanelTopInset(
        Transform optionsCard,
        float topInset)
    {
        var controlsPanel = FindTransform(
            optionsCard,
            "Pause Controls Options Panel");
        var controlsRect = controlsPanel?.GetComponent<RectTransform>();
        if (controlsRect == null)
        {
            throw new InvalidOperationException(
                "PHS_NETWORK_OPTIONS_AUTHOR_FAILED reason=controls_panel_missing");
        }

        controlsRect.offsetMax = new Vector2(
            controlsRect.offsetMax.x,
            -34f - topInset);
    }

    private static void ConfigureVideoDropdownRect(
        RectTransform rect,
        float anchorMinX,
        float anchorMaxX)
    {
        rect.anchorMin = new Vector2(anchorMinX, 0f);
        rect.anchorMax = new Vector2(anchorMaxX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void RemovePersistentDropdownListeners(TMP_Dropdown dropdown)
    {
        while (dropdown.onValueChanged.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(dropdown.onValueChanged, 0);
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
