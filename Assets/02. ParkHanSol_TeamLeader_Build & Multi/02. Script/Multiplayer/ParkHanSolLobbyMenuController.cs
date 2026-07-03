using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolLobbyMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private NetworkSessionPanel sessionPanelController;

        [Header("Main Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Lobby Buttons")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private InputField lobbyJoinCodeInput;
        [SerializeField] private Text lobbyStatusText;

        [Header("Room Buttons")]
        [SerializeField] private Button roomLeaveButton;
        [SerializeField] private Button roomSettingsButton;
        [SerializeField] private Button roomStartGameButton;
        [SerializeField] private string playSceneName = "ParkHanSol_PlayScene";

        [Header("Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Button settingsBackButton;

        private void Awake()
        {
            Bind(startButton, ShowLobbySelection);
            Bind(settingsButton, ShowSettings);
            Bind(quitButton, QuitGame);
            Bind(createRoomButton, ShowCreateRoom);
            Bind(joinRoomButton, ShowJoinRoom);
            Bind(lobbyBackButton, ShowStart);
            Bind(roomLeaveButton, LeaveRoom);
            Bind(roomSettingsButton, ShowSettings);
            Bind(roomStartGameButton, StartGame);
            Bind(settingsBackButton, ShowStart);

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

        private void OnDestroy()
        {
            Unbind(startButton, ShowLobbySelection);
            Unbind(settingsButton, ShowSettings);
            Unbind(quitButton, QuitGame);
            Unbind(createRoomButton, ShowCreateRoom);
            Unbind(joinRoomButton, ShowJoinRoom);
            Unbind(lobbyBackButton, ShowStart);
            Unbind(roomLeaveButton, LeaveRoom);
            Unbind(roomSettingsButton, ShowSettings);
            Unbind(roomStartGameButton, StartGame);
            Unbind(settingsBackButton, ShowStart);

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
            SetPanel(sessionPanel, false);
        }

        private void ShowSettings()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, false);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, true);
            SetPanel(sessionPanel, false);
        }

        public void ShowLobbySelection()
        {
            SetPanel(startPanel, false);
            SetPanel(lobbyPanel, true);
            SetPanel(roomPanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(sessionPanel, false);
        }

        private async void ShowCreateRoom()
        {
            if (sessionPanelController == null)
            {
                SetLobbyStatus("SESSION NOT READY");
                return;
            }

            SetLobbyStatus("CREATING ROOM");
            if (!await sessionPanelController.StartRelayHostSessionAsync())
            {
                SetLobbyStatus("CREATE FAILED");
            }
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
