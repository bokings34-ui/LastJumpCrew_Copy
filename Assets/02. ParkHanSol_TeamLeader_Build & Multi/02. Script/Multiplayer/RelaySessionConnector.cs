using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class RelaySessionConnector : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private NetworkSessionStarter sessionStarter;
        [SerializeField, Min(1)] private int maxPlayers = 8;
        [SerializeField] private string relayConnectionType = "dtls";
        [SerializeField] private string joinCode;

        public string JoinCode => joinCode;

        public async Task<bool> StartRelayHostAsync()
        {
            if (!await EnsureServicesReadyAsync())
            {
                return false;
            }

            EnsureReferences();
            var maxPeerConnections = Mathf.Max(1, maxPlayers - 1);
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxPeerConnections);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            ApplyHostAllocation(allocation);

            if (sessionStarter != null)
            {
                sessionStarter.SetMaxPlayers(maxPlayers);
            }

            ConfigureConnectionApproval();
            return networkManager != null && networkManager.StartHost();
        }

        public async Task<bool> StartRelayClientAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !await EnsureServicesReadyAsync())
            {
                return false;
            }

            EnsureReferences();
            joinCode = code.Trim().ToUpperInvariant();
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            ApplyJoinAllocation(allocation);
            return networkManager != null && networkManager.StartClient();
        }

        public void SetJoinCode(string code)
        {
            joinCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
        }

        public void SetMaxPlayers(int sessionMaxPlayers)
        {
            maxPlayers = Mathf.Max(1, sessionMaxPlayers);
        }

        private async Task<bool> EnsureServicesReadyAsync()
        {
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

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Relay service setup failed: {exception.Message}");
                return false;
            }
        }

        private void EnsureReferences()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
            }

            if (transport == null && networkManager != null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
            }
        }

        private void ConfigureConnectionApproval()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.ConnectionApprovalCallback += ApproveConnection;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var connectedCount = networkManager == null ? 0 : networkManager.ConnectedClientsIds.Count;
            response.Approved = connectedCount < maxPlayers;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "Session is full.";
        }

        private void ApplyHostAllocation(Allocation allocation)
        {
            var relayServerData = CreateRelayServerData(
                allocation.ServerEndpoints,
                allocation.RelayServer,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key);
            transport.SetRelayServerData(relayServerData);
        }

        private void ApplyJoinAllocation(JoinAllocation allocation)
        {
            var relayServerData = CreateRelayServerData(
                allocation.ServerEndpoints,
                allocation.RelayServer,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.HostConnectionData,
                allocation.Key);
            transport.SetRelayServerData(relayServerData);
        }

        private RelayServerData CreateRelayServerData(
            System.Collections.Generic.IReadOnlyCollection<RelayServerEndpoint> endpoints,
            RelayServer fallbackServer,
            byte[] allocationIdBytes,
            byte[] connectionData,
            byte[] hostConnectionData,
            byte[] key)
        {
            var endpoint = endpoints?.FirstOrDefault(value =>
                string.Equals(value.ConnectionType, relayConnectionType, StringComparison.OrdinalIgnoreCase));

            var host = endpoint?.Host ?? fallbackServer.IpV4;
            var port = endpoint == null ? (ushort)fallbackServer.Port : (ushort)endpoint.Port;
            var isSecure = string.Equals(relayConnectionType, "dtls", StringComparison.OrdinalIgnoreCase)
                || string.Equals(relayConnectionType, "wss", StringComparison.OrdinalIgnoreCase);
            var isWebSocket = string.Equals(relayConnectionType, "wss", StringComparison.OrdinalIgnoreCase);

            return new RelayServerData(
                host,
                port,
                allocationIdBytes,
                connectionData,
                hostConnectionData,
                key,
                isSecure,
                isWebSocket);
        }
    }
}
