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
            action.Disable();
            entry.BindingText.text = "PRESS A KEY";

            activeRebind = action.PerformInteractiveRebinding(entry.BindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation =>
                {
                    FinishRebind(operation, action, wasEnabled, false);
                })
                .OnComplete(operation =>
                {
                    FinishRebind(operation, action, wasEnabled, true);
                });
            activeRebind.Start();
        }

        private void FinishRebind(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action,
            bool wasEnabled,
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
                SaveBindingOverrides();
                ReloadActivePlayerInputs();
            }

            RefreshBindingTexts();
        }

        private void ResetBindings()
        {
            CancelActiveRebind();
            inputActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PlayerControlInput.BindingOverridesPreferenceKey);
            PlayerPrefs.Save();
            ReloadActivePlayerInputs();
            RefreshBindingTexts();
            Debug.Log("PHS_CONTROL_BINDINGS_RESET");
        }

        private void SetMouseSensitivity(float value)
        {
            NetworkPlayerController.SaveMouseSensitivity(value);
            PlayerPrefs.Save();
            RefreshMouseSensitivity();
        }

        private void LoadSavedBindingOverrides()
        {
            if (!PlayerPrefs.HasKey(PlayerControlInput.BindingOverridesPreferenceKey))
            {
                return;
            }

            var json = PlayerPrefs.GetString(
                PlayerControlInput.BindingOverridesPreferenceKey,
                string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    $"PHS_CONTROL_REBIND_LOAD_FAILED reason=saved_json_empty " +
                    $"key={PlayerControlInput.BindingOverridesPreferenceKey}",
                    this);
                return;
            }

            try
            {
                inputActions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"PHS_CONTROL_REBIND_LOAD_FAILED reason=invalid_json " +
                    $"exception={exception.GetType().Name} message={exception.Message}",
                    this);
            }
        }

        private void SaveBindingOverrides()
        {
            PlayerPrefs.SetString(
                PlayerControlInput.BindingOverridesPreferenceKey,
                inputActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            Debug.Log("PHS_CONTROL_BINDING_SAVED");
        }

        private void RefreshControls()
        {
            mouseSensitivitySlider.SetValueWithoutNotify(
                NetworkPlayerController.GetSavedMouseSensitivity());
            RefreshMouseSensitivity();
            RefreshBindingTexts();
        }

        private void RefreshMouseSensitivity()
        {
            mouseSensitivityValueText.text =
                NetworkPlayerController.GetSavedMouseSensitivity().ToString("0.00");
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
