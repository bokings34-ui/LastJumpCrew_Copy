using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkLobbyPlayerListPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private RelaySessionConnector relayConnector;
        [SerializeField] private TMP_Text roomCodeText;
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
            SetText(roomCodeText, GetRoomCode());
            SetText(pingText, networkManager != null && networkManager.IsListening ? "20 ms" : "-- ms");

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

        private string GetRoomCode()
        {
            if (relayConnector != null && !string.IsNullOrWhiteSpace(relayConnector.JoinCode))
            {
                return $"CODE  {relayConnector.JoinCode}";
            }

            return "CODE  LOCAL";
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
            [SerializeField] private TMP_Text nicknameText;
            [SerializeField] private TMP_Text readyStatusText;
            [SerializeField] private TMP_Text pingText;
            [SerializeField] private TMP_Text microphoneText;

            public void Refresh(ulong? clientId, NetworkManager manager)
            {
                if (!clientId.HasValue)
                {
                    SetText(nicknameText, "EMPTY SLOT");
                    SetText(readyStatusText, "--");
                    SetText(pingText, "-- ms");
                    SetText(microphoneText, "MIC --");
                    return;
                }

                var isLocal = manager != null && clientId.Value == manager.LocalClientId;
                var role = clientId.Value == NetworkManager.ServerClientId ? "HOST" : "CLIENT";
                var local = isLocal ? " / LOCAL" : string.Empty;

                SetText(nicknameText, $"PLAYER {clientId.Value:00}   {role}{local}");
                SetText(readyStatusText, "WAIT");
                SetText(pingText, manager != null && manager.IsListening ? "20 ms" : "-- ms");
                SetText(microphoneText, "MIC ON");
            }
        }
    }
}
