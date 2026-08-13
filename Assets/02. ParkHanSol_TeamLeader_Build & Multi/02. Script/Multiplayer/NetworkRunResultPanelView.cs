using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkRunResultPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statisticsText;
        [SerializeField] private Button restartRunButton;
        [SerializeField] private TMP_Text restartRunButtonText;
        [SerializeField] private Button returnToLobbyButton;

        private string baseStatisticsText;

        public Button RestartRunButton => restartRunButton;
        public Button ReturnToLobbyButton => returnToLobbyButton;

        public bool HasRequiredReferences =>
            titleText != null
            && statisticsText != null
            && restartRunButton != null
            && restartRunButtonText != null
            && returnToLobbyButton != null;

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(true);
            GetComponent<Canvas>().enabled = visible;
            GetComponent<GraphicRaycaster>().enabled = visible;
        }

        public void SetResult(
            NetworkRunPhase phase,
            int clearedZones,
            int completedShopCycles,
            int partyCredits)
        {
            if (titleText == null || statisticsText == null)
            {
                Debug.LogError(
                    $"PHS_RUN_RESULT_UI_FAILED reason=text_reference_missing panel={name}",
                    this);
                return;
            }

            titleText.text = phase == NetworkRunPhase.Clear
                ? "RUN CLEAR"
                : "GAME OVER";
            baseStatisticsText =
                $"ZONES CLEARED   {clearedZones}\n" +
                $"SHOP CYCLES     {completedShopCycles}\n" +
                $"PARTY CREDITS   ${partyCredits}";
            statisticsText.text = baseStatisticsText;
        }

        public void SetRestartReady()
        {
            SetRestartPresentation(true, "RESTART RUN", string.Empty);
        }

        public void SetRestartHostOnly()
        {
            SetRestartPresentation(false, "HOST ONLY", "HOST ONLY");
        }

        public void SetRestartPending()
        {
            SetRestartPresentation(false, "RESTARTING...", "RESTARTING RUN");
        }

        public void SetRestartFailed(string reason)
        {
            SetRestartPresentation(false, "RESTART FAILED", $"RESTART FAILED: {reason}");
        }

        private void SetRestartPresentation(
            bool interactable,
            string buttonText,
            string statusText)
        {
            if (restartRunButton == null
                || restartRunButtonText == null
                || statisticsText == null)
            {
                Debug.LogError(
                    $"PHS_RUN_RESULT_RESTART_UI_FAILED reason=reference_missing panel={name}",
                    this);
                return;
            }

            restartRunButton.interactable = interactable;
            restartRunButtonText.text = buttonText;
            statisticsText.text = string.IsNullOrWhiteSpace(statusText)
                ? baseStatisticsText
                : $"{baseStatisticsText}\n\n{statusText}";
        }
    }
}
