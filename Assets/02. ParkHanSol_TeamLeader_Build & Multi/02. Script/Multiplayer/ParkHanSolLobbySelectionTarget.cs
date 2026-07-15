using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Selectable))]
    public sealed class ParkHanSolLobbySelectionTarget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IPointerExitHandler,
        ISelectHandler,
        ISubmitHandler
    {
        [Header("References")]
        [SerializeField] private ParkHanSolLobbySelectionIndicator indicator;
        [SerializeField] private Selectable selectable;
        [SerializeField] private RectTransform visualTarget;
        [SerializeField] private Graphic focusGraphic;

        [Header("Button Press Motion")]
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.96f;
        [SerializeField, Min(0.01f)] private float pressDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.12f;
        [SerializeField] private Ease pressEase = Ease.OutQuad;
        [SerializeField] private Ease releaseEase = Ease.OutBack;

        private Color defaultGraphicColor;
        private bool hasDefaultGraphicColor;
        private Vector3 defaultVisualScale;
        private bool hasDefaultVisualScale;
        private Tween pressTween;

        public RectTransform VisualTarget => visualTarget;

        public bool IsAvailable =>
            isActiveAndEnabled &&
            selectable != null &&
            selectable.IsInteractable() &&
            selectable.gameObject.activeInHierarchy;

        private void Awake()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            if (visualTarget == null)
            {
                visualTarget = transform as RectTransform;
            }

            CaptureDefaultGraphicColor();
            CaptureDefaultVisualScale();
        }

        private void OnEnable()
        {
            indicator?.Register(this);
        }

        private void OnDisable()
        {
            indicator?.Unregister(this);
            SetFocused(false, Color.black, 0f, true);
            ResetPressVisual(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsAvailable)
            {
                return;
            }

            selectable.Select();
            indicator?.Focus(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsAvailable)
            {
                return;
            }

            Select();
            indicator?.Focus(this);
            PlayPressMotion();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ResetPressVisual(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsAvailable)
            {
                return;
            }

            Select();
            indicator?.Focus(this);
            indicator?.PlaySubmitFeedback();
            ResetPressVisual(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetPressVisual(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            indicator?.Focus(this);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            indicator?.PlaySubmitFeedback();
            PlaySubmitMotion();
        }

        public void SetIndicator(ParkHanSolLobbySelectionIndicator value)
        {
            if (indicator == value)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                indicator?.Unregister(this);
            }

            indicator = value;

            if (isActiveAndEnabled)
            {
                indicator?.Register(this);
            }
        }

        public void Select()
        {
            if (IsAvailable)
            {
                selectable.Select();
            }
        }

        public void SetFocused(bool focused, Color focusedColor, float duration, bool immediate)
        {
            if (focusGraphic == null)
            {
                return;
            }

            CaptureDefaultGraphicColor();
            var targetColor = focused ? focusedColor : defaultGraphicColor;
            focusGraphic.DOKill();
            if (immediate || duration <= 0f)
            {
                focusGraphic.color = targetColor;
                return;
            }

            focusGraphic.DOColor(targetColor, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void CaptureDefaultGraphicColor()
        {
            if (hasDefaultGraphicColor || focusGraphic == null)
            {
                return;
            }

            defaultGraphicColor = focusGraphic.color;
            hasDefaultGraphicColor = true;
        }

        private void CaptureDefaultVisualScale()
        {
            if (hasDefaultVisualScale || visualTarget == null)
            {
                return;
            }

            defaultVisualScale = visualTarget.localScale;
            hasDefaultVisualScale = true;
        }

        private void PlayPressMotion()
        {
            if (selectable is not Button || visualTarget == null)
            {
                return;
            }

            CaptureDefaultVisualScale();
            KillPressTween();
            pressTween = visualTarget
                .DOScale(defaultVisualScale * pressedScale, pressDuration)
                .SetEase(pressEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlaySubmitMotion()
        {
            if (selectable is not Button || visualTarget == null)
            {
                return;
            }

            CaptureDefaultVisualScale();
            KillPressTween();
            pressTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject)
                .Append(visualTarget.DOScale(defaultVisualScale * pressedScale, pressDuration).SetEase(pressEase))
                .Append(visualTarget.DOScale(defaultVisualScale, releaseDuration).SetEase(releaseEase));
        }

        private void ResetPressVisual(bool immediate)
        {
            if (selectable is not Button || visualTarget == null)
            {
                return;
            }

            CaptureDefaultVisualScale();
            KillPressTween();
            if (immediate || !gameObject.activeInHierarchy)
            {
                visualTarget.localScale = defaultVisualScale;
                return;
            }

            pressTween = visualTarget
                .DOScale(defaultVisualScale, releaseDuration)
                .SetEase(releaseEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void KillPressTween()
        {
            pressTween?.Kill();
            pressTween = null;
        }

        private void OnDestroy()
        {
            KillPressTween();
            focusGraphic?.DOKill();
            visualTarget?.DOKill();
        }
    }
}
