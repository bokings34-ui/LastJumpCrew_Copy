using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolLobbySelectionIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform selectionBar;
        [SerializeField] private CanvasGroup selectionCanvasGroup;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float horizontalPadding = 18f;
        [SerializeField, Min(0f)] private float verticalPadding = 4f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float moveDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float resizeDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float textColorDuration = 0.10f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private Color focusedTextColor = new(0.015f, 0.02f, 0.03f, 1f);

        private readonly List<ParkHanSolLobbySelectionTarget> targets = new();
        private ParkHanSolLobbySelectionTarget currentTarget;
        private Sequence transitionSequence;
        private bool hasPresented;
        private readonly Vector3[] visualTargetWorldCorners = new Vector3[4];

        public void Register(ParkHanSolLobbySelectionTarget target)
        {
            if (target == null || targets.Contains(target))
            {
                return;
            }

            targets.Add(target);
        }

        public void Unregister(ParkHanSolLobbySelectionTarget target)
        {
            if (target == null)
            {
                return;
            }

            targets.Remove(target);
            if (currentTarget == target)
            {
                currentTarget.SetFocused(false, focusedTextColor, textColorDuration, true);
                currentTarget = null;
            }
        }

        public void Focus(ParkHanSolLobbySelectionTarget target, bool immediate = false)
        {
            if (target == null || !target.IsAvailable || selectionBar == null)
            {
                return;
            }

            var visualTarget = target.VisualTarget;
            var indicatorParent = selectionBar.parent as RectTransform;
            if (visualTarget == null || indicatorParent == null)
            {
                return;
            }

            if (currentTarget != target)
            {
                currentTarget?.SetFocused(false, focusedTextColor, textColorDuration, immediate);
                currentTarget = target;
                currentTarget.SetFocused(true, focusedTextColor, textColorDuration, immediate);
            }

            visualTarget.GetWorldCorners(visualTargetWorldCorners);
            var min = indicatorParent.InverseTransformPoint(visualTargetWorldCorners[0]);
            var max = min;
            for (var i = 1; i < visualTargetWorldCorners.Length; i++)
            {
                var corner = indicatorParent.InverseTransformPoint(visualTargetWorldCorners[i]);
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            var targetPosition = new Vector2(
                (min.x + max.x) * 0.5f,
                (min.y + max.y) * 0.5f);
            var targetSize = new Vector2(
                max.x - min.x + horizontalPadding * 2f,
                max.y - min.y + verticalPadding * 2f);

            KillTransition();
            if (immediate || !hasPresented)
            {
                selectionBar.anchoredPosition = targetPosition;
                selectionBar.sizeDelta = targetSize;
                SetAlpha(1f);
                hasPresented = true;
                return;
            }

            transitionSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject);
            transitionSequence.Join(selectionBar.DOAnchorPos(targetPosition, moveDuration).SetEase(moveEase));
            transitionSequence.Join(selectionBar.DOSizeDelta(targetSize, resizeDuration).SetEase(moveEase));
            if (selectionCanvasGroup != null)
            {
                transitionSequence.Join(selectionCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad));
            }
        }

        public void PlaySubmitFeedback()
        {
            if (selectionCanvasGroup == null)
            {
                return;
            }

            selectionCanvasGroup.DOKill();
            DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject)
                .Append(selectionCanvasGroup.DOFade(0.78f, 0.04f).SetEase(Ease.OutQuad))
                .Append(selectionCanvasGroup.DOFade(1f, 0.08f).SetEase(Ease.OutCubic));
        }

        private void OnEnable()
        {
            SetAlpha(0f);
        }

        private void LateUpdate()
        {
            var selectedObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;
            var selectedTarget = selectedObject == null
                ? null
                : selectedObject.GetComponent<ParkHanSolLobbySelectionTarget>();

            if (selectedTarget != null && selectedTarget.IsAvailable)
            {
                if (selectedTarget != currentTarget)
                {
                    Focus(selectedTarget);
                }

                return;
            }

            if (currentTarget != null && currentTarget.IsAvailable)
            {
                return;
            }

            SelectFirstAvailableTarget();
        }

        private void SelectFirstAvailableTarget()
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAvailable)
                {
                    continue;
                }

                target.Select();
                Focus(target, !hasPresented);
                return;
            }

            currentTarget = null;
            if (selectionCanvasGroup != null && selectionCanvasGroup.alpha > 0f)
            {
                selectionCanvasGroup.DOKill();
                selectionCanvasGroup.DOFade(0f, fadeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private void OnDisable()
        {
            KillTransition();
            selectionCanvasGroup?.DOKill();
            currentTarget?.SetFocused(false, focusedTextColor, textColorDuration, true);
            currentTarget = null;
            hasPresented = false;
        }

        private void KillTransition()
        {
            transitionSequence?.Kill();
            transitionSequence = null;
            selectionBar?.DOKill();
        }

        private void SetAlpha(float alpha)
        {
            if (selectionCanvasGroup != null)
            {
                selectionCanvasGroup.alpha = alpha;
            }
        }
    }
}
