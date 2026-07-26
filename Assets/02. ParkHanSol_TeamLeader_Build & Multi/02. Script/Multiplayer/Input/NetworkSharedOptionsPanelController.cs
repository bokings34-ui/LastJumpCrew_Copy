using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public sealed class NetworkSharedOptionsPanelController : MonoBehaviour, INetworkOptionsPanel
    {
        private const int WindowedIndex = 0;
        private const int BorderlessIndex = 1;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private PlayerControlRebindPanel rebindPanel;
        [SerializeField] private TMP_Dropdown windowModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Button closeButton;
        [SerializeField] private MonoBehaviour saveCuePlayerSource;

        private readonly INetworkPlayerOptionsStore optionsStore =
            NetworkPlayerOptionsStore.Shared;
        private bool setupValid;
        private readonly List<Vector2Int> resolutions = new();
        private INetworkAudioCuePlayer saveCuePlayer;

        public event Action Closed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public bool IsRebinding => rebindPanel != null && rebindPanel.IsRebinding;
        public bool ConsumedCancelThisFrame =>
            rebindPanel != null && rebindPanel.ConsumedCancelThisFrame;

        private void Awake()
        {
            saveCuePlayer = saveCuePlayerSource as INetworkAudioCuePlayer;
            setupValid = ValidateSetup();
            if (!setupValid)
            {
                enabled = false;
                return;
            }

            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "WINDOWED",
                "BORDERLESS"
            });
            windowModeDropdown.onValueChanged.AddListener(SetWindowMode);
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
            closeButton.onClick.AddListener(Close);
            CloseWithoutNotification();
        }

        private void OnDestroy()
        {
            if (!setupValid)
            {
                return;
            }

            windowModeDropdown.onValueChanged.RemoveListener(SetWindowMode);
            resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
            closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_OPEN_FAILED reason=invalid_setup panel={name}",
                    this);
                return;
            }

            RefreshVideoOptions();
            panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            panelRoot.SetActive(false);
            PlaySaveCue();
            Closed?.Invoke();
        }

        public void CloseWithoutNotification()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void SetWindowMode(int index)
        {
            if (index == WindowedIndex)
            {
                optionsStore.SetWindowMode(FullScreenMode.Windowed);
                return;
            }

            if (index == BorderlessIndex)
            {
                optionsStore.SetWindowMode(FullScreenMode.FullScreenWindow);
                return;
            }

            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_WINDOW_FAILED reason=invalid_dropdown_index index={index}",
                this);
        }

        private void RefreshWindowMode()
        {
            if (!optionsStore.TryGetWindowMode(out var mode))
            {
                SetInvalidCaption(windowModeDropdown);
                return;
            }

            windowModeDropdown.SetValueWithoutNotify(
                mode == FullScreenMode.Windowed ? WindowedIndex : BorderlessIndex);
            windowModeDropdown.RefreshShownValue();
        }

        private void RefreshVideoOptions()
        {
            resolutions.Clear();
            resolutions.AddRange(optionsStore.GetSupportedResolutions());
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.ConvertAll(
                resolution => $"{resolution.x} x {resolution.y}"));
            if (!optionsStore.TryGetSavedResolution(out var savedResolution))
            {
                SetInvalidCaption(resolutionDropdown);
                RefreshWindowMode();
                return;
            }

            var index = resolutions.IndexOf(savedResolution);
            if (index < 0)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_RESOLUTION_FAILED reason=saved_resolution_not_supported " +
                    $"width={savedResolution.x} height={savedResolution.y}",
                    this);
                SetInvalidCaption(resolutionDropdown);
                RefreshWindowMode();
                return;
            }

            resolutionDropdown.SetValueWithoutNotify(index);
            resolutionDropdown.RefreshShownValue();
            RefreshWindowMode();
        }

        private static void SetInvalidCaption(TMP_Dropdown dropdown)
        {
            if (dropdown.captionText == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_INVALID_STATE_FAILED reason=caption_missing dropdown={dropdown.name}",
                    dropdown);
                return;
            }

            dropdown.captionText.text = "INVALID SETTING";
        }

        private void SetResolution(int index)
        {
            if (index < 0 || index >= resolutions.Count)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_RESOLUTION_FAILED reason=invalid_dropdown_index " +
                    $"index={index} count={resolutions.Count}",
                    this);
                return;
            }

            optionsStore.SetResolution(resolutions[index]);
        }

        private bool ValidateSetup()
        {
            if (panelRoot != null
                && rebindPanel != null
                && windowModeDropdown != null
                && resolutionDropdown != null
                && closeButton != null)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_SETUP_FAILED panel={name} root={panelRoot != null} " +
                $"rebind={rebindPanel != null} windowMode={windowModeDropdown != null} " +
                $"resolution={resolutionDropdown != null} " +
                $"close={closeButton != null}",
                this);
            return false;
        }

        private void PlaySaveCue()
        {
            if (saveCuePlayer == null)
            {
                return;
            }

            if (!saveCuePlayer.TryPlay(NetworkAudioCue.OptionsSaved, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_OPTIONS_AUDIO_PLAY_FAILED reason={reason} panel={name}",
                    this);
            }
        }
    }
}
