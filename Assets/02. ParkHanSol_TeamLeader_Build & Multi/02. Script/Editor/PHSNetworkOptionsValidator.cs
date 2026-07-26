using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkOptionsValidator
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string LobbyPrefabPath = Root +
            "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string PlayHudPrefabPath = Root +
            "/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab";
        private const string OwnerPausePrefabPath = Root +
            "/03. Prefab/UI/PHS_NetworkOwnerPauseUI.prefab";
        private const string PlayerPrefabPath = Root +
            "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Network Options")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateLobbyOptionsPrefab(errors);
            ValidateSharedOptionsPrefab(
                PlayHudPrefabPath,
                false,
                errors);
            ValidateSharedOptionsPrefab(
                OwnerPausePrefabPath,
                true,
                errors);
            ValidatePlayerOwnerPauseConnection(errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError(
                        $"PHS_NETWORK_OPTIONS_VALIDATION_FAILED {error}");
                }

                throw new InvalidOperationException(
                    $"PHS_NETWORK_OPTIONS_VALIDATION_FAILED count={errors.Count}");
            }

            Debug.Log(
                "PHS_NETWORK_OPTIONS_VALIDATION_PASS " +
                "prefabs=3 player_owner_pause_connected=1 serialized_refs=valid");
        }

        private static void ValidateLobbyOptionsPrefab(
            ICollection<string> errors)
        {
            var prefab = LoadPrefab(LobbyPrefabPath, errors);
            if (prefab == null)
            {
                return;
            }

            var settingsControllers = prefab.GetComponentsInChildren<
                ParkHanSolGameSettingsController>(true);
            var rebindPanels = prefab.GetComponentsInChildren<
                PlayerControlRebindPanel>(true);
            Require(
                settingsControllers.Length == 1,
                $"reason=lobby_settings_controller_count actual={settingsControllers.Length}",
                errors);
            Require(
                rebindPanels.Length == 1,
                $"reason=lobby_rebind_panel_count actual={rebindPanels.Length}",
                errors);
            if (settingsControllers.Length != 1)
            {
                return;
            }

            var state = new SerializedObject(settingsControllers[0]);
            foreach (var propertyName in new[]
                     {
                         "resolutionDropdown",
                         "fullScreenToggle",
                         "qualityDropdown",
                         "vSyncToggle",
                         "applyButton"
                     })
            {
                RequireNonNullReference(
                    state,
                    propertyName,
                    LobbyPrefabPath,
                    errors);
            }
        }

        private static void ValidateSharedOptionsPrefab(
            string path,
            bool requireOwnerRoot,
            ICollection<string> errors)
        {
            var prefab = LoadPrefab(path, errors);
            if (prefab == null)
            {
                return;
            }

            var sharedControllers = prefab.GetComponentsInChildren<
                NetworkSharedOptionsPanelController>(true);
            var pauseControllers = prefab.GetComponentsInChildren<
                ParkHanSolPauseMenuController>(true);
            Require(
                sharedControllers.Length == 1,
                $"reason=shared_options_controller_count path={path} actual={sharedControllers.Length}",
                errors);
            Require(
                pauseControllers.Length == 1,
                $"reason=pause_controller_count path={path} actual={pauseControllers.Length}",
                errors);
            if (sharedControllers.Length != 1 || pauseControllers.Length != 1)
            {
                return;
            }

            var shared = sharedControllers[0];
            var pause = pauseControllers[0];
            var sharedState = new SerializedObject(shared);
            var pauseState = new SerializedObject(pause);

            var panelRoot = GetReference<GameObject>(
                sharedState,
                "panelRoot",
                path,
                errors);
            var rebindPanel = GetReference<PlayerControlRebindPanel>(
                sharedState,
                "rebindPanel",
                path,
                errors);
            var windowModeDropdown = GetReference<Component>(
                sharedState,
                "windowModeDropdown",
                path,
                errors);
            var resolutionDropdown = GetReference<Component>(
                sharedState,
                "resolutionDropdown",
                path,
                errors);
            var closeButton = GetReference<Component>(
                sharedState,
                "closeButton",
                path,
                errors);

            var pausePanel = GetReference<GameObject>(
                pauseState,
                "pausePanel",
                path,
                errors);
            var optionsPanel = GetReference<GameObject>(
                pauseState,
                "optionsPanel",
                path,
                errors);
            var resumeButton = GetReference<Component>(
                pauseState,
                "resumeButton",
                path,
                errors);
            var optionsButton = GetReference<Component>(
                pauseState,
                "optionsButton",
                path,
                errors);
            var optionsBackButton = GetReference<Component>(
                pauseState,
                "optionsBackButton",
                path,
                errors);
            var exitGameButton = GetReference<Component>(
                pauseState,
                "exitGameButton",
                path,
                errors);
            var sharedOptionsPanel = GetReference<
                NetworkSharedOptionsPanelController>(
                pauseState,
                "sharedOptionsPanel",
                path,
                errors);

            Require(
                panelRoot != null && panelRoot == optionsPanel,
                $"reason=shared_panel_not_pause_options path={path}",
                errors);
            Require(
                closeButton != null && closeButton == optionsBackButton,
                $"reason=shared_close_not_pause_back path={path}",
                errors);
            Require(
                sharedOptionsPanel == shared,
                $"reason=pause_shared_options_reference_invalid path={path}",
                errors);
            Require(
                pausePanel != null && pausePanel != optionsPanel,
                $"reason=pause_options_panels_invalid path={path}",
                errors);

            RequireContained(rebindPanel, panelRoot, "rebindPanel", path, errors);
            RequireContained(windowModeDropdown, panelRoot, "windowModeDropdown", path, errors);
            RequireContained(resolutionDropdown, panelRoot, "resolutionDropdown", path, errors);
            RequireContained(closeButton, panelRoot, "closeButton", path, errors);
            RequireContained(resumeButton, pausePanel, "resumeButton", path, errors);
            RequireContained(optionsButton, pausePanel, "optionsButton", path, errors);
            RequireContained(exitGameButton, pausePanel, "exitGameButton", path, errors);

            ValidateOwnerRoot(
                prefab,
                shared,
                pause,
                requireOwnerRoot,
                path,
                errors);
        }

        private static void ValidateOwnerRoot(
            GameObject prefab,
            Component shared,
            Component pause,
            bool required,
            string path,
            ICollection<string> errors)
        {
            var ownerRoots = prefab.GetComponentsInChildren<
                NetworkOwnerUiRoot>(true);
            if (!required)
            {
                Require(
                    ownerRoots.Length == 0,
                    $"reason=unexpected_owner_ui_root path={path} actual={ownerRoots.Length}",
                    errors);
                return;
            }

            Require(
                ownerRoots.Length == 1,
                $"reason=owner_ui_root_count path={path} actual={ownerRoots.Length}",
                errors);
            if (ownerRoots.Length != 1)
            {
                return;
            }

            var presentationRoot = GetReference<GameObject>(
                new SerializedObject(ownerRoots[0]),
                "presentationRoot",
                path,
                errors);
            Require(
                ownerRoots[0].gameObject == prefab,
                $"reason=owner_ui_component_not_prefab_root path={path}",
                errors);
            RequireContained(shared, presentationRoot, "sharedOptionsController", path, errors);
            RequireContained(pause, presentationRoot, "pauseController", path, errors);
        }

        private static void ValidatePlayerOwnerPauseConnection(
            ICollection<string> errors)
        {
            var player = LoadPrefab(PlayerPrefabPath, errors);
            if (player == null)
            {
                return;
            }

            var ownerRoots = player.GetComponentsInChildren<
                NetworkOwnerUiRoot>(true);
            Require(
                ownerRoots.Length == 1,
                $"reason=player_owner_ui_root_count actual={ownerRoots.Length}",
                errors);
            var dependencies = AssetDatabase.GetDependencies(
                PlayerPrefabPath,
                false);
            Require(
                dependencies.Contains(
                    OwnerPausePrefabPath,
                    StringComparer.Ordinal),
                "reason=player_owner_pause_prefab_dependency_missing",
                errors);
            if (ownerRoots.Length != 1)
            {
                return;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(
                ownerRoots[0].gameObject);
            Require(
                source != null
                && AssetDatabase.GetAssetPath(source) == OwnerPausePrefabPath,
                $"reason=player_owner_pause_source_invalid actual={AssetDatabase.GetAssetPath(source)}",
                errors);
        }

        private static GameObject LoadPrefab(
            string path,
            ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(
                prefab != null,
                $"reason=prefab_missing path={path}",
                errors);
            return prefab;
        }

        private static T GetReference<T>(
            SerializedObject state,
            string propertyName,
            string path,
            ICollection<string> errors)
            where T : UnityEngine.Object
        {
            var property = state.FindProperty(propertyName);
            var value = property?.objectReferenceValue as T;
            Require(
                property != null && value != null,
                $"reason=reference_missing path={path} target={state.targetObject.GetType().Name} property={propertyName}",
                errors);
            return value;
        }

        private static void RequireNonNullReference(
            SerializedObject state,
            string propertyName,
            string path,
            ICollection<string> errors)
        {
            var property = state.FindProperty(propertyName);
            Require(
                property != null && property.objectReferenceValue != null,
                $"reason=reference_missing path={path} target={state.targetObject.GetType().Name} property={propertyName}",
                errors);
        }

        private static void RequireContained(
            Component component,
            GameObject expectedRoot,
            string propertyName,
            string path,
            ICollection<string> errors)
        {
            Require(
                component != null
                && expectedRoot != null
                && (component.gameObject == expectedRoot
                    || component.transform.IsChildOf(expectedRoot.transform)),
                $"reason=reference_outside_expected_root path={path} property={propertyName}",
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
    }
}
