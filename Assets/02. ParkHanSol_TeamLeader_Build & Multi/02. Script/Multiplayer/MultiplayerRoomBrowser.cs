using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class MultiplayerRoomBrowser : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerRoomService roomService;
        [SerializeField] private ParkHanSolLobbyMenuController lobbyMenuController;

        [Header("Panels")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject passwordPanel;

        [Header("Room List")]
        [SerializeField] private Transform roomListContent;
        [SerializeField] private MultiplayerRoomListItem entryPrefab;

        [Header("Create Room")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private Toggle passwordToggle;
        [SerializeField] private TMP_InputField createPasswordInput;
        [SerializeField] private Button createConfirmButton;
        [SerializeField] private Button createCancelButton;

        [Header("Room List Actions")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button roomListBackButton;

        [Header("Password Join")]
        [SerializeField] private TMP_InputField joinPasswordInput;
        [SerializeField] private Button passwordConfirmButton;
        [SerializeField] private Button passwordCancelButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        private readonly List<MultiplayerRoomListItem> spawnedEntries = new();
        private RoomSessionInfo selectedRoom;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            roomService.RoomsChanged += RenderRooms;
            roomService.SessionJoined += HandleSessionJoined;
            roomService.OperationFailed += HandleOperationFailed;

            createConfirmButton.onClick.AddListener(CreateRoom);
            createCancelButton.onClick.AddListener(ShowActionPanel);
            refreshButton.onClick.AddListener(RefreshRooms);
            roomListBackButton.onClick.AddListener(ShowActionPanel);
            passwordConfirmButton.onClick.AddListener(JoinSelectedRoomWithPassword);
            passwordCancelButton.onClick.AddListener(ShowRoomListPanel);
        }

        private void OnDestroy()
        {
            if (roomService != null)
            {
                roomService.RoomsChanged -= RenderRooms;
                roomService.SessionJoined -= HandleSessionJoined;
                roomService.OperationFailed -= HandleOperationFailed;
            }

            if (createConfirmButton != null)
            {
                createConfirmButton.onClick.RemoveListener(CreateRoom);
            }

            if (createCancelButton != null)
            {
                createCancelButton.onClick.RemoveListener(ShowActionPanel);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(RefreshRooms);
            }

            if (roomListBackButton != null)
            {
                roomListBackButton.onClick.RemoveListener(ShowActionPanel);
            }

            if (passwordConfirmButton != null)
            {
                passwordConfirmButton.onClick.RemoveListener(JoinSelectedRoomWithPassword);
            }

            if (passwordCancelButton != null)
            {
                passwordCancelButton.onClick.RemoveListener(ShowRoomListPanel);
            }

            DestroyEntries();
        }

        public void ShowCreateRoomPanel()
        {
            SetPanel(actionPanel, false);
            SetPanel(createRoomPanel, true);
            SetPanel(roomListPanel, false);
            SetPanel(passwordPanel, false);
            SetStatus(string.Empty);
        }

        public async Task ShowRoomListAsync()
        {
            ShowRoomListPanel();
            SetStatus("LOADING ROOMS");

            if (await roomService.RefreshRoomsAsync())
            {
                SetStatus(roomService.Rooms.Count == 0 ? "NO ROOMS" : string.Empty);
            }
        }

        private bool ValidateReferences()
        {
            var missing = new List<string>();
            Require(roomService, nameof(roomService), missing);
            Require(lobbyMenuController, nameof(lobbyMenuController), missing);
            Require(actionPanel, nameof(actionPanel), missing);
            Require(createRoomPanel, nameof(createRoomPanel), missing);
            Require(roomListPanel, nameof(roomListPanel), missing);
            Require(passwordPanel, nameof(passwordPanel), missing);
            Require(roomListContent, nameof(roomListContent), missing);
            Require(entryPrefab, nameof(entryPrefab), missing);
            Require(roomNameInput, nameof(roomNameInput), missing);
            Require(maxPlayersInput, nameof(maxPlayersInput), missing);
            Require(passwordToggle, nameof(passwordToggle), missing);
            Require(createPasswordInput, nameof(createPasswordInput), missing);
            Require(createConfirmButton, nameof(createConfirmButton), missing);
            Require(createCancelButton, nameof(createCancelButton), missing);
            Require(refreshButton, nameof(refreshButton), missing);
            Require(roomListBackButton, nameof(roomListBackButton), missing);
            Require(joinPasswordInput, nameof(joinPasswordInput), missing);
            Require(passwordConfirmButton, nameof(passwordConfirmButton), missing);
            Require(passwordCancelButton, nameof(passwordCancelButton), missing);
            Require(statusText, nameof(statusText), missing);

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError($"{nameof(MultiplayerRoomBrowser)} missing Inspector references: {string.Join(", ", missing)}", this);
            return false;
        }

        private static void Require(UnityEngine.Object reference, string fieldName, ICollection<string> missing)
        {
            if (reference == null)
            {
                missing.Add(fieldName);
            }
        }

        private async void CreateRoom()
        {
            if (!int.TryParse(maxPlayersInput.text, out var maxPlayers))
            {
                SetStatus("INVALID MAX PLAYERS");
                Debug.LogError($"{nameof(MultiplayerRoomBrowser)} invalid max players: {maxPlayersInput.text}", this);
                return;
            }

            var password = passwordToggle.isOn ? createPasswordInput.text : string.Empty;
            SetStatus("CREATING ROOM");
            await roomService.CreateRoomAsync(roomNameInput.text, maxPlayers, password);
        }

        private async void RefreshRooms()
        {
            SetStatus("LOADING ROOMS");
            if (await roomService.RefreshRoomsAsync())
            {
                SetStatus(roomService.Rooms.Count == 0 ? "NO ROOMS" : string.Empty);
            }
        }

        private void RenderRooms()
        {
            DestroyEntries();

            foreach (var room in roomService.Rooms)
            {
                var entry = Instantiate(entryPrefab, roomListContent);
                entry.Configure(room, SelectRoom);
                spawnedEntries.Add(entry);
            }
        }

        private void SelectRoom(RoomSessionInfo room)
        {
            selectedRoom = room;

            if (room.HasPassword)
            {
                SetPanel(roomListPanel, false);
                SetPanel(passwordPanel, true);
                joinPasswordInput.text = string.Empty;
                joinPasswordInput.Select();
                SetStatus(string.Empty);
                return;
            }

            JoinSelectedRoom(string.Empty);
        }

        private void JoinSelectedRoomWithPassword()
        {
            JoinSelectedRoom(joinPasswordInput.text);
        }

        private async void JoinSelectedRoom(string password)
        {
            if (selectedRoom == null)
            {
                SetStatus("ROOM NOT SELECTED");
                Debug.LogError($"{nameof(MultiplayerRoomBrowser)} join requested without a selected room", this);
                return;
            }

            SetStatus("JOINING ROOM");
            await roomService.JoinRoomAsync(selectedRoom.Id, password);
        }

        private void HandleSessionJoined()
        {
            SetStatus(string.Empty);
            lobbyMenuController.ShowRoom();
        }

        private void HandleOperationFailed(string message)
        {
            SetStatus(message);
            Debug.LogError($"{nameof(MultiplayerRoomBrowser)} operation failed: {message}", this);
        }

        public void ShowActionPanel()
        {
            selectedRoom = null;
            SetPanel(actionPanel, true);
            SetPanel(createRoomPanel, false);
            SetPanel(roomListPanel, false);
            SetPanel(passwordPanel, false);
            SetStatus(string.Empty);
        }

        private void ShowRoomListPanel()
        {
            selectedRoom = null;
            SetPanel(actionPanel, false);
            SetPanel(createRoomPanel, false);
            SetPanel(roomListPanel, true);
            SetPanel(passwordPanel, false);
            SetStatus(string.Empty);
        }

        private void DestroyEntries()
        {
            foreach (var entry in spawnedEntries)
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
            }

            spawnedEntries.Clear();
        }

        private void SetStatus(string message)
        {
            statusText.text = message;
        }

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
