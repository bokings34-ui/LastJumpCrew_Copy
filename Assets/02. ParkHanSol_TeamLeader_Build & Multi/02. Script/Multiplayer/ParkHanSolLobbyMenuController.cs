using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolLobbyMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject settingsLeftMenu;
        [SerializeField] private GameObject settingsApplyButton;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private NetworkSessionPanel sessionPanelController;

        [Header("Main Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button quitButton;

        [Header("Lobby Buttons")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private TMP_InputField lobbyJoinCodeInput;
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

        [Header("Shop")]
        [SerializeField] private Button shopBackButton;

        private void Awake()
        {
            Bind(startButton, ShowLobbySelection);
            Bind(settingsButton, ShowSettings);
            Bind(shopButton, ShowShop);
            Bind(quitButton, QuitGame);
            Bind(createRoomButton, ShowCreateRoom);
            Bind(joinRoomButton, ShowJoinRoom);
            Bind(lobbyBackButton, ShowStart);
            Bind(roomLeaveButton, LeaveRoom);
            Bind(roomSettingsButton, ShowSettings);
            Bind(roomStartGameButton, StartGame);
            Bind(settingsBackButton, CloseSettingsWithoutSave);
            Bind(shopBackButton, ShowStart);

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
                masterVolumeSlider.value = AudioListener.volume;
            }

            ShowStart();
        }

        private void Start()
        {
            ShowStart();
            StartCoroutine(RunCommandLineAutomation());
        }

        private void Update()
        {
            if (settingsPanel != null && settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSettingsWithoutSave();
            }
        }

        private void OnDestroy()
        {
            Unbind(startButton, ShowLobbySelection);
            Unbind(settingsButton, ShowSettings);
            Unbind(shopButton, ShowShop);
            Unbind(quitButton, QuitGame);
            Unbind(createRoomButton, ShowCreateRoom);
            Unbind(joinRoomButton, ShowJoinRoom);
            Unbind(lobbyBackButton, ShowStart);
            Unbind(roomLeaveButton, LeaveRoom);
            Unbind(roomSettingsButton, ShowSettings);
            Unbind(roomStartGameButton, StartGame);
            Unbind(settingsBackButton, CloseSettingsWithoutSave);
            Unbind(shopBackButton, ShowStart);

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            }
        }

        private void ShowStart()
        {
            SetPanel(startPanel, true);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(shopPanel, false);
            SetPanel(sessionPanel, false);
        }

        private void ShowSettings()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, true);
            SetPanel(settingsLeftMenu, true);
            SetPanel(settingsApplyButton, true);
            SetPanel(shopPanel, false);
            SetPanel(sessionPanel, false);
        }

        private void ShowShop()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, true);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(shopPanel, true);
            SetPanel(sessionPanel, false);
            ShowOnlySettingsChild(shopPanel);
        }

        private void CloseSettingsWithoutSave()
        {
            gameSettingsController?.CancelSettings();
            ShowStart();
        }

        public void ShowLobbySelection()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, true);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(shopPanel, false);
            SetPanel(sessionPanel, false);
        }

        private async void ShowCreateRoom()
        {
            await CreateRoomAsync();
        }

        private async Task<bool> CreateRoomAsync()
        {
            if (sessionPanelController == null)
            {
                SetLobbyStatus("SESSION NOT READY");
                return false;
            }

            SetLobbyStatus("CREATING ROOM");
            if (!await sessionPanelController.StartRelayHostSessionAsync())
            {
                SetLobbyStatus("CREATE FAILED");
                return false;
            }

            SetLobbyStatus("ROOM READY");
            return true;
        }

        private async void ShowJoinRoom()
        {
            if (sessionPanelController == null)
            {
                SetLobbyStatus("SESSION NOT READY");
                return;
            }

            var joinCode = lobbyJoinCodeInput == null ? string.Empty : lobbyJoinCodeInput.text;
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SetLobbyStatus("ENTER ROOM CODE");
                return;
            }

            SetLobbyStatus("JOINING ROOM");
            if (!await sessionPanelController.StartRelayClientSessionAsync(joinCode))
            {
                SetLobbyStatus("JOIN FAILED");
            }
        }

        public void ShowRoom()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, true);
            SetPanel(settingsPanel, false);
            SetPanel(settingsLeftMenu, false);
            SetPanel(settingsApplyButton, false);
            SetPanel(shopPanel, false);
            SetPanel(sessionPanel, false);
            SetLocalGameplayInput(false);
            Debug.Log($"PHS_ONLINE_ROOM scene={SceneManager.GetActiveScene().name} clients={GetConnectedClientCount()}");
        }

        private void LeaveRoom()
        {
            sessionPanelController?.ShutdownSession();
            SetLocalGameplayInput(false);
            ShowLobbySelection();
        }

        private void StartGame()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                if (networkManager.IsServer)
                {
                    networkManager.SceneManager.LoadScene(playSceneName, LoadSceneMode.Single);
                }

                return;
            }

            SceneManager.LoadScene(playSceneName);
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

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private void ShowOnlySettingsChild(GameObject targetPanel)
        {
            if (settingsPanel == null || targetPanel == null)
            {
                return;
            }

            for (var i = 0; i < settingsPanel.transform.childCount; i++)
            {
                var child = settingsPanel.transform.GetChild(i).gameObject;
                if (child == targetPanel || child.transform.IsChildOf(targetPanel.transform))
                {
                    continue;
                }

                child.SetActive(false);
            }

            targetPanel.SetActive(true);
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

            if (lobbyJoinCodeInput != null)
            {
                lobbyJoinCodeInput.text = joinCode;
            }

            Debug.Log($"PHS_AUTO_JOIN_BEGIN code={joinCode}");
            ShowJoinRoom();
        }

        private async Task CreateRoomFromCommandLineAsync()
        {
            var created = await CreateRoomAsync();
            var relayConnector = FindObjectOfType<RelaySessionConnector>();
            var joinCode = relayConnector == null ? string.Empty : relayConnector.JoinCode;
            Debug.Log(created
                ? $"PHS_AUTO_HOST_READY code={joinCode}"
                : "PHS_AUTO_HOST_FAILED");

            if (created && HasCommandLineFlag(Environment.GetCommandLineArgs(), "-phsAutoStartGame"))
            {
                StartCoroutine(StartGameFromCommandLineWhenReady());
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
