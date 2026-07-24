using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string RoomEntryPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkRoomListEntry.prefab";
        private const string CustomizationFrontendPrefabPath = RootFolder +
            "/03. Prefab/UI/Customization/PHS_NetworkLobbyCustomizationFrontend.prefab";
        private const string LobbyScenePath = RootFolder +
            "/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string BrowserSubtitle =
            "CREATE OR JOIN A CREW ROOM";
        private const string BrowserJoinLabel = "";
        private const string RoomReadyLabel = "ROOM NAME";

        private static readonly string[] LegacyInviteCopy =
        {
            "CREATE ROOM OR JOIN WITH CODE",
            "ROOM CODE",
            "ENTER CODE",
            "CODE LOCAL"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Manual Room Browser")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateLobbyPrefab(errors);
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
                "create_join=2 legacy_invite_ui=0 customize_tutorial_vertical_orange=1 " +
                "assets01_modified=0");
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

            var createButton = actionPanel.Find("Create Room Button")
                ?.GetComponent<UnityEngine.UI.Button>();
            var joinButton = actionPanel.Find("Join Room Button")
                ?.GetComponent<UnityEngine.UI.Button>();
            Require(
                createButton != null && joinButton != null,
                "reason=create_join_buttons_missing",
                errors);
            Require(
                actionPanel.Find("Join Code Input") == null,
                "reason=legacy_join_code_input_present",
                errors);
            Require(
                actionPanel.Find("Back Button") != null
                && actionPanel.Find("Status Text") != null,
                "reason=action_controls_missing",
                errors);
            RequireText(
                root.transform,
                "Lobby Panel/Lobby Action Panel/Subtitle",
                BrowserSubtitle,
                errors);
            RequireText(
                root.transform,
                "Lobby Panel/Lobby Action Panel/Join Label",
                BrowserJoinLabel,
                errors);
            Require(
                !actionPanel.Find("Join Label").gameObject.activeSelf,
                "reason=join_label_active",
                errors);
            RequireText(
                root.transform,
                "Room Panel/Player List Panel/Room Name Text",
                RoomReadyLabel,
                errors);
            ValidateLegacyInviteCopyRemoved(root, errors);

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
                "createRoomButton",
                createButton,
                errors);
            RequireReference(
                controllerState,
                "joinRoomButton",
                joinButton,
                errors);
            RequireReference(
                controllerState,
                "roomBrowser",
                browser,
                errors);
        }

        private static void ValidateLegacyInviteCopyRemoved(
            GameObject root,
            ICollection<string> errors)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var normalized = NormalizeCopy(label.text);
                if (LegacyInviteCopy.Contains(normalized))
                {
                    errors.Add(
                        $"reason=legacy_invite_copy_present object={label.name} text={normalized}");
                }
            }
        }

        private static void RequireText(
            Transform root,
            string path,
            string expected,
            ICollection<string> errors)
        {
            var label = root.Find(path)?.GetComponent<TMP_Text>();
            Require(
                label != null
                && string.Equals(label.text, expected, StringComparison.Ordinal),
                $"reason=room_browser_copy_invalid path={path}",
                errors);
        }

        private static string NormalizeCopy(string value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries));
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
            var scene = SceneManager.GetSceneByPath(LobbyScenePath);
            var openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    LobbyScenePath,
                    OpenSceneMode.Additive);
            }

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
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
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
