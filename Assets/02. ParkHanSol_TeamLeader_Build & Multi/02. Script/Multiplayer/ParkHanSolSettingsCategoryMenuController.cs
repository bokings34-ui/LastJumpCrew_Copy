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

        [Header("Category State")]
        [SerializeField] private Color activeCategoryColor =
            new(1f, 0.76f, 0.08f, 1f);
        [SerializeField] private Color inactiveCategoryColor =
            new(0.95f, 0.33f, 0.04f, 1f);

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
            ShowPanel(gameplayPanel, gameplayButton);
        }

        private void ShowGraphics()
        {
            ShowPanel(graphicsPanel, graphicsButton);
        }

        private void ShowAudio()
        {
            ShowPanel(audioPanel, audioButton);
        }

        private void ShowVoice()
        {
            ShowPanel(voicePanel, voiceButton);
        }

        private void ShowControls()
        {
            ShowPanel(controlsPanel, controlsButton);
        }

        private void ShowPanel(GameObject target, Button activeButton)
        {
            SetActive(gameplayPanel, target == gameplayPanel);
            SetActive(graphicsPanel, target == graphicsPanel);
            SetActive(audioPanel, target == audioPanel);
            SetActive(voicePanel, target == voicePanel);
            SetActive(controlsPanel, target == controlsPanel);

            SetCategoryColor(gameplayButton, activeButton == gameplayButton);
            SetCategoryColor(graphicsButton, activeButton == graphicsButton);
            SetCategoryColor(audioButton, activeButton == audioButton);
            SetCategoryColor(voiceButton, activeButton == voiceButton);
            SetCategoryColor(controlsButton, activeButton == controlsButton);
        }

        private void SetCategoryColor(Button button, bool active)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = active
                ? activeCategoryColor
                : inactiveCategoryColor;
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
