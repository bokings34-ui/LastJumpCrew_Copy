using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class MultiplayerRoomListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Button joinButton;
        [SerializeField] private ParkHanSolLobbySelectionTarget selectionTarget;

        private RoomSessionInfo roomInfo;
        private Action<RoomSessionInfo> joinRequested;

        public void Configure(
            RoomSessionInfo info,
            Action<RoomSessionInfo> onJoinRequested,
            ParkHanSolLobbySelectionIndicator selectionIndicator)
        {
            joinButton.onClick.RemoveListener(HandleJoinClicked);

            roomInfo = info;
            joinRequested = onJoinRequested ?? throw new ArgumentNullException(nameof(onJoinRequested));
            roomNameText.text = info.Name;
            playerCountText.text = $"{info.PlayerCount}/{info.MaxPlayers}";
            lockIcon.SetActive(info.HasPassword);
            selectionTarget.SetIndicator(selectionIndicator);

            joinButton.onClick.AddListener(HandleJoinClicked);
        }

        private void OnDestroy()
        {
            joinButton.onClick.RemoveListener(HandleJoinClicked);
        }

        private void HandleJoinClicked()
        {
            joinRequested.Invoke(roomInfo);
        }
    }
}
