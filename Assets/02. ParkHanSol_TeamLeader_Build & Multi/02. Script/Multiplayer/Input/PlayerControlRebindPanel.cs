using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public sealed class PlayerControlRebindPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class BindingEntry
        {
            [SerializeField] private string actionName;
            [SerializeField] private int bindingIndex;
            [SerializeField] private Button rebindButton;
            [SerializeField] private TMP_Text bindingText;

            public string ActionName => actionName;
            public int BindingIndex => bindingIndex;
            public Button RebindButton => rebindButton;
            public TMP_Text BindingText => bindingText;
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TMP_Text mouseSensitivityValueText;
        [SerializeField] private Button resetBindingsButton;
        [SerializeField] private List<BindingEntry> bindingEntries = new();

        private InputActionRebindingExtensions.RebindingOperation activeRebind;
        private bool setupValid;
        private int lastCancelFrame = -1;
        private readonly INetworkPlayerOptionsStore optionsStore =
            NetworkPlayerOptionsStore.Shared;

        public bool IsRebinding => activeRebind != null;
        public bool ConsumedCancelThisFrame => lastCancelFrame == Time.frameCount;

        private void Awake()
        {
            setupValid = ValidateSetup();
            if (!setupValid)
            {
                enabled = false;
                return;
            }

            mouseSensitivitySlider.minValue = NetworkPlayerController.MinimumMouseSensitivity;
            mouseSensitivitySlider.maxValue = NetworkPlayerController.MaximumMouseSensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
            resetBindingsButton.onClick.AddListener(ResetBindings);
            for (var index = 0; index < bindingEntries.Count; index++)
            {
                var entryIndex = index;
                bindingEntries[index].RebindButton.onClick.AddListener(
                    () => StartRebind(entryIndex));
            }

            LoadSavedBindingOverrides();
            RefreshControls();
        }

        private void OnEnable()
        {
            if (setupValid)
            {
                RefreshControls();
            }
        }

        private void OnDestroy()
        {
            CancelActiveRebind();
            if (!setupValid)
            {
                return;
            }

            mouseSensitivitySlider.onValueChanged.RemoveListener(SetMouseSensitivity);
            resetBindingsButton.onClick.RemoveListener(ResetBindings);
            for (var index = 0; index < bindingEntries.Count; index++)
            {
                bindingEntries[index].RebindButton.onClick.RemoveAllListeners();
            }
        }

        private bool ValidateSetup()
        {
            if (inputActions == null
                || mouseSensitivitySlider == null
                || mouseSensitivityValueText == null
                || resetBindingsButton == null
                || bindingEntries == null
                || bindingEntries.Count == 0)
            {
                Debug.LogError(
                    $"PHS_CONTROL_REBIND_SETUP_FAILED panel={name} actions={inputActions != null} " +
                    $"sensitivitySlider={mouseSensitivitySlider != null} sensitivityText={mouseSensitivityValueText != null} " +
                    $"resetButton={resetBindingsButton != null} entries={bindingEntries?.Count ?? 0}",
                    this);
                return false;
            }

            foreach (var entry in bindingEntries)
            {
                var action = inputActions.FindAction(entry.ActionName, false);
                if (action == null
                    || entry.BindingIndex < 0
                    || entry.BindingIndex >= action.bindings.Count
                    || entry.RebindButton == null
                    || entry.BindingText == null)
                {
                    Debug.LogError(
                        $"PHS_CONTROL_REBIND_SETUP_FAILED panel={name} action={entry.ActionName} " +
                        $"bindingIndex={entry.BindingIndex} actionFound={action != null} " +
                        $"button={entry.RebindButton != null} text={entry.BindingText != null}",
                        this);
                    return false;
                }
            }

            return true;
        }

        private void StartRebind(int entryIndex)
        {
            CancelActiveRebind();
            var entry = bindingEntries[entryIndex];
            var action = inputActions.FindAction(entry.ActionName, true);
            var wasEnabled = action.enabled;
            var previousOverridePath = action.bindings[entry.BindingIndex].overridePath;
            action.Disable();
            entry.BindingText.text = "PRESS A KEY";

            activeRebind = action.PerformInteractiveRebinding(entry.BindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation =>
                {
                    lastCancelFrame = Time.frameCount;
                    FinishRebind(
                        operation,
                        action,
                        entry,
                        wasEnabled,
                        previousOverridePath,
                        false);
                })
                .OnComplete(operation =>
                {
                    FinishRebind(
                        operation,
                        action,
                        entry,
                        wasEnabled,
                        previousOverridePath,
                        true);
                });
            activeRebind.Start();
        }

        private void FinishRebind(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action,
            BindingEntry entry,
            bool wasEnabled,
            string previousOverridePath,
            bool save)
        {
            operation.Dispose();
            activeRebind = null;
            if (wasEnabled)
            {
                action.Enable();
            }

            if (save)
            {
                if (HasBindingConflict(action, entry.BindingIndex))
                {
                    RestoreBindingOverride(action, entry.BindingIndex, previousOverridePath);
                    entry.BindingText.text = "KEY IN USE";
                    Debug.LogError(
                        $"PHS_CONTROL_REBIND_REJECTED reason=duplicate_binding " +
                        $"action={entry.ActionName} bindingIndex={entry.BindingIndex}",
                        this);
                    return;
                }

                optionsStore.SaveBindingOverrides(inputActions);
                ReloadActivePlayerInputs();
            }

            RefreshBindingTexts();
        }

        private void ResetBindings()
        {
            CancelActiveRebind();
            optionsStore.ResetBindingOverrides(inputActions);
            ReloadActivePlayerInputs();
            RefreshBindingTexts();
            Debug.Log("PHS_CONTROL_BINDINGS_RESET");
        }

        private void SetMouseSensitivity(float value)
        {
            optionsStore.SetMouseSensitivity(value);
            RefreshMouseSensitivity();
        }

        private void LoadSavedBindingOverrides()
        {
            optionsStore.LoadBindingOverrides(inputActions);
        }

        private void RefreshControls()
        {
            mouseSensitivitySlider.SetValueWithoutNotify(
                optionsStore.GetMouseSensitivity(NetworkPlayerController.DefaultMouseSensitivity));
            RefreshMouseSensitivity();
            RefreshBindingTexts();
        }

        private void RefreshMouseSensitivity()
        {
            mouseSensitivityValueText.text =
                optionsStore.GetMouseSensitivity(NetworkPlayerController.DefaultMouseSensitivity).ToString("0.00");
        }

        private void RefreshBindingTexts()
        {
            foreach (var entry in bindingEntries)
            {
                var action = inputActions.FindAction(entry.ActionName, true);
                entry.BindingText.text = action.GetBindingDisplayString(entry.BindingIndex);
            }
        }

        private void CancelActiveRebind()
        {
            if (activeRebind == null)
            {
                return;
            }

            activeRebind.Cancel();
            activeRebind = null;
        }

        private bool HasBindingConflict(InputAction targetAction, int targetBindingIndex)
        {
            var targetPath = targetAction.bindings[targetBindingIndex].effectivePath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                Debug.LogError(
                    $"PHS_CONTROL_REBIND_REJECTED reason=effective_path_empty " +
                    $"action={targetAction.name} bindingIndex={targetBindingIndex}",
                    this);
                return true;
            }

            var actionMap = targetAction.actionMap;
            if (actionMap == null)
            {
                Debug.LogError(
                    $"PHS_CONTROL_REBIND_REJECTED reason=action_map_missing action={targetAction.name}",
                    this);
                return true;
            }

            foreach (var action in actionMap.actions)
            {
                for (var bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                {
                    if (action == targetAction && bindingIndex == targetBindingIndex)
                    {
                        continue;
                    }

                    var binding = action.bindings[bindingIndex];
                    if (binding.isComposite || string.IsNullOrWhiteSpace(binding.effectivePath))
                    {
                        continue;
                    }

                    if (string.Equals(
                            binding.effectivePath,
                            targetPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RestoreBindingOverride(
            InputAction action,
            int bindingIndex,
            string previousOverridePath)
        {
            if (string.IsNullOrEmpty(previousOverridePath))
            {
                action.RemoveBindingOverride(bindingIndex);
                return;
            }

            action.ApplyBindingOverride(bindingIndex, previousOverridePath);
        }

        private static void ReloadActivePlayerInputs()
        {
            foreach (var playerInput in FindObjectsByType<PlayerControlInput>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                playerInput.ReloadBindingOverridesFromPreferences();
            }
        }
    }
}
