using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSRoomBrowserRecoveryAuthoring
    {
        private const string RootFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string SourceLobbyPrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/UI/ParkHanSol_StartLobbyUI.prefab";
        private const string SourceRoomEntryPrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/UI/ParkHanSol_RoomListEntry.prefab";
        private const string TargetLobbyPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string TargetRoomEntryPrefabPath = RootFolder +
            "/03. Prefab/UI/PHS_NetworkRoomListEntry.prefab";
        private const string LobbyScenePath = RootFolder +
            "/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";

        private const string LobbyPanelPath = "Lobby Panel";
        private const string ActionPanelName = "Lobby Action Panel";
        private const string CreatePanelName = "Create Room Panel";
        private const string ListPanelName = "Room List Panel";
        private const string PasswordPanelName = "Password Panel";

        [MenuItem("Tools/ParkHanSol/BEAVER/Restore Manual Room Browser")]
        public static void Author()
        {
            RequireAssets();
            var roomEntry = CreateOwnedRoomEntryCopy();
            AuthorLobbyPrefab(roomEntry);
            WireLobbySceneRoomService();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "PHS_ROOM_BROWSER_RECOVERY_AUTHORING_OK " +
                $"prefab={TargetLobbyPrefabPath} scene={LobbyScenePath}");
        }

        private static MultiplayerRoomListItem CreateOwnedRoomEntryCopy()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                TargetRoomEntryPrefabPath);
            if (existing == null)
            {
                if (!AssetDatabase.CopyAsset(
                        SourceRoomEntryPrefabPath,
                        TargetRoomEntryPrefabPath))
                {
                    throw Failure("room_entry_copy_failed");
                }

                AssetDatabase.ImportAsset(
                    TargetRoomEntryPrefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TargetRoomEntryPrefabPath);
            }

            var entries = existing == null
                ? Array.Empty<MultiplayerRoomListItem>()
                : existing.GetComponents<MultiplayerRoomListItem>();
            if (entries.Length != 1)
            {
                throw Failure(
                    $"room_entry_contract count={entries.Length}");
            }

            return entries[0];
        }

        private static void AuthorLobbyPrefab(MultiplayerRoomListItem roomEntry)
        {
            var sourceRoot = PrefabUtility.LoadPrefabContents(
                SourceLobbyPrefabPath);
            var targetRoot = PrefabUtility.LoadPrefabContents(
                TargetLobbyPrefabPath);
            try
            {
                var sourceLobbyPanel = RequireChild(
                    sourceRoot.transform,
                    LobbyPanelPath);
                var targetLobbyPanel = RequireChild(
                    targetRoot.transform,
                    LobbyPanelPath);
                var sourceActionPanel = RequireChild(
                    sourceLobbyPanel,
                    ActionPanelName);
                var sourceCreatePanel = RequireChild(
                    sourceLobbyPanel,
                    CreatePanelName);
                var sourceListPanel = RequireChild(
                    sourceLobbyPanel,
                    ListPanelName);
                var sourcePasswordPanel = RequireChild(
                    sourceLobbyPanel,
                    PasswordPanelName);

                var actionPanel = targetLobbyPanel.Find(ActionPanelName);
                var createPanel = targetLobbyPanel.Find(CreatePanelName);
                var listPanel = targetLobbyPanel.Find(ListPanelName);
                var passwordPanel = targetLobbyPanel.Find(PasswordPanelName);
                var browser = targetRoot.GetComponent<MultiplayerRoomBrowser>();

                var hasAnyRecoveryObject = actionPanel != null
                    || createPanel != null
                    || listPanel != null
                    || passwordPanel != null
                    || browser != null;
                var hasCompleteRecovery = actionPanel != null
                    && createPanel != null
                    && listPanel != null
                    && passwordPanel != null
                    && browser != null;
                if (hasAnyRecoveryObject && !hasCompleteRecovery)
                {
                    throw Failure("partial_existing_recovery_state");
                }

                if (!hasCompleteRecovery)
                {
                    var preservedChildren = targetLobbyPanel
                        .Cast<Transform>()
                        .ToArray();
                    actionPanel = ClonePanel(
                        sourceActionPanel,
                        targetLobbyPanel,
                        true);
                    foreach (var child in preservedChildren)
                    {
                        child.SetParent(actionPanel, false);
                    }

                    createPanel = ClonePanel(
                        sourceCreatePanel,
                        targetLobbyPanel,
                        false);
                    listPanel = ClonePanel(
                        sourceListPanel,
                        targetLobbyPanel,
                        false);
                    passwordPanel = ClonePanel(
                        sourcePasswordPanel,
                        targetLobbyPanel,
                        false);
                    browser = targetRoot.AddComponent<MultiplayerRoomBrowser>();
                }

                var controller = RequireSingle<ParkHanSolLobbyMenuController>(
                    targetRoot,
                    "lobby_controller");
                var selectionIndicator = RequireSingle<
                    ParkHanSolLobbySelectionIndicator>(
                    targetRoot,
                    "selection_indicator");
                RewireSelectionTargets(
                    createPanel,
                    selectionIndicator);
                RewireSelectionTargets(
                    listPanel,
                    selectionIndicator);
                RewireSelectionTargets(
                    passwordPanel,
                    selectionIndicator);

                WireBrowser(
                    browser,
                    controller,
                    selectionIndicator,
                    actionPanel.gameObject,
                    createPanel.gameObject,
                    listPanel.gameObject,
                    passwordPanel.gameObject,
                    roomEntry);
                SetObject(controller, "roomBrowser", browser);

                PrefabUtility.SaveAsPrefabAsset(
                    targetRoot,
                    TargetLobbyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static Transform ClonePanel(
            Transform source,
            Transform parent,
            bool replaceChildren)
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = source.name;
            clone.transform.SetParent(parent, false);
            if (replaceChildren)
            {
                for (var index = clone.transform.childCount - 1;
                     index >= 0;
                     index--)
                {
                    UnityEngine.Object.DestroyImmediate(
                        clone.transform.GetChild(index).gameObject);
                }
            }

            return clone.transform;
        }

        private static void RewireSelectionTargets(
            Transform panel,
            ParkHanSolLobbySelectionIndicator indicator)
        {
            foreach (var target in panel.GetComponentsInChildren<
                         ParkHanSolLobbySelectionTarget>(true))
            {
                target.SetIndicator(indicator);
            }
        }

        private static void WireBrowser(
            MultiplayerRoomBrowser browser,
            ParkHanSolLobbyMenuController controller,
            ParkHanSolLobbySelectionIndicator selectionIndicator,
            GameObject actionPanel,
            GameObject createPanel,
            GameObject listPanel,
            GameObject passwordPanel,
            MultiplayerRoomListItem roomEntry)
        {
            SetObject(browser, "roomService", null);
            SetObject(browser, "lobbyMenuController", controller);
            SetObject(browser, "selectionIndicator", selectionIndicator);
            SetObject(browser, "actionPanel", actionPanel);
            SetObject(browser, "createRoomPanel", createPanel);
            SetObject(browser, "roomListPanel", listPanel);
            SetObject(browser, "passwordPanel", passwordPanel);
            SetObject(browser, "roomListContent", RequireComponent<RectTransform>(
                listPanel.transform,
                "Scroll View/Viewport/Content"));
            SetObject(browser, "entryPrefab", roomEntry);
            SetObject(browser, "roomNameInput", RequireComponent<TMP_InputField>(
                createPanel.transform,
                "Room Name Input"));
            SetObject(browser, "maxPlayersInput", RequireComponent<TMP_InputField>(
                createPanel.transform,
                "Max Players Input"));
            SetObject(browser, "passwordToggle", RequireComponent<Toggle>(
                createPanel.transform,
                "Password Toggle"));
            SetObject(browser, "createPasswordInput", RequireComponent<TMP_InputField>(
                createPanel.transform,
                "Create Password Input"));
            SetObject(browser, "createConfirmButton", RequireComponent<Button>(
                createPanel.transform,
                "Create Confirm Button"));
            SetObject(browser, "createCancelButton", RequireComponent<Button>(
                createPanel.transform,
                "Create Cancel Button"));
            SetObject(browser, "refreshButton", RequireComponent<Button>(
                listPanel.transform,
                "Refresh Button"));
            SetObject(browser, "roomListBackButton", RequireComponent<Button>(
                listPanel.transform,
                "Room List Back Button"));
            SetObject(browser, "joinPasswordInput", RequireComponent<TMP_InputField>(
                passwordPanel.transform,
                "Join Password Input"));
            SetObject(browser, "passwordConfirmButton", RequireComponent<Button>(
                passwordPanel.transform,
                "Password Confirm Button"));
            SetObject(browser, "passwordCancelButton", RequireComponent<Button>(
                passwordPanel.transform,
                "Password Cancel Button"));
            SetObject(browser, "statusText", RequireComponent<TMP_Text>(
                actionPanel.transform,
                "Status Text"));
        }

        private static void WireLobbySceneRoomService()
        {
            if (SceneManager.GetSceneByPath(LobbyScenePath).isLoaded)
            {
                throw Failure("lobby_scene_already_loaded");
            }

            var scene = EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Additive);
            try
            {
                var browsers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        MultiplayerRoomBrowser>(true))
                    .ToArray();
                var services = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        MultiplayerRoomService>(true))
                    .ToArray();
                if (browsers.Length != 1 || services.Length != 1)
                {
                    throw Failure(
                        $"scene_contract browsers={browsers.Length} services={services.Length}");
                }

                SetObject(browsers[0], "roomService", services[0]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    browsers[0]);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T RequireSingle<T>(GameObject root, string label)
            where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            if (components.Length != 1)
            {
                throw Failure($"{label}_count count={components.Length}");
            }

            return components[0];
        }

        private static T RequireComponent<T>(Transform root, string path)
            where T : Component
        {
            var child = RequireChild(root, path);
            var component = child.GetComponent<T>();
            if (component == null)
            {
                throw Failure(
                    $"component_missing path={path} type={typeof(T).Name}");
            }

            return component;
        }

        private static Transform RequireChild(Transform root, string path)
        {
            var child = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (child == null)
            {
                throw Failure($"child_missing path={path}");
            }

            return child;
        }

        private static void SetObject(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw Failure(
                    $"serialized_property_missing target={target.GetType().Name} property={propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void RequireAssets()
        {
            RequireAsset<GameObject>(SourceLobbyPrefabPath);
            RequireAsset<GameObject>(SourceRoomEntryPrefabPath);
            RequireAsset<GameObject>(TargetLobbyPrefabPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyScenePath) == null)
            {
                throw Failure($"scene_missing path={LobbyScenePath}");
            }
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw Failure($"asset_missing path={path}");
            }

            return asset;
        }

        private static InvalidOperationException Failure(string reason)
        {
            return new InvalidOperationException(
                $"PHS_ROOM_BROWSER_RECOVERY_AUTHORING_FAILED reason={reason}");
        }
    }
}
