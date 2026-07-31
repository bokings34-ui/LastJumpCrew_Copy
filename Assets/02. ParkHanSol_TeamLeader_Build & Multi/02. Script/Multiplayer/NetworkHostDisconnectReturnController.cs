using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkHostDisconnectReturnController : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private MultiplayerRoomService roomService;
        [SerializeField] private string lobbySceneName = "ParkHanSol_LobbyScene";

        private bool observedLocalClient;
        private ulong observedLocalClientId;
        private bool returnRequested;

        private void Awake()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
            }

            if (roomService == null)
            {
                roomService = GetComponent<MultiplayerRoomService>();
            }
        }

        private void OnEnable()
        {
            if (networkManager == null || roomService == null)
            {
                Debug.LogError(
                    $"PHS_HOST_DISCONNECT_RETURN_SETUP_FAILED " +
                    $"network_manager={networkManager != null} room_service={roomService != null}",
                    this);
                enabled = false;
                return;
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnTransportFailure += HandleTransportFailure;
            roomService.UnexpectedSessionEnded += HandleUnexpectedSessionEnded;
            if (networkManager.IsClient && !networkManager.IsServer)
            {
                observedLocalClient = true;
                observedLocalClientId = networkManager.LocalClientId;
            }
        }

        private void OnDisable()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnTransportFailure -= HandleTransportFailure;
            if (roomService != null)
            {
                roomService.UnexpectedSessionEnded -= HandleUnexpectedSessionEnded;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager.IsServer || clientId != networkManager.LocalClientId)
            {
                return;
            }

            observedLocalClient = true;
            observedLocalClientId = clientId;
            returnRequested = false;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId != observedLocalClientId)
            {
                return;
            }

            RequestReturn("client_disconnected");
        }

        private void HandleTransportFailure()
        {
            RequestReturn("transport_failure");
        }

        private void HandleUnexpectedSessionEnded()
        {
            RequestReturn("session_ended");
        }

        private void RequestReturn(string reason)
        {
            if (networkManager.IsServer || !observedLocalClient || returnRequested)
            {
                return;
            }

            returnRequested = true;
            Debug.Log($"PHS_HOST_DISCONNECT_RETURN_REQUESTED reason={reason}", this);
            _ = ReturnToLobbyAsync(reason);
        }

        private async Task ReturnToLobbyAsync(string reason)
        {
            await Task.Yield();
            var exitService = new NetworkSessionExitService();
            if (!await exitService.LeaveToLobbyAsync(lobbySceneName))
            {
                returnRequested = false;
                Debug.LogError(
                    $"PHS_HOST_DISCONNECT_RETURN_FAILED reason={reason} scene={lobbySceneName}",
                    this);
                return;
            }

            Debug.Log($"PHS_HOST_DISCONNECT_RETURN_OK reason={reason} scene={lobbySceneName}");
        }
    }
}
