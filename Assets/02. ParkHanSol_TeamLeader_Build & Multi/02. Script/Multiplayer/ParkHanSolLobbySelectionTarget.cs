using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Selectable))]
    public sealed class ParkHanSolLobbySelectionTarget : MonoBehaviour,
        IPointerEnterHandler,
        ISelectHandler,
        ISubmitHandler
    {
        [SerializeField] private ParkHanSolLobbySelectionIndicator indicator;
        [SerializeField] private Selectable selectable;
        [SerializeField] private RectTransform visualTarget;
        [SerializeField] private Graphic focusGraphic;

        private Color defaultGraphicColor;
        private bool hasDefaultGraphicColor;

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
        }

        private void OnEnable()
        {
            indicator?.Register(this);
        }

        private void OnDisable()
        {
            indicator?.Unregister(this);
            SetFocused(false, Color.black, 0f, true);
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

        public void OnSelect(BaseEventData eventData)
        {
            indicator?.Focus(this);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            indicator?.PlaySubmitFeedback();
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

        private void OnDestroy()
        {
            focusGraphic?.DOKill();
        }
    }
}
