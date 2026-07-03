using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkSessionStarter : MonoBehaviour, INetworkSession
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private string connectAddress = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField, Min(1)] private int maxPlayers = 8;

        public bool IsRunning => networkManager != null && networkManager.IsListening;
        public int MaxPlayers => maxPlayers;

        public bool StartHost()
        {
            if (!CanStartSession())
            {
                return false;
            }

            ConfigureConnectionApproval();
            ConfigureTransport();
            return networkManager.StartHost();
        }

        public bool StartClient()
        {
            if (!CanStartSession())
            {
                return false;
            }

            ConfigureTransport();
            return networkManager.StartClient();
        }

        public bool StartServer()
        {
            if (!CanStartSession())
            {
                return false;
            }

            ConfigureConnectionApproval();
            ConfigureTransport();
            return networkManager.StartServer();
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        public void SetAddress(string address)
        {
            connectAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        }

        public void SetPort(ushort sessionPort)
        {
            port = sessionPort == 0 ? (ushort)7777 : sessionPort;
        }

        public void SetMaxPlayers(int sessionMaxPlayers)
        {
            maxPlayers = Mathf.Max(1, sessionMaxPlayers);
        }

        private bool CanStartSession()
        {
            return networkManager != null && !networkManager.IsListening;
        }

        private void ConfigureTransport()
        {
            if (transport == null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
            }

            if (transport != null)
            {
                transport.SetConnectionData(connectAddress, port);
            }
        }

        private void ConfigureConnectionApproval()
        {
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
    }
}
