using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkSessionPanel : MonoBehaviour
    {
        [SerializeField] private NetworkSessionStarter sessionStarter;
        [SerializeField] private RelaySessionConnector relayConnector;
        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
        [SerializeField] private ParkHanSolLobbyMenuController lobbyMenuController;
        [SerializeField] private string lanVoiceChannelName = "ParkHanSol_TestVoice";
        [SerializeField] private InputField addressInput;
        [SerializeField] private InputField portInput;
        [SerializeField] private InputField maxPlayersInput;
        [SerializeField] private InputField joinCodeInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private Button relayHostButton;
        [SerializeField] private Button relayClientButton;
        [SerializeField] private Button shutdownButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Text statusText;

        private void Awake()
        {
            Bind(hostButton, StartHost);
            Bind(clientButton, StartClient);
            Bind(serverButton, StartServer);
            Bind(relayHostButton, StartRelayHost);
            Bind(relayClientButton, StartRelayClient);
            Bind(shutdownButton, Shutdown);
            Bind(backButton, BackToLobbySelection);
            RefreshStatus("Idle");
        }

        private void OnDestroy()
        {
            Unbind(hostButton, StartHost);
            Unbind(clientButton, StartClient);
            Unbind(serverButton, StartServer);
            Unbind(relayHostButton, StartRelayHost);
            Unbind(relayClientButton, StartRelayClient);
            Unbind(shutdownButton, Shutdown);
            Unbind(backButton, BackToLobbySelection);
        }

        private void Update()
        {
            if (sessionStarter != null && sessionStarter.IsRunning)
            {
                RefreshStatus("Running");
            }
        }

        private void StartHost()
        {
            StartHostSession();
        }

        private void StartClient()
        {
            StartClientSession();
        }

        private void StartServer()
        {
            ApplyInputs();
            var started = sessionStarter != null && sessionStarter.StartServer();
            RefreshStatus(started ? "Server started" : "Server failed");
            ShowRoomIfStarted(started);
        }

        private async void StartRelayHost()
        {
            await StartRelayHostSessionAsync();
        }

        private async void StartRelayClient()
        {
            var code = joinCodeInput == null ? string.Empty : joinCodeInput.text;
            await StartRelayClientSessionAsync(code);
        }

        public bool StartHostSession()
        {
            ApplyInputs();
            var started = sessionStarter != null && sessionStarter.StartHost();
            RefreshStatus(started ? "Host started" : "Host failed");
            ShowRoomIfStarted(started);
            StartVoiceIfRunning(started, lanVoiceChannelName);
            return started;
        }

        public bool StartClientSession()
        {
            ApplyInputs();
            var started = sessionStarter != null && sessionStarter.StartClient();
            RefreshStatus(started ? "Client started" : "Client failed");
            ShowRoomIfStarted(started);
            StartVoiceIfRunning(started, lanVoiceChannelName);
            return started;
        }

        public async System.Threading.Tasks.Task<bool> StartRelayHostSessionAsync()
        {
            ApplyInputs();
            RefreshStatus("Relay host starting");
            var started = relayConnector != null && await relayConnector.StartRelayHostAsync();
            RefreshStatus(started ? $"Relay host: {relayConnector.JoinCode}" : "Relay host failed");
            ShowRoomIfStarted(started);
            StartVoiceIfRunning(started, relayConnector == null ? string.Empty : relayConnector.JoinCode);
            return started;
        }

        public async System.Threading.Tasks.Task<bool> StartRelayClientSessionAsync(string code)
        {
            ApplyInputs();
            RefreshStatus("Relay client starting");
            if (joinCodeInput != null)
            {
                joinCodeInput.text = code;
            }

            var started = relayConnector != null && await relayConnector.StartRelayClientAsync(code);
            RefreshStatus(started ? "Relay client started" : "Relay client failed");
            ShowRoomIfStarted(started);
            StartVoiceIfRunning(started, code);
            return started;
        }

        public async void ShutdownSession()
        {
            if (voiceChatSession != null)
            {
                await voiceChatSession.LeaveAsync();
            }

            sessionStarter?.Shutdown();
            RefreshStatus("Shutdown");
        }

        private void Shutdown()
        {
            ShutdownSession();
        }

        private void BackToLobbySelection()
        {
            if (sessionStarter != null && sessionStarter.IsRunning)
            {
                ShutdownSession();
            }

            lobbyMenuController?.ShowLobbySelection();
        }

        private void ApplyInputs()
        {
            if (sessionStarter == null)
            {
                return;
            }

            if (addressInput != null)
            {
                sessionStarter.SetAddress(addressInput.text);
            }

            if (portInput != null && ushort.TryParse(portInput.text, out var parsedPort))
            {
                sessionStarter.SetPort(parsedPort);
            }

            if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out var maxPlayers))
            {
                sessionStarter.SetMaxPlayers(maxPlayers);
                relayConnector?.SetMaxPlayers(maxPlayers);
            }

            if (joinCodeInput != null)
            {
                relayConnector?.SetJoinCode(joinCodeInput.text);
            }
        }

        private async void StartVoiceIfRunning(bool sessionStarted, string channelName)
        {
            if (!sessionStarted || voiceChatSession == null)
            {
                return;
            }

            voiceChatSession.SetVoiceChannel(channelName);
            var voiceReady = await voiceChatSession.JoinLocalPlayerIfReadyAsync();
            RefreshStatus(voiceReady ? $"Voice: {voiceChatSession.ActiveChannelName}" : "Voice waiting for player");
        }

        private void RefreshStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void ShowRoomIfStarted(bool started)
        {
            if (started)
            {
                lobbyMenuController?.ShowRoom();
            }
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
