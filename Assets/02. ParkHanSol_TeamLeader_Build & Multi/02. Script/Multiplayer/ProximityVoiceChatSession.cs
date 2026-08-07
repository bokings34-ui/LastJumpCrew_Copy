using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ProximityVoiceChatSession :
        MonoBehaviour,
        IVoiceChatSession,
        IVoiceCommunicationSuppression
    {
        [SerializeField] private string defaultChannelName = "ParkHanSol_TestVoice";
        [SerializeField, Min(1)] private int audibleDistance = 22;
        [SerializeField, Min(1)] private int conversationalDistance = 5;
        [SerializeField, Min(0.1f)] private float fadeIntensity = 1f;
        [SerializeField] private AudioFadeModel fadeModel = AudioFadeModel.InverseByDistance;
        [SerializeField, Min(0.1f)] private float positionUpdateRetryDelay = 1f;

        private const float PositionUpdateStartDelay = 1f;

        private string requestedChannelName;
        private bool servicesReady;
        private bool joinInProgress;
        private int requestedInputVolume;
        private int requestedOutputVolume;
        private int requestedChannelVolume;
        private bool requestedInputMuted;
        private bool eventInputSuppressed;
        private bool requestedOutputMuted;
        private bool warnedMissingActiveChannel;
        private bool warnedMissingSelfParticipant;
        private float nextAllowedPositionUpdateTime;
        private Task<bool> servicesReadyTask;
        private readonly SemaphoreSlim inputDeviceChangeLock = new(1, 1);
        private readonly SemaphoreSlim outputDeviceChangeLock = new(1, 1);
        private readonly List<VivoxParticipant> trackedParticipants = new();

        public bool IsInChannel { get; private set; }
        public string ActiveChannelName { get; private set; }
        public static ProximityVoiceChatSession ActiveSession { get; private set; }
        public static event Action<ProximityVoiceChatSession> ActiveSessionChanged;
        public event Action<IReadOnlyList<string>> SpeakingParticipantsChanged;

        private void Awake()
        {
            requestedChannelName = NormalizeChannelName(defaultChannelName);
        }

        private void OnEnable()
        {
            ActiveSession = this;
            ActiveSessionChanged?.Invoke(this);
        }

        public void SetVoiceChannel(string channelName)
        {
            requestedChannelName = NormalizeChannelName(channelName);
        }

        public async Task<bool> JoinLocalPlayerIfReadyAsync()
        {
            var localPlayer = NetworkManager.Singleton == null
                ? null
                : NetworkManager.Singleton.LocalClient.PlayerObject;

            return localPlayer != null && await JoinForLocalPlayerAsync(localPlayer.gameObject);
        }

        public async Task<bool> JoinForLocalPlayerAsync(GameObject localPlayer)
        {
            if (localPlayer == null || joinInProgress)
            {
                return false;
            }

            joinInProgress = true;

            try
            {
                if (!await EnsureServicesReadyAsync())
                {
                    return false;
                }

                var channelName = NormalizeChannelName(requestedChannelName);
                if (IsInChannel && string.Equals(ActiveChannelName, channelName, StringComparison.Ordinal))
                {
                    UpdateLocalPosition(localPlayer);
                    return true;
                }

                if (IsInChannel)
                {
                    await LeaveAsync();
                }

                var properties = new Channel3DProperties(
                    audibleDistance,
                    Mathf.Min(conversationalDistance, audibleDistance),
                    fadeIntensity,
                    fadeModel);

                await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, properties);
                ApplyVoiceDeviceSettings();
                ActiveChannelName = channelName;
                IsInChannel = true;
                warnedMissingActiveChannel = false;
                warnedMissingSelfParticipant = false;
                nextAllowedPositionUpdateTime = Time.realtimeSinceStartup + PositionUpdateStartDelay;
                BindVoiceParticipantEvents();
                await ApplyChannelVolumeAsync();
                UpdateLocalPosition(localPlayer);
                Debug.Log($"PHS_VOICE_CHANNEL_READY channel={ActiveChannelName}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Proximity voice join failed: {exception.Message}");
                IsInChannel = false;
                return false;
            }
            finally
            {
                joinInProgress = false;
            }
        }

        public async Task LeaveAsync()
        {
            if (!IsInChannel || string.IsNullOrEmpty(ActiveChannelName))
            {
                return;
            }

            try
            {
                await VivoxService.Instance.LeaveChannelAsync(ActiveChannelName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Proximity voice leave failed: {exception.Message}");
            }
            finally
            {
                UnbindVoiceParticipantEvents();
                SpeakingParticipantsChanged?.Invoke(Array.Empty<string>());
                IsInChannel = false;
                ActiveChannelName = string.Empty;
                warnedMissingActiveChannel = false;
                warnedMissingSelfParticipant = false;
                nextAllowedPositionUpdateTime = 0f;
            }
        }

        public void UpdateLocalPosition(GameObject localPlayer)
        {
            if (localPlayer == null || !CanUpdateVoicePosition())
            {
                return;
            }

            if (Time.realtimeSinceStartup < nextAllowedPositionUpdateTime)
            {
                return;
            }

            try
            {
                VivoxService.Instance.Set3DPosition(localPlayer, ActiveChannelName);
            }
            catch (Exception exception)
            {
                nextAllowedPositionUpdateTime = Time.realtimeSinceStartup + positionUpdateRetryDelay;
                Debug.LogWarning(BuildPositionUpdateFailureMessage(localPlayer, exception));
            }
        }

        public async Task<bool> PrepareVoiceSettingsAsync()
        {
            return await EnsureServicesReadyAsync();
        }

        public IReadOnlyList<string> GetInputDeviceNames()
        {
            return servicesReady
                ? VivoxService.Instance.AvailableInputDevices.Select(device => device.DeviceName).ToList()
                : Array.Empty<string>();
        }

        public IReadOnlyList<string> GetOutputDeviceNames()
        {
            return servicesReady
                ? VivoxService.Instance.AvailableOutputDevices.Select(device => device.DeviceName).ToList()
                : Array.Empty<string>();
        }

        public IReadOnlyList<string> GetRemoteParticipantNames()
        {
            if (!HasActiveVivoxChannel())
            {
                return Array.Empty<string>();
            }

            try
            {
                if (VivoxService.Instance == null
                    || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
                {
                    Debug.LogWarning("PHS_VOICE_ACTIVE_CHANNEL_READ_FAILED Vivox service or active channel is missing.");
                    return Array.Empty<string>();
                }

                return participants
                    .Where(participant => participant != null && !participant.IsSelf)
                    .Select(GetParticipantDisplayName)
                    .Where(displayName => !string.IsNullOrWhiteSpace(displayName))
                    .ToList();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PHS_VOICE_REMOTE_PARTICIPANTS_READ_FAILED {exception.Message}");
                return Array.Empty<string>();
            }
        }

        public IReadOnlyList<string> GetSpeakingParticipantNames()
        {
            if (!HasActiveVivoxChannel())
            {
                return Array.Empty<string>();
            }

            try
            {
                if (VivoxService.Instance == null
                    || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
                {
                    Debug.LogWarning("PHS_VOICE_ACTIVE_CHANNEL_READ_FAILED Vivox service or active channel is missing.");
                    return Array.Empty<string>();
                }

                var speakingNames = new List<string>();
                foreach (var participant in participants)
                {
                    if (!TryReadParticipantSpeech(participant, out var isSpeaking) || !isSpeaking)
                    {
                        continue;
                    }

                    var displayName = GetParticipantDisplayName(participant);
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        speakingNames.Add(displayName);
                    }
                }

                return speakingNames;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PHS_VOICE_SPEAKING_PARTICIPANTS_READ_FAILED {exception.Message}");
                return Array.Empty<string>();
            }
        }

        public int GetActiveInputDeviceIndex()
        {
            if (!servicesReady || VivoxService.Instance.ActiveInputDevice == null)
            {
                return 0;
            }

            var devices = VivoxService.Instance.AvailableInputDevices;
            for (var i = 0; i < devices.Count; i++)
            {
                if (devices[i].DeviceID == VivoxService.Instance.ActiveInputDevice.DeviceID)
                {
                    return i;
                }
            }

            return 0;
        }

        public int GetActiveOutputDeviceIndex()
        {
            if (!servicesReady || VivoxService.Instance.ActiveOutputDevice == null)
            {
                return 0;
            }

            var devices = VivoxService.Instance.AvailableOutputDevices;
            for (var i = 0; i < devices.Count; i++)
            {
                if (devices[i].DeviceID == VivoxService.Instance.ActiveOutputDevice.DeviceID)
                {
                    return i;
                }
            }

            return 0;
        }

        public async Task SetInputDeviceByIndexAsync(int index)
        {
            await inputDeviceChangeLock.WaitAsync();
            try
            {
                if (!await EnsureServicesReadyAsync())
                {
                    return;
                }

                var devices = VivoxService.Instance.AvailableInputDevices;
                if (index >= 0 && index < devices.Count)
                {
                    await VivoxService.Instance.SetActiveInputDeviceAsync(devices[index]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox input device change failed: {exception.Message}");
            }
            finally
            {
                inputDeviceChangeLock.Release();
            }
        }

        public async Task SetOutputDeviceByIndexAsync(int index)
        {
            await outputDeviceChangeLock.WaitAsync();
            try
            {
                if (!await EnsureServicesReadyAsync())
                {
                    return;
                }

                var devices = VivoxService.Instance.AvailableOutputDevices;
                if (index >= 0 && index < devices.Count)
                {
                    await VivoxService.Instance.SetActiveOutputDeviceAsync(devices[index]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox output device change failed: {exception.Message}");
            }
            finally
            {
                outputDeviceChangeLock.Release();
            }
        }

        public string GetInputDeviceIdByIndex(int index)
        {
            if (!servicesReady)
            {
                return string.Empty;
            }

            var devices = VivoxService.Instance.AvailableInputDevices;
            return index >= 0 && index < devices.Count ? devices[index].DeviceID : string.Empty;
        }

        public string GetOutputDeviceIdByIndex(int index)
        {
            if (!servicesReady)
            {
                return string.Empty;
            }

            var devices = VivoxService.Instance.AvailableOutputDevices;
            return index >= 0 && index < devices.Count ? devices[index].DeviceID : string.Empty;
        }

        public async Task<bool> SetInputDeviceByIdAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            await inputDeviceChangeLock.WaitAsync();
            try
            {
                if (!await EnsureServicesReadyAsync())
                {
                    return false;
                }

                var device = VivoxService.Instance.AvailableInputDevices
                    .FirstOrDefault(value => string.Equals(value.DeviceID, deviceId, StringComparison.Ordinal));
                if (device == null)
                {
                    return false;
                }

                await VivoxService.Instance.SetActiveInputDeviceAsync(device);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox saved input device restore failed: {exception.Message}");
                return false;
            }
            finally
            {
                inputDeviceChangeLock.Release();
            }
        }

        public async Task<bool> SetOutputDeviceByIdAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            await outputDeviceChangeLock.WaitAsync();
            try
            {
                if (!await EnsureServicesReadyAsync())
                {
                    return false;
                }

                var device = VivoxService.Instance.AvailableOutputDevices
                    .FirstOrDefault(value => string.Equals(value.DeviceID, deviceId, StringComparison.Ordinal));
                if (device == null)
                {
                    return false;
                }

                await VivoxService.Instance.SetActiveOutputDeviceAsync(device);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox saved output device restore failed: {exception.Message}");
                return false;
            }
            finally
            {
                outputDeviceChangeLock.Release();
            }
        }

        public void SetInputVolume(int value)
        {
            requestedInputVolume = Mathf.Clamp(value, -50, 50);
            if (servicesReady)
            {
                VivoxService.Instance.SetInputDeviceVolume(requestedInputVolume);
            }
        }

        public void SetOutputVolume(int value)
        {
            requestedOutputVolume = Mathf.Clamp(value, -50, 50);
            if (servicesReady)
            {
                VivoxService.Instance.SetOutputDeviceVolume(requestedOutputVolume);
            }
        }

        public async void SetPartyVolume(int value)
        {
            requestedChannelVolume = Mathf.Clamp(value, -50, 50);
            await ApplyChannelVolumeAsync();
        }

        public void SetRemoteParticipantVolumeByIndex(int index, int value)
        {
            if (TryGetRemoteParticipantByIndex(index, out var participant))
            {
                participant.SetLocalVolume(value);
            }
        }

        public bool TryGetRemoteParticipantVolumeByIndex(int index, out int volume)
        {
            if (TryGetRemoteParticipantByIndex(index, out var participant))
            {
                volume = participant.LocalVolume;
                return true;
            }

            volume = 0;
            return false;
        }

        public Dictionary<string, int> CaptureRemoteParticipantVolumes()
        {
            var volumes = new Dictionary<string, int>();
            if (!HasActiveVivoxChannel()
                || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return volumes;
            }

            foreach (var participant in participants.Where(value => !value.IsSelf))
            {
                volumes[participant.PlayerId] = participant.LocalVolume;
            }

            return volumes;
        }

        public void RestoreRemoteParticipantVolumes(IReadOnlyDictionary<string, int> volumes)
        {
            if (volumes == null ||
                !HasActiveVivoxChannel() ||
                !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return;
            }

            foreach (var participant in participants.Where(value => !value.IsSelf))
            {
                if (volumes.TryGetValue(participant.PlayerId, out var volume))
                {
                    participant.SetLocalVolume(volume);
                }
            }
        }

        public void SetInputMuted(bool muted)
        {
            requestedInputMuted = muted;
            ApplyInputMuteState();
        }

        public void SetEventInputSuppressed(bool suppressed)
        {
            eventInputSuppressed = suppressed;
            ApplyInputMuteState();
        }

        private void ApplyInputMuteState()
        {
            if (!servicesReady)
            {
                return;
            }

            if (requestedInputMuted || eventInputSuppressed)
            {
                VivoxService.Instance.MuteInputDevice();
            }
            else
            {
                VivoxService.Instance.UnmuteInputDevice();
            }
        }

        public void SetOutputMuted(bool muted)
        {
            requestedOutputMuted = muted;
            if (!servicesReady)
            {
                return;
            }

            if (requestedOutputMuted)
            {
                VivoxService.Instance.MuteOutputDevice();
            }
            else
            {
                VivoxService.Instance.UnmuteOutputDevice();
            }
        }

        private async Task<bool> EnsureServicesReadyAsync()
        {
            if (servicesReady)
            {
                return true;
            }

            if (servicesReadyTask == null)
            {
                servicesReadyTask = InitializeServicesAsync();
            }

            var pendingTask = servicesReadyTask;
            var ready = await pendingTask;
            if (!ready && ReferenceEquals(servicesReadyTask, pendingTask))
            {
                servicesReadyTask = null;
            }

            return ready;
        }

        private async Task<bool> InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync(BuildInitializationOptions());
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await VivoxService.Instance.InitializeAsync();
                servicesReady = true;
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAddedToChannel;
                VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAddedToChannel;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemovedFromChannel;
                VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemovedFromChannel;
                ApplyVoiceDeviceSettings();
                Debug.Log("PHS_VOICE_SERVICE_READY");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox service setup failed: {exception.Message}");
                return false;
            }
        }

        private static InitializationOptions BuildInitializationOptions()
        {
            var options = new InitializationOptions();
            var profile = GetCommandLineValue(Environment.GetCommandLineArgs(), "-phsProfile");
            if (!string.IsNullOrWhiteSpace(profile))
            {
                options.SetProfile(profile);
                Debug.Log($"PHS_SERVICES_PROFILE profile={profile}");
            }

            return options;
        }

        private static string GetCommandLineValue(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private string NormalizeChannelName(string channelName)
        {
            var value = string.IsNullOrWhiteSpace(channelName) ? defaultChannelName : channelName;
            return value.Trim().Replace(" ", "_");
        }

        private void ApplyVoiceDeviceSettings()
        {
            SetInputVolume(requestedInputVolume);
            SetOutputVolume(requestedOutputVolume);
            SetInputMuted(requestedInputMuted);
            SetOutputMuted(requestedOutputMuted);
        }

        private async Task ApplyChannelVolumeAsync()
        {
            if (!HasActiveVivoxChannel())
            {
                return;
            }

            await VivoxService.Instance.SetChannelVolumeAsync(ActiveChannelName, requestedChannelVolume);
        }

        private bool HasActiveVivoxChannel()
        {
            if (!servicesReady || !IsInChannel || string.IsNullOrEmpty(ActiveChannelName))
            {
                return false;
            }

            if (VivoxService.Instance != null
                && VivoxService.Instance.ActiveChannels.ContainsKey(ActiveChannelName))
            {
                warnedMissingActiveChannel = false;
                return true;
            }

            if (!warnedMissingActiveChannel)
            {
                Debug.LogWarning($"PHS_VOICE_ACTIVE_CHANNEL_PENDING channel={ActiveChannelName}");
                warnedMissingActiveChannel = true;
            }

            return false;
        }

        private bool TryGetRemoteParticipantByIndex(int index, out VivoxParticipant participant)
        {
            participant = null;
            if (!HasActiveVivoxChannel()
                || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return false;
            }

            var remoteParticipants = participants.Where(value => !value.IsSelf).ToList();
            if (index < 0 || index >= remoteParticipants.Count)
            {
                return false;
            }

            participant = remoteParticipants[index];
            return participant != null;
        }

        private bool CanUpdateVoicePosition()
        {
            if (!HasActiveVivoxChannel())
            {
                return false;
            }

            if (HasSelfParticipant())
            {
                warnedMissingSelfParticipant = false;
                return true;
            }

            nextAllowedPositionUpdateTime = Time.realtimeSinceStartup + positionUpdateRetryDelay;
            if (!warnedMissingSelfParticipant)
            {
                Debug.LogWarning($"PHS_VOICE_POSITION_WAITING_FOR_SELF_PARTICIPANT channel={ActiveChannelName}");
                warnedMissingSelfParticipant = true;
            }

            return false;
        }

        private bool HasSelfParticipant()
        {
            return VivoxService.Instance != null
                && VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants)
                && participants.Any(participant => participant != null && participant.IsSelf);
        }

        private string BuildPositionUpdateFailureMessage(GameObject localPlayer, Exception exception)
        {
            var participantCount = 0;
            var hasSelfParticipant = false;
            if (VivoxService.Instance != null
                && VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                participantCount = participants.Count;
                hasSelfParticipant = participants.Any(participant => participant != null && participant.IsSelf);
            }

            return "PHS_VOICE_POSITION_UPDATE_FAILED "
                + $"channel={ActiveChannelName} "
                + $"player={localPlayer.name} "
                + $"participantCount={participantCount} "
                + $"hasSelfParticipant={hasSelfParticipant} "
                + exception.Message;
        }

        private void OnDestroy()
        {
            UnbindVoiceParticipantEvents();

            if (servicesReady)
            {
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAddedToChannel;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemovedFromChannel;
            }

            if (ActiveSession == this)
            {
                ActiveSession = null;
                ActiveSessionChanged?.Invoke(null);
            }
        }

        private void OnParticipantAddedToChannel(VivoxParticipant participant)
        {
            if (!IsCurrentChannelParticipant(participant))
            {
                return;
            }

            BindParticipant(participant);
            RefreshSpeakingParticipants();
        }

        private void OnParticipantRemovedFromChannel(VivoxParticipant participant)
        {
            UnbindParticipant(participant);
            RefreshSpeakingParticipants();
        }

        private void BindVoiceParticipantEvents()
        {
            UnbindVoiceParticipantEvents();

            if (!servicesReady || string.IsNullOrEmpty(ActiveChannelName)
                || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return;
            }

            foreach (var participant in participants)
            {
                BindParticipant(participant);
            }

            RefreshSpeakingParticipants();
        }

        private void UnbindVoiceParticipantEvents()
        {
            for (var i = 0; i < trackedParticipants.Count; i++)
            {
                trackedParticipants[i].ParticipantSpeechDetected -= RefreshSpeakingParticipants;
            }

            trackedParticipants.Clear();
        }

        private void BindParticipant(VivoxParticipant participant)
        {
            if (participant == null || trackedParticipants.Contains(participant))
            {
                return;
            }

            participant.ParticipantSpeechDetected += RefreshSpeakingParticipants;
            trackedParticipants.Add(participant);
        }

        private void UnbindParticipant(VivoxParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            participant.ParticipantSpeechDetected -= RefreshSpeakingParticipants;
            trackedParticipants.Remove(participant);
        }

        private bool IsCurrentChannelParticipant(VivoxParticipant participant)
        {
            if (participant == null || !IsInChannel)
            {
                return false;
            }

            try
            {
                return string.Equals(participant.ChannelName, ActiveChannelName, StringComparison.Ordinal);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PHS_VOICE_PARTICIPANT_CHANNEL_READ_FAILED {exception.Message}");
                return false;
            }
        }

        private void RefreshSpeakingParticipants()
        {
            SpeakingParticipantsChanged?.Invoke(GetSpeakingParticipantNames());
        }

        private static string GetParticipantDisplayName(VivoxParticipant participant)
        {
            if (participant == null)
            {
                return string.Empty;
            }

            try
            {
                return string.IsNullOrWhiteSpace(participant.DisplayName)
                    ? participant.PlayerId
                    : participant.DisplayName;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PHS_VOICE_PARTICIPANT_NAME_READ_FAILED {exception.Message}");
                return string.Empty;
            }
        }

        private static bool TryReadParticipantSpeech(VivoxParticipant participant, out bool isSpeaking)
        {
            isSpeaking = false;
            if (participant == null)
            {
                return false;
            }

            try
            {
                isSpeaking = participant.SpeechDetected;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PHS_VOICE_PARTICIPANT_SPEECH_READ_FAILED {exception.Message}");
                return false;
            }
        }
    }
}
