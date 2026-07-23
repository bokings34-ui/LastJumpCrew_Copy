using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSRoomBrowserRecoveryValidator
    {
        private const string RootFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string LobbyPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string BaseLobbyPrefabPath = RootFolder +
            "/03. Prefab/UI/ParkHanSol_StartLobbyUI.prefab";
        private const string RoomEntryPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkRoomListEntry.prefab";
        private const string CustomizationFrontendPrefabPath = RootFolder +
            "/03. Prefab/UI/Customization/PHS_NetworkLobbyCustomizationFrontend.prefab";
        private const string LobbyScenePath = RootFolder +
            "/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Manual Room Browser")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateLobbyPrefab(errors);
            ValidateLobbyPanelStateTransitions(errors);
            ValidateRoomEntry(errors);
            ValidateCustomizationLayout(errors);
            ValidateLobbyScene(errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError(
                        $"PHS_ROOM_BROWSER_RECOVERY_VALIDATION_FAILED {error}");
                }

                throw new InvalidOperationException(
                    $"PHS_ROOM_BROWSER_RECOVERY_VALIDATION_FAILED count={errors.Count}");
            }

            Debug.Log(
                "PHS_ROOM_BROWSER_RECOVERY_VALIDATION_PASS " +
                "browser=1 panels=4 prefab_refs=20 scene_room_service=1 " +
                "preserved_create_join=2 customize_tutorial_vertical_orange=1 " +
                "lobby_panel_state_transitions=2 " +
                "assets01_modified=0");
        }

        private static void ValidateLobbyPanelStateTransitions(
            ICollection<string> errors)
        {
            foreach (var prefabPath in new[]
                     {
                         BaseLobbyPrefabPath,
                         LobbyPrefabPath
                     })
            {
                ValidateLobbyPanelStateTransitions(prefabPath, errors);
            }
        }

        private static void ValidateLobbyPanelStateTransitions(
            string prefabPath,
            ICollection<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                errors.Add(
                    $"reason=lobby_panel_state_prefab_missing path={prefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                errors.Add(
                    $"reason=lobby_panel_state_prefab_missing path={prefabPath}");
                return;
            }

            try
            {
                var controllers = root.GetComponentsInChildren<
                    ParkHanSolLobbyMenuController>(true);
                Require(
                    controllers.Length == 1,
                    $"reason=lobby_panel_state_controller_count path={prefabPath} actual={controllers.Length}",
                    errors);
                if (controllers.Length != 1)
                {
                    return;
                }

                var controller = controllers[0];
                var serialized = new SerializedObject(controller);
                var startPanel = GetGameObjectReference(serialized, "startPanel");
                var settingsPanel = GetGameObjectReference(serialized, "settingsPanel");
                var settingsLeftMenu = GetGameObjectReference(
                    serialized,
                    "settingsLeftMenu");
                var settingsApplyButton = GetGameObjectReference(
                    serialized,
                    "settingsApplyButton");
                Require(
                    startPanel != null
                    && settingsPanel != null
                    && settingsLeftMenu != null
                    && settingsApplyButton != null,
                    $"reason=lobby_panel_state_reference_missing path={prefabPath}",
                    errors);
                if (startPanel == null
                    || settingsPanel == null
                    || settingsLeftMenu == null
                    || settingsApplyButton == null)
                {
                    return;
                }

                if (!InvokeControllerStateMethod(
                        controller,
                        "ShowStartImmediate",
                        prefabPath,
                        errors))
                {
                    return;
                }

                ValidateTransitionPanelState(
                    startPanel,
                    expectedVisible: true,
                    expectedInteractive: true,
                    requireInactive: false,
                    $"start_return_start_panel path={prefabPath}",
                    errors);
                ValidateTransitionPanelState(
                    settingsPanel,
                    expectedVisible: false,
                    expectedInteractive: false,
                    requireInactive: true,
                    $"start_return_settings_panel path={prefabPath}",
                    errors);
                Require(
                    !settingsLeftMenu.activeSelf
                    && !settingsApplyButton.activeSelf,
                    $"reason=start_return_settings_auxiliary_active path={prefabPath}",
                    errors);

                if (!InvokeControllerStateMethod(
                        controller,
                        "ShowSettings",
                        prefabPath,
                        errors,
                        true))
                {
                    return;
                }

                ValidateTransitionPanelState(
                    startPanel,
                    expectedVisible: false,
                    expectedInteractive: false,
                    requireInactive: true,
                    $"settings_entry_start_panel path={prefabPath}",
                    errors);
                ValidateTransitionPanelState(
                    settingsPanel,
                    expectedVisible: true,
                    expectedInteractive: true,
                    requireInactive: false,
                    $"settings_entry_settings_panel path={prefabPath}",
                    errors);
                Require(
                    settingsLeftMenu.activeSelf
                    && settingsApplyButton.activeSelf,
                    $"reason=settings_entry_auxiliary_inactive path={prefabPath}",
                    errors);

                InvokeControllerStateMethod(
                    controller,
                    "ShowStartImmediate",
                    prefabPath,
                    errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject GetGameObjectReference(
            SerializedObject serialized,
            string propertyName)
        {
            return serialized.FindProperty(propertyName)?.objectReferenceValue
                as GameObject;
        }

        private static bool InvokeControllerStateMethod(
            ParkHanSolLobbyMenuController controller,
            string methodName,
            string prefabPath,
            ICollection<string> errors,
            params object[] arguments)
        {
            var method = typeof(ParkHanSolLobbyMenuController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                errors.Add(
                    $"reason=lobby_panel_state_method_missing path={prefabPath} method={methodName}");
                return false;
            }

            try
            {
                method.Invoke(controller, arguments);
                return true;
            }
            catch (TargetInvocationException exception)
            {
                errors.Add(
                    $"reason=lobby_panel_state_method_failed path={prefabPath} " +
                    $"method={methodName} exception={exception.InnerException?.GetType().Name ?? exception.GetType().Name}");
                return false;
            }
        }

        private static void ValidateTransitionPanelState(
            GameObject panel,
            bool expectedVisible,
            bool? expectedInteractive,
            bool requireInactive,
            string label,
            ICollection<string> errors)
        {
            var transition = panel.GetComponent<ParkHanSolLobbyPanelTransition>();
            if (transition == null)
            {
                Require(
                    panel.activeSelf == expectedVisible,
                    $"reason={label}_active_invalid expected={expectedVisible} actual={panel.activeSelf}",
                    errors);
                return;
            }

            var targetVisibleField = typeof(ParkHanSolLobbyPanelTransition)
                .GetField(
                    "targetVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Require(
                targetVisibleField != null,
                $"reason={label}_transition_target_field_missing",
                errors);
            if (targetVisibleField != null)
            {
                var actualTarget = (bool)targetVisibleField.GetValue(transition);
                Require(
                    actualTarget == expectedVisible,
                    $"reason={label}_transition_target_invalid expected={expectedVisible} actual={actualTarget}",
                    errors);
            }

            Require(
                !expectedVisible || panel.activeSelf,
                $"reason={label}_visible_panel_inactive",
                errors);
            Require(
                !requireInactive || !panel.activeSelf,
                $"reason={label}_hidden_panel_active",
                errors);

            var canvasGroup = panel.GetComponent<CanvasGroup>();
            Require(
                canvasGroup != null,
                $"reason={label}_canvas_group_missing",
                errors);
            if (canvasGroup == null || !expectedInteractive.HasValue)
            {
                return;
            }

            Require(
                canvasGroup.interactable == expectedInteractive.Value
                && canvasGroup.blocksRaycasts == expectedInteractive.Value,
                $"reason={label}_interaction_invalid expected={expectedInteractive.Value} " +
                $"interactable={canvasGroup.interactable} blocksRaycasts={canvasGroup.blocksRaycasts}",
                errors);
        }

        private static void ValidateLobbyPrefab(ICollection<string> errors)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                LobbyPrefabPath);
            if (root == null)
            {
                errors.Add($"reason=lobby_prefab_missing path={LobbyPrefabPath}");
                return;
            }

            var browsers = root.GetComponents<MultiplayerRoomBrowser>();
            var controllers = root.GetComponents<ParkHanSolLobbyMenuController>();
            if (browsers.Length != 1 || controllers.Length != 1)
            {
                errors.Add(
                    $"reason=root_component_count browsers={browsers.Length} controllers={controllers.Length}");
                return;
            }

            var lobbyPanel = root.transform.Find("Lobby Panel");
            if (lobbyPanel == null)
            {
                errors.Add("reason=lobby_panel_missing");
                return;
            }

            var actionPanel = lobbyPanel.Find("Lobby Action Panel");
            var createPanel = lobbyPanel.Find("Create Room Panel");
            var listPanel = lobbyPanel.Find("Room List Panel");
            var passwordPanel = lobbyPanel.Find("Password Panel");
            Require(actionPanel != null, "reason=action_panel_missing", errors);
            Require(createPanel != null, "reason=create_panel_missing", errors);
            Require(listPanel != null, "reason=list_panel_missing", errors);
            Require(passwordPanel != null, "reason=password_panel_missing", errors);
            if (actionPanel == null || createPanel == null
                || listPanel == null || passwordPanel == null)
            {
                return;
            }

            Require(
                actionPanel.Find("Create Room Button") != null
                && actionPanel.Find("Join Room Button") != null
                && actionPanel.Find("Join Code Input") != null
                && actionPanel.Find("Back Button") != null
                && actionPanel.Find("Status Text") != null,
                "reason=preserved_action_controls_missing",
                errors);

            var browser = browsers[0];
            var browserState = new SerializedObject(browser);
            RequireReference(browserState, "lobbyMenuController", controllers[0], errors);
            RequireReference(browserState, "actionPanel", actionPanel.gameObject, errors);
            RequireReference(browserState, "createRoomPanel", createPanel.gameObject, errors);
            RequireReference(browserState, "roomListPanel", listPanel.gameObject, errors);
            RequireReference(browserState, "passwordPanel", passwordPanel.gameObject, errors);
            RequireNullReference(browserState, "roomService", errors);

            foreach (var propertyName in new[]
                     {
                         "selectionIndicator",
                         "roomListContent",
                         "entryPrefab",
                         "roomNameInput",
                         "maxPlayersInput",
                         "passwordToggle",
                         "createPasswordInput",
                         "createConfirmButton",
                         "createCancelButton",
                         "refreshButton",
                         "roomListBackButton",
                         "joinPasswordInput",
                         "passwordConfirmButton",
                         "passwordCancelButton",
                         "statusText"
                     })
            {
                RequireNonNullReference(browserState, propertyName, errors);
            }

            var entryProperty = browserState.FindProperty("entryPrefab");
            Require(
                entryProperty != null
                && AssetDatabase.GetAssetPath(
                    entryProperty.objectReferenceValue) == RoomEntryPrefabPath,
                "reason=entry_prefab_not_owned_copy",
                errors);

            var controllerState = new SerializedObject(controllers[0]);
            RequireReference(
                controllerState,
                "roomBrowser",
                browser,
                errors);
        }

        private static void ValidateRoomEntry(ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                RoomEntryPrefabPath);
            Require(
                prefab != null
                && prefab.GetComponents<MultiplayerRoomListItem>().Length == 1,
                "reason=owned_room_entry_invalid",
                errors);
        }

        private static void ValidateCustomizationLayout(
            ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CustomizationFrontendPrefabPath);
            if (prefab == null)
            {
                errors.Add(
                    $"reason=customization_frontend_missing path={CustomizationFrontendPrefabPath}");
                return;
            }

            var customize = prefab.transform.Find("OpenCustomizationButton")
                as RectTransform;
            var tutorial = prefab.transform.Find("TrainingButton")
                as RectTransform;
            Require(
                customize != null && tutorial != null,
                "reason=customize_tutorial_button_missing",
                errors);
            if (customize == null || tutorial == null)
            {
                return;
            }

            var expectedColor = new Color(1f, 0.404f, 0.282f, 1f);
            Require(
                Approximately(customize.anchorMin, new Vector2(0.5f, 0.29f))
                && Approximately(tutorial.anchorMin, new Vector2(0.5f, 0.21f))
                && Approximately(customize.sizeDelta, new Vector2(420f, 56f))
                && Approximately(tutorial.sizeDelta, new Vector2(420f, 56f))
                && customize.anchorMin.y > tutorial.anchorMin.y,
                "reason=customize_tutorial_vertical_layout_changed",
                errors);
            Require(
                HasLabelColor(customize, expectedColor)
                && HasLabelColor(tutorial, expectedColor),
                "reason=customize_tutorial_orange_changed",
                errors);
        }

        private static void ValidateLobbyScene(ICollection<string> errors)
        {
            if (SceneManager.GetSceneByPath(LobbyScenePath).isLoaded)
            {
                errors.Add("reason=lobby_scene_already_loaded");
                return;
            }

            var scene = EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Additive);
            try
            {
                var sceneTransforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        Transform>(true))
                    .ToArray();
                var browsers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        MultiplayerRoomBrowser>(true))
                    .ToArray();
                var services = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        MultiplayerRoomService>(true))
                    .ToArray();
                var lobbyMenus = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        ParkHanSolLobbyMenuController>(true))
                    .ToArray();
                var frontends = sceneTransforms
                    .Where(transform =>
                        transform.name
                            == "PHS_NetworkLobbyCustomizationFrontend")
                    .ToArray();
                Require(
                    lobbyMenus.Length == 1 && frontends.Length == 1,
                    $"reason=start_option_set_count menus={lobbyMenus.Length} frontends={frontends.Length}",
                    errors);
                if (lobbyMenus.Length == 1 && frontends.Length == 1)
                {
                    var startPanel = new SerializedObject(lobbyMenus[0])
                        .FindProperty("startPanel")
                        ?.objectReferenceValue as GameObject;
                    Require(
                        startPanel != null,
                        "reason=start_option_set_start_panel_missing",
                        errors);
                    Require(
                        startPanel != null
                        && frontends[0].parent == startPanel.transform,
                        "reason=customize_tutorial_not_direct_start_panel_children",
                        errors);
                }

                Require(
                    browsers.Length == 1 && services.Length == 1,
                    $"reason=scene_component_count browsers={browsers.Length} services={services.Length}",
                    errors);
                if (browsers.Length != 1 || services.Length != 1)
                {
                    return;
                }

                var browserState = new SerializedObject(browsers[0]);
                RequireReference(
                    browserState,
                    "roomService",
                    services[0],
                    errors);
                Require(
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        browsers[0]) != null,
                    "reason=browser_not_prefab_owned",
                    errors);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RequireReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object expected,
            ICollection<string> errors)
        {
            var property = serialized.FindProperty(propertyName);
            Require(
                property != null && property.objectReferenceValue == expected,
                $"reason=reference_invalid target={serialized.targetObject.GetType().Name} property={propertyName}",
                errors);
        }

        private static void RequireNonNullReference(
            SerializedObject serialized,
            string propertyName,
            ICollection<string> errors)
        {
            var property = serialized.FindProperty(propertyName);
            Require(
                property != null && property.objectReferenceValue != null,
                $"reason=reference_missing target={serialized.targetObject.GetType().Name} property={propertyName}",
                errors);
        }

        private static void RequireNullReference(
            SerializedObject serialized,
            string propertyName,
            ICollection<string> errors)
        {
            var property = serialized.FindProperty(propertyName);
            Require(
                property != null && property.objectReferenceValue == null,
                $"reason=prefab_scene_reference_leak property={propertyName}",
                errors);
        }

        private static void Require(
            bool condition,
            string error,
            ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }

        private static bool HasLabelColor(
            Component button,
            Color expected)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            return label != null && Approximately(label.color, expected);
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= 0.0001f
                && Mathf.Abs(left.g - right.g) <= 0.0001f
                && Mathf.Abs(left.b - right.b) <= 0.0001f
                && Mathf.Abs(left.a - right.a) <= 0.0001f;
        }
    }
}
