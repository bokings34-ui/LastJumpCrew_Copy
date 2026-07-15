using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolPauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private Button exitGameButton;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TMP_Text mouseSensitivityValueText;
        [Header("Pause Presentation")]
        [SerializeField] private Image dimBackgroundImage;
        [SerializeField] private CanvasGroup menuCanvasGroup;
        [SerializeField] private RectTransform menuCard;
        [SerializeField, Min(0.01f)] private float dimFadeDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float menuShowDuration = 0.22f;
        [SerializeField] private string lobbySceneName = "ParkHanSol_LobbyScene";

        private bool isOpen;
        private Vector2 menuCardShownPosition;
        private Sequence openSequence;

        private void Awake()
        {
            Bind(resumeButton, CloseMenu);
            Bind(optionsButton, OpenOptions);
            Bind(optionsBackButton, CloseOptions);
            Bind(exitGameButton, ExitToLobby);
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.minValue = NetworkPlayerController.MinimumMouseSensitivity;
                mouseSensitivitySlider.maxValue = NetworkPlayerController.MaximumMouseSensitivity;
                mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
            }

            SetPanels(false, false);
        }

        private void OnDestroy()
        {
            Unbind(resumeButton, CloseMenu);
            Unbind(optionsButton, OpenOptions);
            Unbind(optionsBackButton, CloseOptions);
            Unbind(exitGameButton, ExitToLobby);
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.RemoveListener(SetMouseSensitivity);
            }
        }

        private void OnDisable()
        {
            KillOpenSequence();
            SetPlayerInputBlocked(false);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (!isOpen)
            {
                OpenMenu();
                return;
            }

            if (optionsPanel != null && optionsPanel.activeSelf)
            {
                CloseOptions();
                return;
            }

            CloseMenu();
        }

        public void OpenMenu()
        {
            isOpen = true;
            SetPlayerInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetPanels(true, false);
            PlayOpenAnimation();
        }

        public void CloseMenu()
        {
            isOpen = false;
            SetPanels(false, false);
            SetPlayerInputBlocked(false);
            PlayerPrefs.Save();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OpenOptions()
        {
            if (!isOpen)
            {
                return;
            }

            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.SetValueWithoutNotify(NetworkPlayerController.GetSavedMouseSensitivity());
            }

            RefreshMouseSensitivityLabel();
            SetPanels(true, true);
        }

        public void CloseOptions()
        {
            SetPanels(true, false);
        }

        public void SetMouseSensitivity(float value)
        {
            NetworkPlayerController.SaveMouseSensitivity(value);
            RefreshMouseSensitivityLabel();
        }

        private void RefreshMouseSensitivityLabel()
        {
            if (mouseSensitivityValueText != null)
            {
                mouseSensitivityValueText.text = $"{NetworkPlayerController.GetSavedMouseSensitivity():0.00}";
            }
        }

        private void SetPanels(bool pauseActive, bool optionsActive)
        {
            if (pausePanel != null) pausePanel.SetActive(pauseActive);
            if (optionsPanel != null) optionsPanel.SetActive(optionsActive);
        }

        private async void ExitToLobby()
        {
            if (!isOpen)
            {
                return;
            }

            SetExitButtonInteractable(false);
            if (!await LeaveLocalSessionAsync())
            {
                SetExitButtonInteractable(true);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                Debug.LogError($"PHS_PAUSE_EXIT_FAILED reason=lobby_scene_not_in_build scene={lobbySceneName}");
                SetExitButtonInteractable(true);
                return;
            }

            SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }

        private async System.Threading.Tasks.Task<bool> LeaveLocalSessionAsync()
        {
            var networkManager = NetworkManager.Singleton;
            var roomService = networkManager == null
                ? null
                : networkManager.GetComponent<MultiplayerRoomService>();
            if (roomService != null && roomService.IsActive)
            {
                if (!await roomService.LeaveRoomAsync())
                {
                    Debug.LogError("PHS_PAUSE_EXIT_FAILED reason=room_leave_failed");
                    return false;
                }
            }

            if (ProximityVoiceChatSession.ActiveSession != null)
            {
                await ProximityVoiceChatSession.ActiveSession.LeaveAsync();
            }

            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            if (networkManager != null)
            {
                Destroy(networkManager.gameObject);
            }

            return true;
        }

        private void PlayOpenAnimation()
        {
            KillOpenSequence();
            if (menuCard != null)
            {
                menuCardShownPosition = menuCard.anchoredPosition;
                menuCard.anchoredPosition = menuCardShownPosition + new Vector2(0f, -28f);
                menuCard.localScale = Vector3.one * 0.92f;
            }

            if (dimBackgroundImage != null)
            {
                var dimColor = dimBackgroundImage.color;
                dimColor.a = 0f;
                dimBackgroundImage.color = dimColor;
            }

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.interactable = false;
                menuCanvasGroup.blocksRaycasts = false;
            }

            openSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject);
            if (dimBackgroundImage != null)
            {
                openSequence.Join(dimBackgroundImage.DOFade(0.82f, dimFadeDuration).SetEase(Ease.OutQuad));
            }

            if (menuCanvasGroup != null)
            {
                openSequence.Join(menuCanvasGroup.DOFade(1f, menuShowDuration).SetEase(Ease.OutQuad));
            }

            if (menuCard != null)
            {
                openSequence.Join(menuCard.DOAnchorPos(menuCardShownPosition, menuShowDuration).SetEase(Ease.OutCubic));
                openSequence.Join(menuCard.DOScale(Vector3.one, menuShowDuration).SetEase(Ease.OutBack));
            }

            openSequence.OnComplete(() =>
            {
                if (menuCanvasGroup != null)
                {
                    menuCanvasGroup.interactable = true;
                    menuCanvasGroup.blocksRaycasts = true;
                }
            });
        }

        private void KillOpenSequence()
        {
            openSequence?.Kill();
            openSequence = null;
            dimBackgroundImage?.DOKill();
            menuCanvasGroup?.DOKill();
            menuCard?.DOKill();
        }

        private void SetExitButtonInteractable(bool interactable)
        {
            if (exitGameButton != null)
            {
                exitGameButton.interactable = interactable;
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }

        private static void SetPlayerInputBlocked(bool blocked)
        {
            foreach (var player in FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None))
            {
                player.SetPauseInputBlocked(blocked);
            }
        }
    }
}
