using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ProximityVoiceChatSession : MonoBehaviour, IVoiceChatSession
    {
        [SerializeField] private string defaultChannelName = "ParkHanSol_TestVoice";
        [SerializeField, Min(1)] private int audibleDistance = 22;
        [SerializeField, Min(1)] private int conversationalDistance = 5;
        [SerializeField, Min(0.1f)] private float fadeIntensity = 1f;
        [SerializeField] private AudioFadeModel fadeModel = AudioFadeModel.InverseByDistance;

        private string requestedChannelName;
        private bool servicesReady;
        private bool joinInProgress;
        private int requestedInputVolume;
        private int requestedOutputVolume;
        private int requestedChannelVolume;
        private bool requestedInputMuted;
        private bool requestedOutputMuted;

        public bool IsInChannel { get; private set; }
        public string ActiveChannelName { get; private set; }

        private void Awake()
        {
            requestedChannelName = NormalizeChannelName(defaultChannelName);
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
                await ApplyChannelVolumeAsync();
                UpdateLocalPosition(localPlayer);
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
                IsInChannel = false;
                ActiveChannelName = string.Empty;
            }
        }

        public void UpdateLocalPosition(GameObject localPlayer)
        {
            if (!IsInChannel || localPlayer == null || string.IsNullOrEmpty(ActiveChannelName))
            {
                return;
            }

            VivoxService.Instance.Set3DPosition(localPlayer, ActiveChannelName);
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
            if (!servicesReady || !IsInChannel || string.IsNullOrEmpty(ActiveChannelName)
                || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return Array.Empty<string>();
            }

            return participants
                .Where(participant => !participant.IsSelf)
                .Select(participant => string.IsNullOrWhiteSpace(participant.DisplayName)
                    ? participant.PlayerId
                    : participant.DisplayName)
                .ToList();
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

        public async void SetInputDeviceByIndex(int index)
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

        public async void SetOutputDeviceByIndex(int index)
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
            if (!servicesReady || !IsInChannel || string.IsNullOrEmpty(ActiveChannelName)
                || !VivoxService.Instance.ActiveChannels.TryGetValue(ActiveChannelName, out var participants))
            {
                return;
            }

            var remoteParticipants = participants.Where(participant => !participant.IsSelf).ToList();
            if (index >= 0 && index < remoteParticipants.Count)
            {
                remoteParticipants[index].SetLocalVolume(value);
            }
        }

        public void SetInputMuted(bool muted)
        {
            requestedInputMuted = muted;
            if (!servicesReady)
            {
                return;
            }

            if (requestedInputMuted)
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

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await VivoxService.Instance.InitializeAsync();
                servicesReady = true;
                ApplyVoiceDeviceSettings();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Vivox service setup failed: {exception.Message}");
                return false;
            }
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
            if (!servicesReady || !IsInChannel || string.IsNullOrEmpty(ActiveChannelName))
            {
                return;
            }

            await VivoxService.Instance.SetChannelVolumeAsync(ActiveChannelName, requestedChannelVolume);
        }
    }
}
