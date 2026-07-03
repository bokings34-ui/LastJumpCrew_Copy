using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolGameSettingsController : MonoBehaviour
    {
        private const string FullScreenKey = "PHS_FullScreen";
        private const string MasterVolumeKey = "PHS_MasterVolume";
        private const string MicVolumeKey = "PHS_MicVolume";
        private const string PartyVolumeKey = "PHS_PartyVolume";
        private const string OutputVolumeKey = "PHS_OutputVolume";
        private const string QualityKey = "PHS_Quality";
        private const string VSyncKey = "PHS_VSync";

        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Dropdown microphoneDropdown;
        [SerializeField] private Dropdown outputDeviceDropdown;
        [SerializeField] private Slider microphoneVolumeSlider;
        [SerializeField] private Slider partyVolumeSlider;
        [SerializeField] private Dropdown participantDropdown;
        [SerializeField] private Slider participantVolumeSlider;
        [SerializeField] private Slider outputVolumeSlider;
        [SerializeField] private Toggle microphoneMuteToggle;
        [SerializeField] private Toggle outputMuteToggle;
        [SerializeField] private Text statusText;

        private readonly List<Resolution> resolutions = new();
        private bool suppressEvents;

        private async void OnEnable()
        {
            RefreshVideoOptions();
            ApplySavedValuesToControls();

            if (voiceChatSession != null && await voiceChatSession.PrepareVoiceSettingsAsync())
            {
                RefreshVoiceOptions();
            }
            else
            {
                SetStatus("VOICE OFFLINE");
            }
        }

        private void Awake()
        {
            BindControls();
            ApplySavedRuntimeValues();
        }

        private void OnDestroy()
        {
            UnbindControls();
        }

        private void BindControls()
        {
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
            if (fullScreenToggle != null) fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
            if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(SetVSync);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            if (microphoneDropdown != null) microphoneDropdown.onValueChanged.AddListener(SetMicrophoneDevice);
            if (outputDeviceDropdown != null) outputDeviceDropdown.onValueChanged.AddListener(SetOutputDevice);
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.onValueChanged.AddListener(SetMicrophoneVolume);
            if (partyVolumeSlider != null) partyVolumeSlider.onValueChanged.AddListener(SetPartyVolume);
            if (participantDropdown != null) participantDropdown.onValueChanged.AddListener(SetParticipantSelection);
            if (participantVolumeSlider != null) participantVolumeSlider.onValueChanged.AddListener(SetParticipantVolume);
            if (outputVolumeSlider != null) outputVolumeSlider.onValueChanged.AddListener(SetOutputVolume);
            if (microphoneMuteToggle != null) microphoneMuteToggle.onValueChanged.AddListener(SetMicrophoneMuted);
            if (outputMuteToggle != null) outputMuteToggle.onValueChanged.AddListener(SetOutputMuted);
        }

        private void UnbindControls()
        {
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
            if (fullScreenToggle != null) fullScreenToggle.onValueChanged.RemoveListener(SetFullScreen);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(SetQuality);
            if (vSyncToggle != null) vSyncToggle.onValueChanged.RemoveListener(SetVSync);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            if (microphoneDropdown != null) microphoneDropdown.onValueChanged.RemoveListener(SetMicrophoneDevice);
            if (outputDeviceDropdown != null) outputDeviceDropdown.onValueChanged.RemoveListener(SetOutputDevice);
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.onValueChanged.RemoveListener(SetMicrophoneVolume);
            if (partyVolumeSlider != null) partyVolumeSlider.onValueChanged.RemoveListener(SetPartyVolume);
            if (participantDropdown != null) participantDropdown.onValueChanged.RemoveListener(SetParticipantSelection);
            if (participantVolumeSlider != null) participantVolumeSlider.onValueChanged.RemoveListener(SetParticipantVolume);
            if (outputVolumeSlider != null) outputVolumeSlider.onValueChanged.RemoveListener(SetOutputVolume);
            if (microphoneMuteToggle != null) microphoneMuteToggle.onValueChanged.RemoveListener(SetMicrophoneMuted);
            if (outputMuteToggle != null) outputMuteToggle.onValueChanged.RemoveListener(SetOutputMuted);
        }

        private void RefreshVideoOptions()
        {
            resolutions.Clear();
            resolutions.AddRange(Screen.resolutions
                .GroupBy(value => new { value.width, value.height })
                .Select(group => group.OrderByDescending(value => value.refreshRateRatio.value).First())
                .OrderBy(value => value.width)
                .ThenBy(value => value.height));

            if (resolutions.Count == 0)
            {
                resolutions.Add(Screen.currentResolution);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(resolutions
                    .Select(value => $"{value.width} x {value.height}")
                    .ToList());
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(QualitySettings.names.ToList());
            }
        }

        private void RefreshVoiceOptions()
        {
            suppressEvents = true;
            SetDropdownOptions(microphoneDropdown, voiceChatSession.GetInputDeviceNames(), voiceChatSession.GetActiveInputDeviceIndex());
            SetDropdownOptions(outputDeviceDropdown, voiceChatSession.GetOutputDeviceNames(), voiceChatSession.GetActiveOutputDeviceIndex());
            SetDropdownOptions(participantDropdown, voiceChatSession.GetRemoteParticipantNames(), 0);
            suppressEvents = false;
            SetStatus("READY");
        }

        private void ApplySavedValuesToControls()
        {
            suppressEvents = true;

            if (fullScreenToggle != null) fullScreenToggle.isOn = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
            if (masterVolumeSlider != null) masterVolumeSlider.value = PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume);
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.value = PlayerPrefs.GetInt(MicVolumeKey, 0);
            if (partyVolumeSlider != null) partyVolumeSlider.value = PlayerPrefs.GetInt(PartyVolumeKey, 0);
            if (outputVolumeSlider != null) outputVolumeSlider.value = PlayerPrefs.GetInt(OutputVolumeKey, 0);
            if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            if (vSyncToggle != null) vSyncToggle.isOn = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            if (resolutionDropdown != null) resolutionDropdown.value = FindCurrentResolutionIndex();

            suppressEvents = false;
        }

        private void ApplySavedRuntimeValues()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume));
            SetQuality(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()));
            SetVSync(PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
        }

        private void SetResolution(int index)
        {
            if (suppressEvents || index < 0 || index >= resolutions.Count)
            {
                return;
            }

            var resolution = resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
            SetStatus($"{resolution.width} x {resolution.height}");
        }

        private void SetFullScreen(bool active)
        {
            if (suppressEvents)
            {
                return;
            }

            Screen.fullScreenMode = active ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(FullScreenKey, active ? 1 : 0);
        }

        private void SetQuality(int index)
        {
            if (suppressEvents || QualitySettings.names.Length == 0)
            {
                return;
            }

            var quality = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(quality);
            PlayerPrefs.SetInt(QualityKey, quality);
        }

        private void SetVSync(bool active)
        {
            if (suppressEvents)
            {
                return;
            }

            QualitySettings.vSyncCount = active ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, active ? 1 : 0);
        }

        private void SetMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
        }

        private void SetMicrophoneDevice(int index)
        {
            if (!suppressEvents) voiceChatSession?.SetInputDeviceByIndex(index);
        }

        private void SetOutputDevice(int index)
        {
            if (!suppressEvents) voiceChatSession?.SetOutputDeviceByIndex(index);
        }

        private void SetMicrophoneVolume(float value)
        {
            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetInputVolume(volume);
            PlayerPrefs.SetInt(MicVolumeKey, volume);
        }

        private void SetPartyVolume(float value)
        {
            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetPartyVolume(volume);
            PlayerPrefs.SetInt(PartyVolumeKey, volume);
        }

        private void SetParticipantSelection(int index)
        {
            if (suppressEvents || participantVolumeSlider == null)
            {
                return;
            }

            SetParticipantVolume(participantVolumeSlider.value);
        }

        private void SetParticipantVolume(float value)
        {
            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetRemoteParticipantVolumeByIndex(participantDropdown == null ? 0 : participantDropdown.value, volume);
        }

        private void SetOutputVolume(float value)
        {
            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetOutputVolume(volume);
            PlayerPrefs.SetInt(OutputVolumeKey, volume);
        }

        private void SetMicrophoneMuted(bool active)
        {
            voiceChatSession?.SetInputMuted(active);
        }

        private void SetOutputMuted(bool active)
        {
            voiceChatSession?.SetOutputMuted(active);
        }

        private int FindCurrentResolutionIndex()
        {
            for (var i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                {
                    return i;
                }
            }

            return Mathf.Max(0, resolutions.Count - 1);
        }

        private static void SetDropdownOptions(Dropdown dropdown, IReadOnlyList<string> options, int selectedIndex)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options.Count == 0 ? new List<string> { "DEFAULT" } : options.ToList());
            dropdown.value = Mathf.Clamp(selectedIndex, 0, dropdown.options.Count - 1);
            dropdown.RefreshShownValue();
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }
    }
}
