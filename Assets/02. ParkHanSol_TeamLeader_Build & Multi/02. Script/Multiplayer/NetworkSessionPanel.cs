using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkSessionPanel : MonoBehaviour
    {
        [SerializeField] private NetworkSessionStarter sessionStarter;
        [SerializeField] private RelaySessionConnector relayConnector;
        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
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
        [SerializeField] private Text statusText;

        private void Awake()
        {
            Bind(hostButton, StartHost);
            Bind(clientButton, StartClient);
            Bind(serverButton, StartServer);
            Bind(relayHostButton, StartRelayHost);
            Bind(relayClientButton, StartRelayClient);
            Bind(shutdownButton, Shutdown);
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
            ApplyInputs();
            var started = sessionStarter != null && sessionStarter.StartHost();
            RefreshStatus(started ? "Host started" : "Host failed");
            StartVoiceIfRunning(started, lanVoiceChannelName);
        }

        private void StartClient()
        {
            ApplyInputs();
            var started = sessionStarter != null && sessionStarter.StartClient();
            RefreshStatus(started ? "Client started" : "Client failed");
            StartVoiceIfRunning(started, lanVoiceChannelName);
        }

        private void StartServer()
        {
            ApplyInputs();
            RefreshStatus(sessionStarter != null && sessionStarter.StartServer() ? "Server started" : "Server failed");
        }

        private async void StartRelayHost()
        {
            ApplyInputs();
            RefreshStatus("Relay host starting");
            var started = relayConnector != null && await relayConnector.StartRelayHostAsync();
            RefreshStatus(started ? $"Relay host: {relayConnector.JoinCode}" : "Relay host failed");
            StartVoiceIfRunning(started, relayConnector == null ? string.Empty : relayConnector.JoinCode);
        }

        private async void StartRelayClient()
        {
            ApplyInputs();
            RefreshStatus("Relay client starting");
            var code = joinCodeInput == null ? string.Empty : joinCodeInput.text;
            var started = relayConnector != null && await relayConnector.StartRelayClientAsync(code);
            RefreshStatus(started ? "Relay client started" : "Relay client failed");
            StartVoiceIfRunning(started, code);
        }

        private async void Shutdown()
        {
            if (voiceChatSession != null)
            {
                await voiceChatSession.LeaveAsync();
            }

            sessionStarter?.Shutdown();
            RefreshStatus("Shutdown");
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
