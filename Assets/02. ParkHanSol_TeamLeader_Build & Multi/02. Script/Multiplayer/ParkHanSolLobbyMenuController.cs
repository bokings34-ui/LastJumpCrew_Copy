using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using System.Threading.Tasks;
using LastJumpCrew.SeoBoGyeong;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolLobbyMenuController : MonoBehaviour
    {
        private enum SettingsReturnTarget
        {
            Start,
            Room
        }

        [Header("Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject settingsLeftMenu;
        [SerializeField] private GameObject settingsApplyButton;
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private MultiplayerRoomService roomService;
        [SerializeField] private MultiplayerRoomBrowser roomBrowser;
        [SerializeField] private ProximityVoiceChatSession voiceChatSession;

        [Header("Main Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Lobby Buttons")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private TMP_Text lobbyStatusText;

        [Header("Room Buttons")]
        [SerializeField] private Button roomLeaveButton;
        [SerializeField] private Button roomSettingsButton;
        [SerializeField] private Button roomStartGameButton;
        [SerializeField] private string playSceneName = "ParkHanSol_PlayScene";

        [Header("Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private ParkHanSolGameSettingsController gameSettingsController;

        private SettingsReturnTarget settingsReturnTarget = SettingsReturnTarget.Start;
        private bool ownsLegacyMasterVolumeSlider;

        private void Awake()
        {
            Bind(startButton, ShowLobbySelection);
            Bind(settingsButton, ShowSettingsFromStart);
            Bind(quitButton, QuitGame);
            Bind(createRoomButton, ShowCreateRoom);
            Bind(joinRoomButton, ShowJoinRoom);
            Bind(lobbyBackButton, ShowStart);
            Bind(roomLeaveButton, LeaveRoom);
            Bind(roomSettingsButton, ShowSettingsFromRoom);
            Bind(roomStartGameButton, StartGame);
            Bind(settingsBackButton, CloseSettingsWithoutSave);

            ownsLegacyMasterVolumeSlider =
                masterVolumeSlider != null
                && gameSettingsController == null;
            if (ownsLegacyMasterVolumeSlider)
            {
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
                masterVolumeSlider.value = AudioListener.volume;
            }

            ShowStartImmediate();
        }

        private void Start()
        {
            ShowStart();
            StartCoroutine(RunCommandLineAutomation());
        }

        private void Update()
        {
            if (settingsPanel != null &&
                settingsPanel.activeSelf &&
                Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseSettingsWithoutSave();
            }

            RefreshRoomActionAvailability();
        }

        private void OnDestroy()
        {
            Unbind(startButton, ShowLobbySelection);
            Unbind(settingsButton, ShowSettingsFromStart);
            Unbind(quitButton, QuitGame);
            Unbind(createRoomButton, ShowCreateRoom);
            Unbind(joinRoomButton, ShowJoinRoom);
            Unbind(lobbyBackButton, ShowStart);
            Unbind(roomLeaveButton, LeaveRoom);
            Unbind(roomSettingsButton, ShowSettingsFromRoom);
            Unbind(roomStartGameButton, StartGame);
            Unbind(settingsBackButton, CloseSettingsWithoutSave);

            if (ownsLegacyMasterVolumeSlider)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            }
        }

        private void ShowStart()
        {
            SetStartPanels(false);
        }

        private void ShowStartImmediate()
        {
            SetStartPanels(true);
        }

        private void SetStartPanels(bool immediate)
        {
            settingsReturnTarget = SettingsReturnTarget.Start;
            SetPanel(startPanel, true, immediate);
            SetPanel(lobbyPanel, false, immediate);
            SetPanel(roomPanel, false, immediate);
            SetPanel(settingsPanel, false, immediate);
            SetPanel(settingsLeftMenu, false, immediate);
            SetPanel(settingsApplyButton, false, immediate);
            SetPanel(sessionPanel, false, immediate);
        }

        private void ShowSettingsFromStart()
        {
            settingsReturnTarget = SettingsReturnTarget.Start;
            ShowSettings();
        }

        private void ShowSettingsFromRoom()
        {
            settingsReturnTarget = SettingsReturnTarget.Room;
            ShowSettings();
        }

        private void ShowSettings(bool immediate = false)
        {
            SetPanel(startPanel, false, immediate);
            SetPanel(lobbyPanel, false, immediate);
            SetPanel(roomPanel, false, immediate);
            SetPanel(settingsPanel, true, immediate);
            SetPanel(settingsLeftMenu, true, immediate);
            SetPanel(settingsApplyButton, true, immediate);
            SetPanel(sessionPanel, false, immediate);
        }

        private void CloseSettingsWithoutSave()
        {
            var returnTarget = settingsReturnTarget;
            gameSettingsController?.CancelSettings();
            settingsReturnTarget = SettingsReturnTarget.Start;

            if (returnTarget == SettingsReturnTarget.Room &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                ShowRoom();
                return;
            }

            ShowStart();
        }

        private void RefreshRoomActionAvailability()
        {
            if (roomStartGameButton == null)
            {
                return;
            }

            var manager = NetworkManager.Singleton;
            var shouldShow = roomPanel != null &&
                roomPanel.activeSelf &&
                manager != null &&
                manager.IsHost;
            if (roomStartGameButton.gameObject.activeSelf != shouldShow)
            {
                roomStartGameButton.gameObject.SetActive(shouldShow);
            }
        }

        public void ShowLobbySelection()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, true);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(sessionPanel, false);
            roomBrowser?.ShowActionPanel();
        }

        private void ShowCreateRoom()
        {
            if (roomBrowser == null)
            {
                Debug.LogError("PHS_ROOM_UI_MISSING roomBrowser");
                SetLobbyStatus("ROOM UI NOT READY");
                return;
            }

            roomBrowser.ShowCreateRoomPanel();
        }

        private async Task<bool> CreateRoomAsync(string roomName = "Last Jump Crew Room", int maxPlayers = 8)
        {
            if (roomService == null)
            {
                SetLobbyStatus("SESSION NOT READY");
                Debug.LogError("PHS_ROOM_SERVICE_MISSING create");
                return false;
            }

            SetLobbyStatus("CREATING ROOM");
            if (!await roomService.CreateRoomAsync(roomName, maxPlayers, string.Empty))
            {
                SetLobbyStatus("CREATE FAILED");
                return false;
            }

            SetLobbyStatus("ROOM READY");
            return true;
        }

        private async void ShowJoinRoom()
        {
            if (roomBrowser == null)
            {
                SetLobbyStatus("ROOM UI NOT READY");
                Debug.LogError("PHS_ROOM_UI_MISSING roomBrowser");
                return;
            }

            await roomBrowser.ShowRoomListAsync();
        }

        public void ShowRoom()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, true);
            SetPanel(settingsPanel, false);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(sessionPanel, false);
            SetLocalGameplayInput(false);
            ConfigureVoiceChannel();
            Debug.Log($"PHS_ONLINE_ROOM scene={SceneManager.GetActiveScene().name} clients={GetConnectedClientCount()}");
        }

        private async void LeaveRoom()
        {
            if (voiceChatSession != null)
            {
                await voiceChatSession.LeaveAsync();
            }

            if (roomService == null)
            {
                Debug.LogError("PHS_ROOM_SERVICE_MISSING leave");
                return;
            }

            if (!await roomService.LeaveRoomAsync())
            {
                SetLobbyStatus("LEAVE FAILED");
                return;
            }

            SetLocalGameplayInput(false);
            ShowLobbySelection();
        }

        private void ConfigureVoiceChannel()
        {
            if (voiceChatSession == null || roomService == null || string.IsNullOrWhiteSpace(roomService.SessionCode))
            {
                Debug.LogError("PHS_ROOM_VOICE_CHANNEL_FAILED missing_reference_or_session_code");
                return;
            }

            voiceChatSession.SetVoiceChannel(roomService.SessionCode);
        }

        private void StartGame()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                if (networkManager.IsServer)
                {
                    if (!TryBeginGameRun())
                    {
                        return;
                    }

                    networkManager.SceneManager.LoadScene(playSceneName, LoadSceneMode.Single);
                }

                return;
            }

            if (!TryBeginGameRun())
            {
                return;
            }

            SceneManager.LoadScene(playSceneName);
        }

        private static bool TryBeginGameRun()
        {
            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                Debug.LogError("PHS_GAME_START_FAILED reason=game_core_missing");
                return false;
            }

            var commands = gameCore.Services.Get<IGameCommands>();
            var state = gameCore.Services.Get<IGameStateProvider>();
            if (commands == null || state == null)
            {
                Debug.LogError("PHS_GAME_START_FAILED reason=economy_services_missing");
                return false;
            }

            commands.StartGame();
            if (state.Phase != GamePhase.ZoneSelect)
            {
                Debug.LogError($"PHS_GAME_START_FAILED reason=phase_{state.Phase}");
                return false;
            }

            Debug.Log($"PHS_GAME_RUN_STARTED phase={state.Phase} clearedZones={state.ClearedZoneCount}");
            return true;
        }

        private static void SetMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
        }

        private void SetLobbyStatus(string message)
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text = message;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void SetPanel(GameObject panel, bool active, bool immediate = false)
        {
            if (panel != null && panel.TryGetComponent<ParkHanSolLobbyPanelTransition>(out var transition))
            {
                transition.SetVisible(active, immediate);
                return;
            }

            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private static void SetLocalGameplayInput(bool active)
        {
            foreach (var player in FindObjectsOfType<NetworkPlayerController>())
            {
                player.SetGameplayInputEnabled(active);
            }

            if (!active)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private IEnumerator RunCommandLineAutomation()
        {
            var args = Environment.GetCommandLineArgs();
            if (HasCommandLineFlag(args, "-phsAutoHost"))
            {
                yield return null;
                ShowLobbySelection();
                Debug.Log("PHS_AUTO_HOST_BEGIN");
                _ = CreateRoomFromCommandLineAsync();
                yield break;
            }

            var joinCode = GetCommandLineValue(args, "-phsAutoJoin");
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                yield break;
            }

            yield return null;
            ShowLobbySelection();

            Debug.Log($"PHS_AUTO_JOIN_BEGIN code={joinCode}");
            _ = JoinRoomFromCommandLineAsync(joinCode);
        }

        private async Task CreateRoomFromCommandLineAsync()
        {
            var created = await CreateRoomAsync();
            var joinCode = roomService == null ? string.Empty : roomService.SessionCode;
            Debug.Log(created
                ? $"PHS_AUTO_HOST_READY code={joinCode}"
                : "PHS_AUTO_HOST_FAILED");

            if (created && HasCommandLineFlag(Environment.GetCommandLineArgs(), "-phsAutoStartGame"))
            {
                StartCoroutine(StartGameFromCommandLineWhenReady());
            }
        }

        private async Task JoinRoomFromCommandLineAsync(string joinCode)
        {
            if (roomService == null)
            {
                Debug.LogError("PHS_AUTO_JOIN_FAILED room_service_missing");
                return;
            }

            if (!await roomService.JoinRoomByCodeAsync(joinCode, string.Empty))
            {
                Debug.LogError($"PHS_AUTO_JOIN_FAILED code={joinCode}");
            }
        }

        private IEnumerator StartGameFromCommandLineWhenReady()
        {
            var args = Environment.GetCommandLineArgs();
            var requiredClients = Mathf.Max(1, GetCommandLineInt(args, "-phsAutoStartClients", 4));
            var timeoutSeconds = Mathf.Max(1, GetCommandLineInt(args, "-phsAutoStartTimeout", 60));
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                var connectedCount = GetConnectedClientCount();
                Debug.Log($"PHS_AUTO_ROOM_COUNT clients={connectedCount}/{requiredClients}");
                if (connectedCount >= requiredClients)
                {
                    Debug.Log($"PHS_AUTO_START_GAME clients={connectedCount}");
                    StartGame();
                    yield break;
                }

                yield return new WaitForSecondsRealtime(1f);
            }

            Debug.LogWarning($"PHS_AUTO_START_GAME_TIMEOUT clients={GetConnectedClientCount()}/{requiredClients}");
        }

        private static bool HasCommandLineFlag(string[] args, string key)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetCommandLineValue(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static int GetCommandLineInt(string[] args, string key, int fallback)
        {
            var value = GetCommandLineValue(args, key);
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static int GetConnectedClientCount()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager == null ? 0 : networkManager.ConnectedClientsIds.Count;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
