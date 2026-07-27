using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolGameSettingsController : MonoBehaviour
    {
        private const string FullScreenKey = "PHS_FullScreen";
        private const string MasterVolumeKey = "PHS_MasterVolume";
        private const string EnvironmentVolumeKey = "PHS_EnvironmentVolume";
        private const string EffectsVolumeKey = "PHS_EffectsVolume";
        private const string GameVoiceVolumeKey = "PHS_GameVoiceVolume";
        private const string MicVolumeKey = "PHS_MicVolume";
        private const string MicMutedKey = "PHS_MicMuted";
        private const string MicDeviceIdKey = "PHS_MicDeviceId";
        private const string PartyVolumeKey = "PHS_PartyVolume";
        private const string OutputVolumeKey = "PHS_OutputVolume";
        private const string OutputMutedKey = "PHS_OutputMuted";
        private const string OutputDeviceIdKey = "PHS_OutputDeviceId";
        private const string QualityKey = "PHS_Quality";
        private const string ResolutionWidthKey = "PHS_ResolutionWidth";
        private const string ResolutionHeightKey = "PHS_ResolutionHeight";
        private const string VSyncKey = "PHS_VSync";

        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider environmentVolumeSlider;
        [SerializeField] private Slider effectsVolumeSlider;
        [SerializeField] private Slider gameVoiceVolumeSlider;
        [SerializeField] private TMP_Dropdown microphoneDropdown;
        [SerializeField] private TMP_Dropdown outputDeviceDropdown;
        [SerializeField] private Slider microphoneVolumeSlider;
        [SerializeField] private Slider partyVolumeSlider;
        [SerializeField] private TMP_Dropdown participantDropdown;
        [SerializeField] private Slider participantVolumeSlider;
        [SerializeField] private Slider outputVolumeSlider;
        [SerializeField] private Toggle microphoneMuteToggle;
        [SerializeField] private Toggle outputMuteToggle;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button applyButton;

        private readonly List<Resolution> resolutions = new();
        private bool suppressEvents;
        private SettingsSnapshot savedSnapshot;
        private bool hasSnapshot;
        private int settingsLoadVersion;

        private async void OnEnable()
        {
            var loadVersion = ++settingsLoadVersion;
            RefreshVideoOptions();
            ApplySavedValuesToControls();
            SetVoiceControlsAvailable(false);

            var voiceReady = voiceChatSession != null &&
                await voiceChatSession.PrepareVoiceSettingsAsync();
            if (!IsSettingsLoadCurrent(loadVersion))
            {
                return;
            }

            if (voiceReady)
            {
                await ApplySavedVoiceDevicesAsync();
                if (!IsSettingsLoadCurrent(loadVersion))
                {
                    return;
                }

                SetVoiceControlsAvailable(true);
                RefreshVoiceOptions();
                UpdateSavedVoiceDeviceSelection();
            }
            else
            {
                SetVoiceControlsAvailable(false);
                suppressEvents = true;
                SetDropdownOptions(microphoneDropdown, new List<string> { "VOICE OFFLINE" }, 0);
                SetDropdownOptions(outputDeviceDropdown, new List<string> { "VOICE OFFLINE" }, 0);
                SetDropdownOptions(participantDropdown, new List<string> { "NO MEMBERS" }, 0);
                suppressEvents = false;
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
            if (applyButton != null) applyButton.onClick.AddListener(ApplySettings);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
            if (fullScreenToggle != null) fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
            if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(SetVSync);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            if (environmentVolumeSlider != null) environmentVolumeSlider.onValueChanged.AddListener(SetEnvironmentVolume);
            if (effectsVolumeSlider != null) effectsVolumeSlider.onValueChanged.AddListener(SetEffectsVolume);
            if (gameVoiceVolumeSlider != null) gameVoiceVolumeSlider.onValueChanged.AddListener(SetGameVoiceVolume);
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
            if (applyButton != null) applyButton.onClick.RemoveListener(ApplySettings);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
            if (fullScreenToggle != null) fullScreenToggle.onValueChanged.RemoveListener(SetFullScreen);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(SetQuality);
            if (vSyncToggle != null) vSyncToggle.onValueChanged.RemoveListener(SetVSync);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            if (environmentVolumeSlider != null) environmentVolumeSlider.onValueChanged.RemoveListener(SetEnvironmentVolume);
            if (effectsVolumeSlider != null) effectsVolumeSlider.onValueChanged.RemoveListener(SetEffectsVolume);
            if (gameVoiceVolumeSlider != null) gameVoiceVolumeSlider.onValueChanged.RemoveListener(SetGameVoiceVolume);
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
            var availableResolutions = Screen.resolutions
                .GroupBy(value => new { value.width, value.height })
                .Select(group => group.OrderByDescending(value => value.refreshRateRatio.value).First())
                .OrderByDescending(value => value.width)
                .ThenByDescending(value => value.height)
                .ToList();
            var savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
            var savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
            var supportedResolutions = availableResolutions
                .Where(value =>
                    (value.width >= 1280 && value.height >= 720) ||
                    (value.width == Screen.width && value.height == Screen.height) ||
                    (value.width == savedWidth && value.height == savedHeight))
                .ToList();

            resolutions.Clear();
            resolutions.AddRange(supportedResolutions.Count > 0
                ? supportedResolutions
                : availableResolutions);

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
                var qualityNames = QualitySettings.names;
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(qualityNames.Length <= 1
                    ? new List<string> { "DEFAULT" }
                    : qualityNames.Select(GetQualityDisplayName).ToList());
                qualityDropdown.interactable = qualityNames.Length > 1;
                qualityDropdown.RefreshShownValue();
            }
        }

        private void OnDisable()
        {
            settingsLoadVersion++;
        }

        private void RefreshVoiceOptions()
        {
            var participantNames = voiceChatSession.GetRemoteParticipantNames();
            suppressEvents = true;
            SetDropdownOptions(microphoneDropdown, voiceChatSession.GetInputDeviceNames(), voiceChatSession.GetActiveInputDeviceIndex());
            SetDropdownOptions(outputDeviceDropdown, voiceChatSession.GetOutputDeviceNames(), voiceChatSession.GetActiveOutputDeviceIndex());
            SetDropdownOptions(
                participantDropdown,
                participantNames.Count == 0 ? new List<string> { "NO MEMBERS" } : participantNames,
                0);
            suppressEvents = false;
            if (participantDropdown != null) participantDropdown.interactable = participantNames.Count > 0;
            if (participantVolumeSlider != null) participantVolumeSlider.interactable = participantNames.Count > 0;
            if (participantNames.Count > 0 && participantDropdown != null)
            {
                SetParticipantSelection(participantDropdown.value);
            }

            SetStatus("READY");
        }

        private void SetVoiceControlsAvailable(bool available)
        {
            if (microphoneDropdown != null) microphoneDropdown.interactable = available;
            if (outputDeviceDropdown != null) outputDeviceDropdown.interactable = available;
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.interactable = available;
            if (partyVolumeSlider != null) partyVolumeSlider.interactable = available;
            if (outputVolumeSlider != null) outputVolumeSlider.interactable = available;
            if (microphoneMuteToggle != null) microphoneMuteToggle.interactable = available;
            if (outputMuteToggle != null) outputMuteToggle.interactable = available;
            if (participantDropdown != null) participantDropdown.interactable = available;
            if (participantVolumeSlider != null) participantVolumeSlider.interactable = available;
        }

        private void ApplySavedValuesToControls()
        {
            suppressEvents = true;

            if (fullScreenToggle != null) fullScreenToggle.isOn = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
            if (masterVolumeSlider != null) masterVolumeSlider.value = PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume);
            if (environmentVolumeSlider != null) environmentVolumeSlider.value = PlayerPrefs.GetFloat(EnvironmentVolumeKey, 1f);
            if (effectsVolumeSlider != null) effectsVolumeSlider.value = PlayerPrefs.GetFloat(EffectsVolumeKey, 1f);
            if (gameVoiceVolumeSlider != null) gameVoiceVolumeSlider.value = PlayerPrefs.GetFloat(GameVoiceVolumeKey, 1f);
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.value = PlayerPrefs.GetInt(MicVolumeKey, 0);
            if (partyVolumeSlider != null) partyVolumeSlider.value = PlayerPrefs.GetInt(PartyVolumeKey, 0);
            if (outputVolumeSlider != null) outputVolumeSlider.value = PlayerPrefs.GetInt(OutputVolumeKey, 0);
            if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            if (vSyncToggle != null) vSyncToggle.isOn = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            if (resolutionDropdown != null) resolutionDropdown.value = FindSavedResolutionIndex();
            if (microphoneMuteToggle != null) microphoneMuteToggle.isOn = PlayerPrefs.GetInt(MicMutedKey, 0) == 1;
            if (outputMuteToggle != null) outputMuteToggle.isOn = PlayerPrefs.GetInt(OutputMutedKey, 0) == 1;

            suppressEvents = false;
            ApplyVoiceControlValues();
            savedSnapshot = CaptureCurrentSettings();
            hasSnapshot = true;
        }

        private void ApplySavedRuntimeValues()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume));
            ApplySavedResolution();

            if (QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1));
            }

            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1 ? 1 : 0;
        }

        public void ApplySettings()
        {
            SaveCurrentControlValues();
            savedSnapshot = CaptureCurrentSettings(true);
            hasSnapshot = true;
            PlayerPrefs.Save();
            SetStatus("SAVED");
        }

        public void CancelSettings()
        {
            if (!hasSnapshot)
            {
                return;
            }

            RestoreSettings(savedSnapshot);
            ApplySnapshotToControls(savedSnapshot);
            SetStatus("CANCELED");
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
            SetStatus(active ? "FULLSCREEN" : "WINDOWED");
        }

        private void SetQuality(int index)
        {
            if (suppressEvents || QualitySettings.names.Length == 0)
            {
                return;
            }

            var quality = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(quality);
            if (vSyncToggle != null)
            {
                QualitySettings.vSyncCount = vSyncToggle.isOn ? 1 : 0;
            }

            var displayName = qualityDropdown != null &&
                quality >= 0 &&
                quality < qualityDropdown.options.Count
                    ? qualityDropdown.options[quality].text
                    : GetQualityDisplayName(QualitySettings.names[quality]);
            SetStatus(displayName);
        }

        private void SetVSync(bool active)
        {
            if (suppressEvents)
            {
                return;
            }

            QualitySettings.vSyncCount = active ? 1 : 0;
            SetStatus(active ? "VSYNC ON" : "VSYNC OFF");
        }

        private void SetMasterVolume(float value)
        {
            var volume = Mathf.Clamp01(value);
            AudioListener.volume = volume;
            SetStatus($"MASTER {Mathf.RoundToInt(volume * 100f)}%");
        }

        private void SetEnvironmentVolume(float value)
        {
            SetStatus($"ENV {Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}");
        }

        private void SetEffectsVolume(float value)
        {
            SetStatus($"FX {Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}");
        }

        private void SetGameVoiceVolume(float value)
        {
            SetStatus($"VOICE {Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}");
        }

        private async void SetMicrophoneDevice(int index)
        {
            if (!suppressEvents && voiceChatSession != null)
            {
                await voiceChatSession.SetInputDeviceByIndexAsync(index);
            }
        }

        private async void SetOutputDevice(int index)
        {
            if (!suppressEvents && voiceChatSession != null)
            {
                await voiceChatSession.SetOutputDeviceByIndexAsync(index);
            }
        }

        private void SetMicrophoneVolume(float value)
        {
            if (suppressEvents)
            {
                return;
            }

            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetInputVolume(volume);
            SetStatus($"MIC LEVEL {FormatSignedLevel(volume)}");
        }

        private void SetPartyVolume(float value)
        {
            if (suppressEvents)
            {
                return;
            }

            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetPartyVolume(volume);
            SetStatus($"PARTY LEVEL {FormatSignedLevel(volume)}");
        }

        private void SetParticipantSelection(int index)
        {
            if (suppressEvents ||
                participantVolumeSlider == null ||
                voiceChatSession == null ||
                !voiceChatSession.TryGetRemoteParticipantVolumeByIndex(index, out var volume))
            {
                return;
            }

            participantVolumeSlider.SetValueWithoutNotify(volume);
            SetStatus($"MEMBER LEVEL {FormatSignedLevel(volume)}");
        }

        private void SetParticipantVolume(float value)
        {
            if (suppressEvents)
            {
                return;
            }

            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetRemoteParticipantVolumeByIndex(participantDropdown == null ? 0 : participantDropdown.value, volume);
            SetStatus($"MEMBER LEVEL {FormatSignedLevel(volume)}");
        }

        private void SetOutputVolume(float value)
        {
            if (suppressEvents)
            {
                return;
            }

            var volume = Mathf.RoundToInt(value);
            voiceChatSession?.SetOutputVolume(volume);
            SetStatus($"OUTPUT LEVEL {FormatSignedLevel(volume)}");
        }

        private void SetMicrophoneMuted(bool active)
        {
            if (suppressEvents)
            {
                return;
            }

            voiceChatSession?.SetInputMuted(active);
            SetStatus(active ? "MIC MUTED" : "MIC ACTIVE");
        }

        private void SetOutputMuted(bool active)
        {
            if (suppressEvents)
            {
                return;
            }

            voiceChatSession?.SetOutputMuted(active);
            SetStatus(active ? "OUTPUT MUTED" : "OUTPUT ACTIVE");
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

            return FindClosestResolutionIndex(Screen.width, Screen.height);
        }

        private int FindSavedResolutionIndex()
        {
            var savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
            var savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);

            for (var i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == savedWidth && resolutions[i].height == savedHeight)
                {
                    return i;
                }
            }

            return FindCurrentResolutionIndex();
        }

        private void ApplySavedResolution()
        {
            var savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
            var savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
            var fullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
            Screen.SetResolution(savedWidth, savedHeight, fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private int FindClosestResolutionIndex(int width, int height)
        {
            var closestIndex = 0;
            var closestDistance = long.MaxValue;
            for (var i = 0; i < resolutions.Count; i++)
            {
                var widthDelta = (long)resolutions[i].width - width;
                var heightDelta = (long)resolutions[i].height - height;
                var distance = widthDelta * widthDelta + heightDelta * heightDelta;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestIndex = i;
            }

            return closestIndex;
        }

        private SettingsSnapshot CaptureCurrentSettings(bool useSelectedResolution = false)
        {
            var resolutionIndex = resolutionDropdown == null
                ? FindCurrentResolutionIndex()
                : resolutionDropdown.value;
            var resolutionWidth = Screen.width;
            var resolutionHeight = Screen.height;
            if (useSelectedResolution &&
                resolutionIndex >= 0 &&
                resolutionIndex < resolutions.Count)
            {
                resolutionWidth = resolutions[resolutionIndex].width;
                resolutionHeight = resolutions[resolutionIndex].height;
            }

            var microphoneDeviceIndex = microphoneDropdown == null ? 0 : microphoneDropdown.value;
            var outputDeviceIndex = outputDeviceDropdown == null ? 0 : outputDeviceDropdown.value;
            var microphoneDeviceId = voiceChatSession?.GetInputDeviceIdByIndex(microphoneDeviceIndex);
            var outputDeviceId = voiceChatSession?.GetOutputDeviceIdByIndex(outputDeviceIndex);
            if (string.IsNullOrWhiteSpace(microphoneDeviceId))
            {
                microphoneDeviceId = PlayerPrefs.GetString(MicDeviceIdKey, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(outputDeviceId))
            {
                outputDeviceId = PlayerPrefs.GetString(OutputDeviceIdKey, string.Empty);
            }

            return new SettingsSnapshot
            {
                ResolutionIndex = resolutionIndex,
                ResolutionWidth = resolutionWidth,
                ResolutionHeight = resolutionHeight,
                FullScreen = Screen.fullScreen,
                MasterVolume = AudioListener.volume,
                EnvironmentVolume = environmentVolumeSlider == null ? PlayerPrefs.GetFloat(EnvironmentVolumeKey, 1f) : environmentVolumeSlider.value,
                EffectsVolume = effectsVolumeSlider == null ? PlayerPrefs.GetFloat(EffectsVolumeKey, 1f) : effectsVolumeSlider.value,
                GameVoiceVolume = gameVoiceVolumeSlider == null ? PlayerPrefs.GetFloat(GameVoiceVolumeKey, 1f) : gameVoiceVolumeSlider.value,
                MicrophoneVolume = Mathf.RoundToInt(microphoneVolumeSlider == null ? PlayerPrefs.GetInt(MicVolumeKey, 0) : microphoneVolumeSlider.value),
                PartyVolume = Mathf.RoundToInt(partyVolumeSlider == null ? PlayerPrefs.GetInt(PartyVolumeKey, 0) : partyVolumeSlider.value),
                OutputVolume = Mathf.RoundToInt(outputVolumeSlider == null ? PlayerPrefs.GetInt(OutputVolumeKey, 0) : outputVolumeSlider.value),
                Quality = QualitySettings.GetQualityLevel(),
                VSync = QualitySettings.vSyncCount > 0,
                MicrophoneDevice = microphoneDeviceIndex,
                OutputDevice = outputDeviceIndex,
                MicrophoneDeviceId = microphoneDeviceId,
                OutputDeviceId = outputDeviceId,
                MicrophoneMuted = microphoneMuteToggle != null && microphoneMuteToggle.isOn,
                OutputMuted = outputMuteToggle != null && outputMuteToggle.isOn,
                ParticipantVolumes = voiceChatSession?.CaptureRemoteParticipantVolumes()
            };
        }

        private void ApplySnapshotToControls(SettingsSnapshot snapshot)
        {
            suppressEvents = true;

            if (resolutionDropdown != null) resolutionDropdown.value = Mathf.Clamp(snapshot.ResolutionIndex, 0, Mathf.Max(0, resolutionDropdown.options.Count - 1));
            if (fullScreenToggle != null) fullScreenToggle.isOn = snapshot.FullScreen;
            if (masterVolumeSlider != null) masterVolumeSlider.value = snapshot.MasterVolume;
            if (environmentVolumeSlider != null) environmentVolumeSlider.value = snapshot.EnvironmentVolume;
            if (effectsVolumeSlider != null) effectsVolumeSlider.value = snapshot.EffectsVolume;
            if (gameVoiceVolumeSlider != null) gameVoiceVolumeSlider.value = snapshot.GameVoiceVolume;
            if (microphoneVolumeSlider != null) microphoneVolumeSlider.value = snapshot.MicrophoneVolume;
            if (partyVolumeSlider != null) partyVolumeSlider.value = snapshot.PartyVolume;
            if (outputVolumeSlider != null) outputVolumeSlider.value = snapshot.OutputVolume;
            if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(snapshot.Quality, 0, Mathf.Max(0, qualityDropdown.options.Count - 1));
            if (vSyncToggle != null) vSyncToggle.isOn = snapshot.VSync;
            if (microphoneDropdown != null) microphoneDropdown.value = Mathf.Clamp(snapshot.MicrophoneDevice, 0, Mathf.Max(0, microphoneDropdown.options.Count - 1));
            if (outputDeviceDropdown != null) outputDeviceDropdown.value = Mathf.Clamp(snapshot.OutputDevice, 0, Mathf.Max(0, outputDeviceDropdown.options.Count - 1));
            if (microphoneMuteToggle != null) microphoneMuteToggle.isOn = snapshot.MicrophoneMuted;
            if (outputMuteToggle != null) outputMuteToggle.isOn = snapshot.OutputMuted;

            suppressEvents = false;
        }

        private void RestoreSettings(SettingsSnapshot snapshot)
        {
            if (snapshot.ResolutionWidth > 0 && snapshot.ResolutionHeight > 0)
            {
                Screen.SetResolution(
                    snapshot.ResolutionWidth,
                    snapshot.ResolutionHeight,
                    snapshot.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
            else if (snapshot.ResolutionIndex >= 0 && snapshot.ResolutionIndex < resolutions.Count)
            {
                var resolution = resolutions[snapshot.ResolutionIndex];
                Screen.SetResolution(resolution.width, resolution.height, snapshot.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
            else
            {
                Screen.fullScreenMode = snapshot.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }

            AudioListener.volume = Mathf.Clamp01(snapshot.MasterVolume);
            if (QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(snapshot.Quality, 0, QualitySettings.names.Length - 1));
            }

            QualitySettings.vSyncCount = snapshot.VSync ? 1 : 0;
            if (voiceChatSession != null)
            {
                _ = string.IsNullOrWhiteSpace(snapshot.MicrophoneDeviceId)
                    ? voiceChatSession.SetInputDeviceByIndexAsync(snapshot.MicrophoneDevice)
                    : voiceChatSession.SetInputDeviceByIdAsync(snapshot.MicrophoneDeviceId);
                _ = string.IsNullOrWhiteSpace(snapshot.OutputDeviceId)
                    ? voiceChatSession.SetOutputDeviceByIndexAsync(snapshot.OutputDevice)
                    : voiceChatSession.SetOutputDeviceByIdAsync(snapshot.OutputDeviceId);
                voiceChatSession.RestoreRemoteParticipantVolumes(snapshot.ParticipantVolumes);
            }

            voiceChatSession?.SetInputVolume(snapshot.MicrophoneVolume);
            voiceChatSession?.SetPartyVolume(snapshot.PartyVolume);
            voiceChatSession?.SetOutputVolume(snapshot.OutputVolume);
            voiceChatSession?.SetInputMuted(snapshot.MicrophoneMuted);
            voiceChatSession?.SetOutputMuted(snapshot.OutputMuted);
        }

        private void SaveCurrentControlValues()
        {
            PlayerPrefs.SetInt(FullScreenKey, fullScreenToggle == null ? Screen.fullScreen ? 1 : 0 : fullScreenToggle.isOn ? 1 : 0);
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolumeSlider == null ? AudioListener.volume : Mathf.Clamp01(masterVolumeSlider.value));
            PlayerPrefs.SetFloat(EnvironmentVolumeKey, environmentVolumeSlider == null ? 1f : Mathf.Clamp01(environmentVolumeSlider.value));
            PlayerPrefs.SetFloat(EffectsVolumeKey, effectsVolumeSlider == null ? 1f : Mathf.Clamp01(effectsVolumeSlider.value));
            PlayerPrefs.SetFloat(GameVoiceVolumeKey, gameVoiceVolumeSlider == null ? 1f : Mathf.Clamp01(gameVoiceVolumeSlider.value));
            PlayerPrefs.SetInt(MicVolumeKey, Mathf.RoundToInt(microphoneVolumeSlider == null ? 0 : microphoneVolumeSlider.value));
            PlayerPrefs.SetInt(PartyVolumeKey, Mathf.RoundToInt(partyVolumeSlider == null ? 0 : partyVolumeSlider.value));
            PlayerPrefs.SetInt(OutputVolumeKey, Mathf.RoundToInt(outputVolumeSlider == null ? 0 : outputVolumeSlider.value));
            PlayerPrefs.SetInt(MicMutedKey, microphoneMuteToggle != null && microphoneMuteToggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt(OutputMutedKey, outputMuteToggle != null && outputMuteToggle.isOn ? 1 : 0);
            SaveSelectedVoiceDevices();
            PlayerPrefs.SetInt(QualityKey, qualityDropdown == null ? QualitySettings.GetQualityLevel() : qualityDropdown.value);
            SaveSelectedResolution();
            PlayerPrefs.SetInt(VSyncKey, vSyncToggle != null && vSyncToggle.isOn ? 1 : 0);
        }

        private void SaveSelectedResolution()
        {
            var index = resolutionDropdown == null ? FindCurrentResolutionIndex() : resolutionDropdown.value;
            if (index < 0 || index >= resolutions.Count)
            {
                Debug.LogWarning($"PHS settings resolution save failed. index={index} count={resolutions.Count}");
                return;
            }

            PlayerPrefs.SetInt(ResolutionWidthKey, resolutions[index].width);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolutions[index].height);
        }

        private static void SetDropdownOptions(TMP_Dropdown dropdown, IReadOnlyList<string> options, int selectedIndex)
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

        private void ApplyVoiceControlValues()
        {
            if (voiceChatSession == null)
            {
                return;
            }

            voiceChatSession.SetInputVolume(Mathf.RoundToInt(microphoneVolumeSlider == null ? 0f : microphoneVolumeSlider.value));
            voiceChatSession.SetPartyVolume(Mathf.RoundToInt(partyVolumeSlider == null ? 0f : partyVolumeSlider.value));
            voiceChatSession.SetOutputVolume(Mathf.RoundToInt(outputVolumeSlider == null ? 0f : outputVolumeSlider.value));
            voiceChatSession.SetInputMuted(microphoneMuteToggle != null && microphoneMuteToggle.isOn);
            voiceChatSession.SetOutputMuted(outputMuteToggle != null && outputMuteToggle.isOn);
        }

        private async System.Threading.Tasks.Task ApplySavedVoiceDevicesAsync()
        {
            if (voiceChatSession == null)
            {
                return;
            }

            var inputDeviceId = PlayerPrefs.GetString(MicDeviceIdKey, string.Empty);
            var outputDeviceId = PlayerPrefs.GetString(OutputDeviceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(inputDeviceId))
            {
                await voiceChatSession.SetInputDeviceByIdAsync(inputDeviceId);
            }

            if (!string.IsNullOrWhiteSpace(outputDeviceId))
            {
                await voiceChatSession.SetOutputDeviceByIdAsync(outputDeviceId);
            }
        }

        private void SaveSelectedVoiceDevices()
        {
            if (voiceChatSession == null)
            {
                return;
            }

            var inputDeviceId = voiceChatSession.GetInputDeviceIdByIndex(
                microphoneDropdown == null ? 0 : microphoneDropdown.value);
            if (!string.IsNullOrWhiteSpace(inputDeviceId))
            {
                PlayerPrefs.SetString(MicDeviceIdKey, inputDeviceId);
            }

            var outputDeviceId = voiceChatSession.GetOutputDeviceIdByIndex(
                outputDeviceDropdown == null ? 0 : outputDeviceDropdown.value);
            if (!string.IsNullOrWhiteSpace(outputDeviceId))
            {
                PlayerPrefs.SetString(OutputDeviceIdKey, outputDeviceId);
            }
        }

        private void UpdateSavedVoiceDeviceSelection()
        {
            if (!hasSnapshot)
            {
                return;
            }

            savedSnapshot.MicrophoneDevice = microphoneDropdown == null ? 0 : microphoneDropdown.value;
            savedSnapshot.OutputDevice = outputDeviceDropdown == null ? 0 : outputDeviceDropdown.value;
            savedSnapshot.MicrophoneDeviceId = voiceChatSession?.GetInputDeviceIdByIndex(savedSnapshot.MicrophoneDevice);
            savedSnapshot.OutputDeviceId = voiceChatSession?.GetOutputDeviceIdByIndex(savedSnapshot.OutputDevice);
        }

        private bool IsSettingsLoadCurrent(int loadVersion)
        {
            return this != null && isActiveAndEnabled && loadVersion == settingsLoadVersion;
        }

        private static string GetQualityDisplayName(string qualityName)
        {
            return qualityName.Trim().ToUpperInvariant() switch
            {
                "MOBILE" => "PERFORMANCE",
                "PC" => "QUALITY",
                var value => value
            };
        }

        private static string FormatSignedLevel(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private struct SettingsSnapshot
        {
            public int ResolutionIndex;
            public int ResolutionWidth;
            public int ResolutionHeight;
            public bool FullScreen;
            public float MasterVolume;
            public float EnvironmentVolume;
            public float EffectsVolume;
            public float GameVoiceVolume;
            public int MicrophoneVolume;
            public int PartyVolume;
            public int OutputVolume;
            public int Quality;
            public bool VSync;
            public int MicrophoneDevice;
            public int OutputDevice;
            public string MicrophoneDeviceId;
            public string OutputDeviceId;
            public bool MicrophoneMuted;
            public bool OutputMuted;
            public Dictionary<string, int> ParticipantVolumes;
        }
    }
}
