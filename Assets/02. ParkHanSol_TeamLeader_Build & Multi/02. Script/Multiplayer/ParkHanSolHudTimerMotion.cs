using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudTimerMotion : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private RectTransform timerRoot;
        [SerializeField] private Color safeColor = new(0.22f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color dangerColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.09f;
        [SerializeField, Min(0.01f)] private float minimumShakeStrength = 0.25f;
        [SerializeField, Min(0.01f)] private float maximumShakeStrength = 6f;

        private float nextShakeTime;
        private Vector2 originAnchoredPosition;
        private bool isLayoutCaptured;

        private void Awake()
        {
            CaptureLayoutIfNeeded();
        }

        public void SetTime(float remainingSeconds, float totalSeconds)
        {
            CaptureLayoutIfNeeded();
            if (timerText == null || timerRoot == null)
            {
                Debug.LogError($"PHS_HUD_TIMER_MOTION_FAILED reason=reference_missing target={name}");
                return;
            }

            var clampedRemainingSeconds = Mathf.Max(0f, remainingSeconds);
            var normalizedRemainingTime = totalSeconds <= 0f ? 0f : Mathf.Clamp01(clampedRemainingSeconds / totalSeconds);
            var urgency = 1f - normalizedRemainingTime;
            var seconds = Mathf.CeilToInt(clampedRemainingSeconds);
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timerText.color = Color.Lerp(safeColor, dangerColor, urgency);

            if (urgency <= 0.01f || Time.unscaledTime < nextShakeTime)
            {
                return;
            }

            var strength = Mathf.Lerp(minimumShakeStrength, maximumShakeStrength, urgency);
            var interval = Mathf.Lerp(0.85f, 0.12f, urgency);
            timerRoot.DOKill();
            timerRoot.anchoredPosition = originAnchoredPosition;
            timerRoot.DOShakeAnchorPos(shakeDuration, new Vector2(strength, 0f), 10, 90f, false, true)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
            nextShakeTime = Time.unscaledTime + interval;
        }

        private void OnDestroy()
        {
            if (timerRoot != null)
            {
                timerRoot.DOKill();
            }
        }

        private void CaptureLayoutIfNeeded()
        {
            if (isLayoutCaptured || timerRoot == null)
            {
                return;
            }

            originAnchoredPosition = timerRoot.anchoredPosition;
            isLayoutCaptured = true;
        }
    }
}
