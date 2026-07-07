using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolSettingsCategoryMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button gameplayButton;
        [SerializeField] private Button graphicsButton;
        [SerializeField] private Button audioButton;
        [SerializeField] private Button voiceButton;
        [SerializeField] private Button controlsButton;

        [Header("Panels")]
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject graphicsPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject voicePanel;
        [SerializeField] private GameObject controlsPanel;

        private void Awake()
        {
            Bind(gameplayButton, ShowGameplay);
            Bind(graphicsButton, ShowGraphics);
            Bind(audioButton, ShowAudio);
            Bind(voiceButton, ShowVoice);
            Bind(controlsButton, ShowControls);
        }

        private void OnEnable()
        {
            ShowGraphics();
        }

        private void OnDestroy()
        {
            Unbind(gameplayButton, ShowGameplay);
            Unbind(graphicsButton, ShowGraphics);
            Unbind(audioButton, ShowAudio);
            Unbind(voiceButton, ShowVoice);
            Unbind(controlsButton, ShowControls);
        }

        private void ShowGameplay()
        {
            ShowPanel(gameplayPanel);
        }

        private void ShowGraphics()
        {
            ShowPanel(graphicsPanel);
        }

        private void ShowAudio()
        {
            ShowPanel(audioPanel);
        }

        private void ShowVoice()
        {
            ShowPanel(voicePanel);
        }

        private void ShowControls()
        {
            ShowPanel(controlsPanel);
        }

        private void ShowPanel(GameObject target)
        {
            SetActive(gameplayPanel, target == gameplayPanel);
            SetActive(graphicsPanel, target == graphicsPanel);
            SetActive(audioPanel, target == audioPanel);
            SetActive(voicePanel, target == voicePanel);
            SetActive(controlsPanel, target == controlsPanel);
        }

        private static void SetActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
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
