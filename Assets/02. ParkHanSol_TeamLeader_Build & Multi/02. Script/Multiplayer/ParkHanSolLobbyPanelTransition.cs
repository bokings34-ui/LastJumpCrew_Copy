using DG.Tweening;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ParkHanSolLobbyPanelTransition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform visualRoot;

        [Header("Motion")]
        [SerializeField, Range(0.9f, 1f)] private float hiddenScale = 0.975f;
        [SerializeField, Min(0.01f)] private float showDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.10f;
        [SerializeField] private Ease showEase = Ease.OutCubic;
        [SerializeField] private Ease hideEase = Ease.InQuad;

        private Vector3 visibleScale;
        private bool initialized;
        private bool targetVisible;
        private Sequence transitionSequence;

        public void SetVisible(bool visible, bool immediate = false)
        {
            Initialize();

            if (visible == targetVisible)
            {
                if (immediate)
                {
                    KillTransition();
                    if (visible)
                    {
                        ApplyVisibleState();
                    }
                    else
                    {
                        ApplyHiddenState();
                    }

                    return;
                }

                if (transitionSequence != null && transitionSequence.IsActive())
                {
                    return;
                }

                if (visible && gameObject.activeSelf && IsAtVisibleState())
                {
                    SetInteraction(true);
                    return;
                }

                if (!visible && !gameObject.activeSelf)
                {
                    return;
                }
            }

            targetVisible = visible;
            KillTransition();

            if (visible)
            {
                var wasActive = gameObject.activeSelf;
                if (!wasActive)
                {
                    gameObject.SetActive(true);
                    canvasGroup.alpha = 0f;
                    visualRoot.localScale = visibleScale * hiddenScale;
                }

                SetInteraction(false);
                if (immediate)
                {
                    ApplyVisibleState();
                    return;
                }

                transitionSequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .Join(canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad))
                    .Join(visualRoot.DOScale(visibleScale, showDuration).SetEase(showEase))
                    .OnComplete(() =>
                    {
                        transitionSequence = null;
                        if (targetVisible)
                        {
                            SetInteraction(true);
                        }
                    });
                return;
            }

            if (!gameObject.activeSelf)
            {
                return;
            }

            SetInteraction(false);
            if (immediate)
            {
                ApplyHiddenState();
                return;
            }

            transitionSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject)
                .Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad))
                .Join(visualRoot.DOScale(visibleScale * hiddenScale, hideDuration).SetEase(hideEase))
                .OnComplete(() =>
                {
                    transitionSequence = null;
                    if (!targetVisible)
                    {
                        gameObject.SetActive(false);
                    }
                });
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (visualRoot == null)
            {
                visualRoot = transform as RectTransform;
            }

            visibleScale = visualRoot == null ? Vector3.one : visualRoot.localScale;
            targetVisible = gameObject.activeSelf;
            initialized = true;
        }

        private void ApplyVisibleState()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            canvasGroup.alpha = 1f;
            visualRoot.localScale = visibleScale;
            SetInteraction(true);
        }

        private void ApplyHiddenState()
        {
            canvasGroup.alpha = 0f;
            visualRoot.localScale = visibleScale * hiddenScale;
            SetInteraction(false);
            gameObject.SetActive(false);
        }

        private void SetInteraction(bool active)
        {
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }

        private bool IsAtVisibleState()
        {
            return Mathf.Approximately(canvasGroup.alpha, 1f) &&
                (visualRoot.localScale - visibleScale).sqrMagnitude < 0.000001f;
        }

        private void OnDisable()
        {
            KillTransition();
            if (!initialized)
            {
                return;
            }

            canvasGroup.alpha = targetVisible ? 1f : 0f;
            visualRoot.localScale = targetVisible ? visibleScale : visibleScale * hiddenScale;
            SetInteraction(targetVisible);
        }

        private void OnDestroy()
        {
            KillTransition();
        }

        private void KillTransition()
        {
            transitionSequence?.Kill();
            transitionSequence = null;
            canvasGroup?.DOKill();
            visualRoot?.DOKill();
        }
    }
}
