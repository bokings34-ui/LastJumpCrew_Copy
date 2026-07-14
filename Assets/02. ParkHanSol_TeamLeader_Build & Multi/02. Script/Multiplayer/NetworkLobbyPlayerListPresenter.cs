using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkLobbyPlayerListPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private MultiplayerRoomService roomService;
        [FormerlySerializedAs("roomCodeText")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private PlayerSlotView[] playerSlots;

        private void OnEnable()
        {
            ResolveNetworkManager();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            Refresh();
        }

        private void Subscribe()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientChanged;
            networkManager.OnClientDisconnectCallback -= HandleClientChanged;
            networkManager.OnClientConnectedCallback += HandleClientChanged;
            networkManager.OnClientDisconnectCallback += HandleClientChanged;
        }

        private void Unsubscribe()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientChanged;
            networkManager.OnClientDisconnectCallback -= HandleClientChanged;
        }

        private void HandleClientChanged(ulong clientId)
        {
            Refresh();
        }

        private void Refresh()
        {
            ResolveNetworkManager();

            var clientIds = GetClientIds();
            SetText(playerCountText, $"{clientIds.Count}/8 CREW");
            SetText(roomNameText, GetRoomName());
            SetText(pingText, GetPingLabel(networkManager == null ? null : networkManager.LocalClientId));

            for (var i = 0; i < playerSlots.Length; i++)
            {
                var clientId = i < clientIds.Count ? clientIds[i] : (ulong?)null;
                playerSlots[i].Refresh(clientId, networkManager);
            }
        }

        private List<ulong> GetClientIds()
        {
            var clientIds = new List<ulong>();
            if (networkManager == null || !networkManager.IsListening)
            {
                return clientIds;
            }

            foreach (var clientId in networkManager.ConnectedClientsIds)
            {
                clientIds.Add(clientId);
            }

            clientIds.Sort();
            return clientIds;
        }

        private string BuildPlayerLabel(ulong clientId)
        {
            var role = clientId == NetworkManager.ServerClientId ? "HOST" : "CLIENT";
            var local = networkManager != null && clientId == networkManager.LocalClientId ? " / LOCAL" : string.Empty;
            return $"PLAYER {clientId:00}   {role}{local}";
        }

        private string GetRoomName()
        {
            if (roomService != null && !string.IsNullOrWhiteSpace(roomService.SessionName))
            {
                return $"ROOM  {roomService.SessionName}";
            }

            return "ROOM  UNAVAILABLE";
        }

        private void ResolveNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        [System.Serializable]
        private struct PlayerSlotView
        {
            [SerializeField] private GameObject slotRoot;
            [SerializeField] private TMP_Text nicknameText;
            [SerializeField] private TMP_Text readyStatusText;
            [SerializeField] private TMP_Text pingText;
            [SerializeField] private TMP_Text microphoneText;

            public void Refresh(ulong? clientId, NetworkManager manager)
            {
                if (!clientId.HasValue)
                {
                    SetText(nicknameText, string.Empty);
                    SetText(readyStatusText, string.Empty);
                    SetText(pingText, string.Empty);
                    SetText(microphoneText, string.Empty);
                    slotRoot?.SetActive(false);
                    return;
                }

                slotRoot?.SetActive(true);
                var isLocal = manager != null && clientId.Value == manager.LocalClientId;
                var role = clientId.Value == NetworkManager.ServerClientId ? "HOST" : "CLIENT";
                var local = isLocal ? " / LOCAL" : string.Empty;

                SetText(nicknameText, $"PLAYER {clientId.Value:00}   {role}{local}");
                SetText(readyStatusText, string.Empty);
                SetText(pingText, GetPingLabel(manager, clientId.Value));
                SetText(microphoneText, string.Empty);
            }
        }

        private string GetPingLabel(ulong? clientId)
        {
            return GetPingLabel(networkManager, clientId);
        }

        private static string GetPingLabel(NetworkManager manager, ulong? clientId)
        {
            if (manager == null || !manager.IsListening || !clientId.HasValue || manager.NetworkConfig?.NetworkTransport == null)
            {
                return "-- ms";
            }

            if (manager.IsHost && clientId.Value == manager.LocalClientId)
            {
                return "0 ms";
            }

            var ping = manager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId.Value);
            return $"{ping} ms";
        }
    }
}
