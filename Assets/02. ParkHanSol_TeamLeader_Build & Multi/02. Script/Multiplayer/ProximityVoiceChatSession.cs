using System;
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
                VivoxService.Instance.UnmuteInputDevice();
                ActiveChannelName = channelName;
                IsInChannel = true;
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
    }
}
