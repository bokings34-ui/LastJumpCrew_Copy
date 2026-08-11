using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public enum TutorialBriefingPageKind
    {
        Text = 0,
        Video = 1
    }

    [Serializable]
    public sealed class TutorialBriefingPage
    {
        [SerializeField] private TutorialBriefingPageKind pageKind;
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 10)] private string body;
        [SerializeField] private VideoClip videoClip;

        public TutorialBriefingPageKind PageKind => pageKind;
        public string Title => title;
        public string Body => body;
        public VideoClip VideoClip => videoClip;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                reason = "page_title_missing";
                return false;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                reason = "page_body_missing";
                return false;
            }

            if (pageKind == TutorialBriefingPageKind.Video
                && videoClip == null)
            {
                reason = "video_clip_missing";
                return false;
            }

            if (pageKind == TutorialBriefingPageKind.Text
                && videoClip != null)
            {
                reason = "text_page_video_clip_not_allowed";
                return false;
            }

            reason = null;
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class NetworkTutorialBriefingPresenter : MonoBehaviour
    {
        private const float PageFadeDuration = 0.14f;

        [Header("Popup")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text pageIndicatorText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;

        [Header("Video")]
        [SerializeField] private GameObject videoRoot;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RenderTexture videoTexture;

        [Header("Input")]
        [SerializeField] private NetworkPlayerController playerController;

        private NetworkTutorialRoomController currentOwner;
        private TutorialBriefingPage[] currentPages =
            Array.Empty<TutorialBriefingPage>();
        private int currentPageIndex;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool inputBlocked;
        private int popupOpenedFrame = -1;
        private Tween pageFadeTween;

        public event Action<NetworkTutorialRoomController> Completed;

        private void Awake()
        {
            if (!TryValidateSetup(out var reason))
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_BRIEFING_DISABLED " +
                    $"presenter={name} reason={reason}",
                    this);
                enabled = false;
                return;
            }

            if (currentOwner == null)
            {
                SetPopupVisible(false);
            }
        }

        private void OnEnable()
        {
            if (previousButton != null)
            {
                previousButton.onClick.AddListener(ShowPreviousPage);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(ShowNextPageOrComplete);
            }

            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted += HandleVideoPrepared;
                videoPlayer.errorReceived += HandleVideoError;
            }
        }

        private void OnDisable()
        {
            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(ShowPreviousPage);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(ShowNextPageOrComplete);
            }

            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandleVideoPrepared;
                videoPlayer.errorReceived -= HandleVideoError;
            }

            ClosePresentation();
        }

        private void Update()
        {
            if (currentOwner == null || !popupRoot.activeInHierarchy)
            {
                return;
            }

            if (Time.frameCount == popupOpenedFrame)
            {
                return;
            }

            var keyboardPressed = Keyboard.current != null
                && Keyboard.current.anyKey.wasPressedThisFrame;
            var mouse = Mouse.current;
            var mousePressed = mouse != null
                && (mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame);
            var pointerOverNavigationButton = mouse != null
                && IsPointerOverNavigationButton(mouse.position.ReadValue());
            if (!keyboardPressed && (!mousePressed || pointerOverNavigationButton))
            {
                return;
            }

            ShowNextPageOrComplete();
        }

        public bool TryPresent(
            NetworkTutorialRoomController owner,
            TutorialBriefingPage[] pages,
            out string reason)
        {
            if (!enabled)
            {
                reason = "presenter_disabled";
                return false;
            }

            if (!TryValidateSetup(out reason))
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_BRIEFING_DISABLED " +
                    $"presenter={name} reason={reason}",
                    this);
                enabled = false;
                return false;
            }

            if (owner == null)
            {
                reason = "owner_missing";
                return false;
            }

            if (!TryValidatePages(pages, out reason))
            {
                return false;
            }

            if (currentOwner != null && currentOwner != owner)
            {
                reason = $"owned_by_other_room:{currentOwner.RoomId}";
                return false;
            }

            if (currentOwner == owner && popupRoot.activeSelf)
            {
                reason = null;
                return true;
            }

            currentOwner = owner;
            currentPages = pages;
            currentPageIndex = 0;
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            playerController.SetResultInputBlocked(true);
            inputBlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetPopupVisible(true);
            popupOpenedFrame = Time.frameCount;
            RefreshPage(0f);
            reason = null;
            return true;
        }

        public void Dismiss(NetworkTutorialRoomController owner)
        {
            if (owner == null || currentOwner != owner)
            {
                return;
            }

            ClosePresentation();
        }

        public bool TryValidatePages(
            TutorialBriefingPage[] pages,
            out string reason)
        {
            if (pages == null || pages.Length == 0)
            {
                reason = "pages_missing";
                return false;
            }

            for (var index = 0; index < pages.Length; index++)
            {
                if (pages[index] == null)
                {
                    reason = $"page_missing:{index}";
                    return false;
                }

                if (!pages[index].TryValidate(out var pageReason))
                {
                    reason = $"page_invalid:{index}:{pageReason}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool TryValidateSetup(out string reason)
        {
            if (popupRoot == null
                || popupRoot == gameObject
                || canvasGroup == null
                || titleText == null
                || bodyText == null
                || pageIndicatorText == null
                || previousButton == null
                || nextButton == null
                || nextButtonLabel == null)
            {
                reason = "popup_reference_missing_or_invalid";
                return false;
            }

            if (videoRoot == null
                || videoImage == null
                || videoPlayer == null
                || videoTexture == null)
            {
                reason = "video_reference_missing";
                return false;
            }

            if (playerController == null)
            {
                reason = "player_controller_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void ShowPreviousPage()
        {
            if (currentOwner == null || currentPageIndex <= 0)
            {
                return;
            }

            currentPageIndex--;
            RefreshPage(0.35f);
        }

        private bool IsPointerOverNavigationButton(Vector2 screenPosition)
        {
            return IsPointerOverButton(previousButton, screenPosition)
                || IsPointerOverButton(nextButton, screenPosition);
        }

        private static bool IsPointerOverButton(
            Button button,
            Vector2 screenPosition)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return false;
            }

            var canvas = button.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                button.transform as RectTransform,
                screenPosition,
                eventCamera);
        }

        private void ShowNextPageOrComplete()
        {
            if (currentOwner == null)
            {
                return;
            }

            if (currentPageIndex < currentPages.Length - 1)
            {
                currentPageIndex++;
                RefreshPage(0.35f);
                return;
            }

            var completedOwner = currentOwner;
            ClosePresentation();
            Completed?.Invoke(completedOwner);
        }

        private void RefreshPage(float fadeStartAlpha)
        {
            if (currentOwner == null
                || currentPageIndex < 0
                || currentPageIndex >= currentPages.Length)
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_BRIEFING_PAGE_FAILED " +
                    $"presenter={name} reason=page_index_invalid " +
                    $"index={currentPageIndex} count={currentPages.Length}",
                    this);
                enabled = false;
                return;
            }

            var page = currentPages[currentPageIndex];
            titleText.text = page.Title;
            bodyText.text = page.Body;
            pageIndicatorText.text =
                $"{currentPageIndex + 1} / {currentPages.Length}";
            var hasMultiplePages = currentPages.Length > 1;
            previousButton.gameObject.SetActive(true);
            pageIndicatorText.gameObject.SetActive(hasMultiplePages);
            previousButton.interactable = currentPageIndex > 0;
            var previousRect = previousButton.GetComponent<RectTransform>();
            previousRect.anchorMin = new Vector2(0.30f, 0.055f);
            previousRect.anchorMax = new Vector2(0.44f, 0.16f);
            previousRect.offsetMin = Vector2.zero;
            previousRect.offsetMax = Vector2.zero;
            var nextRect = nextButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.56f, 0.055f);
            nextRect.anchorMax = new Vector2(0.70f, 0.16f);
            nextRect.offsetMin = Vector2.zero;
            nextRect.offsetMax = Vector2.zero;
            nextButtonLabel.text = ">";

            StopVideo();
            var showVideo = page.PageKind == TutorialBriefingPageKind.Video;
            bodyText.gameObject.SetActive(!showVideo);
            videoRoot.SetActive(showVideo);
            if (!showVideo)
            {
                StartPageFade(fadeStartAlpha);
                return;
            }

            videoImage.texture = videoTexture;
            videoImage.enabled = false;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.clip = page.VideoClip;
            videoPlayer.Prepare();
            StartPageFade(fadeStartAlpha);
        }

        private void StartPageFade(float startAlpha)
        {
            StopPageFade();
            canvasGroup.alpha = Mathf.Clamp01(startAlpha);
            pageFadeTween = canvasGroup
                .DOFade(1f, PageFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => pageFadeTween = null);
        }

        private void StopPageFade()
        {
            pageFadeTween?.Kill();
            pageFadeTween = null;
        }

        private void HandleVideoPrepared(VideoPlayer preparedPlayer)
        {
            if (currentOwner == null
                || preparedPlayer != videoPlayer
                || currentPageIndex < 0
                || currentPageIndex >= currentPages.Length
                || currentPages[currentPageIndex].PageKind
                    != TutorialBriefingPageKind.Video
                || preparedPlayer.clip
                    != currentPages[currentPageIndex].VideoClip)
            {
                return;
            }

            videoImage.enabled = true;
            preparedPlayer.Play();
        }

        private void HandleVideoError(VideoPlayer failedPlayer, string message)
        {
            videoImage.enabled = false;
            Debug.LogError(
                "PHS_NETWORK_TUTORIAL_BRIEFING_VIDEO_FAILED " +
                $"presenter={name} clip={failedPlayer.clip?.name ?? "none"} " +
                $"reason={message}",
                this);
        }

        private void StopVideo()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.Stop();
            videoPlayer.clip = null;
            videoImage.enabled = false;
        }

        private void SetPopupVisible(bool visible)
        {
            StopPageFade();
            popupRoot.SetActive(visible);
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (!visible)
            {
                videoRoot.SetActive(false);
            }
        }

        private void ClosePresentation()
        {
            StopVideo();
            if (popupRoot != null && canvasGroup != null)
            {
                SetPopupVisible(false);
            }

            currentOwner = null;
            currentPages = Array.Empty<TutorialBriefingPage>();
            currentPageIndex = 0;
            if (!inputBlocked)
            {
                return;
            }

            inputBlocked = false;
            playerController.SetResultInputBlocked(false);
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }
}
