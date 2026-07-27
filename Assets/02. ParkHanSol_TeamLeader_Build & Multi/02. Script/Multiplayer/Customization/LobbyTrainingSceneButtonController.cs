using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyTrainingSceneButtonController : MonoBehaviour
    {
        [SerializeField] private Button trainingButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string tutorialSceneName =
            "PHS_NetworkTutorialScene";

        private readonly INetworkSessionExitService sessionExitService =
            new NetworkSessionExitService();
        private bool isTransitioning;

        private void Awake()
        {
            if (trainingButton == null
                || statusLabel == null
                || string.IsNullOrWhiteSpace(tutorialSceneName))
            {
                Debug.LogError(
                    "PHS_LOBBY_TRAINING_SETUP_FAILED reason=reference_missing",
                    this);
                enabled = false;
                return;
            }

            trainingButton.onClick.AddListener(OpenTraining);
        }

        private void OnDestroy()
        {
            if (trainingButton != null)
            {
                trainingButton.onClick.RemoveListener(OpenTraining);
            }
        }

        private async void OpenTraining()
        {
            if (isTransitioning)
            {
                return;
            }

            isTransitioning = true;
            trainingButton.interactable = false;
            statusLabel.text = "OPENING TRAINING...";
            if (await sessionExitService.LeaveToLobbyAsync(tutorialSceneName))
            {
                return;
            }

            if (this == null)
            {
                return;
            }

            isTransitioning = false;
            trainingButton.interactable = true;
            statusLabel.text = "TRAINING UNAVAILABLE";
            Debug.LogError(
                $"PHS_LOBBY_TRAINING_FAILED scene={tutorialSceneName}",
                this);
        }
    }
}
