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
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                Debug.LogError("PHS_HOST_DISCONNECT_RETURN_SETUP_FAILED reason=network_manager_missing", this);
                enabled = false;
                return;
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
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
            if (networkManager.IsServer
                || !observedLocalClient
                || clientId != observedLocalClientId
                || returnRequested)
            {
                return;
            }

            returnRequested = true;
            _ = ReturnToLobbyAsync();
        }

        private async Task ReturnToLobbyAsync()
        {
            await Task.Yield();
            var exitService = new NetworkSessionExitService();
            if (!await exitService.LeaveToLobbyAsync(lobbySceneName))
            {
                returnRequested = false;
                Debug.LogError(
                    $"PHS_HOST_DISCONNECT_RETURN_FAILED scene={lobbySceneName}",
                    this);
                return;
            }

            Debug.Log($"PHS_HOST_DISCONNECT_RETURN_OK scene={lobbySceneName}");
        }
    }
}
