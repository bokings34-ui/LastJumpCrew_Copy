using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudTimerMotion : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private RectTransform timerRoot;
        [SerializeField, Range(0f, 1f)] private float shakeStartNormalizedTime = 0.35f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.09f;
        [SerializeField, Min(0.01f)] private float maximumShakeStrength = 6f;

        private float nextShakeTime;
        private Vector2 originAnchoredPosition;

        private void Awake()
        {
            if (timerRoot != null)
            {
                originAnchoredPosition = timerRoot.anchoredPosition;
            }
        }

        public void SetTime(float remainingSeconds, float totalSeconds)
        {
            if (timerText == null || timerRoot == null)
            {
                Debug.LogError($"PHS_HUD_TIMER_MOTION_FAILED reason=reference_missing target={name}");
                return;
            }

            var clampedRemainingSeconds = Mathf.Max(0f, remainingSeconds);
            var normalizedRemainingTime = totalSeconds <= 0f ? 0f : Mathf.Clamp01(clampedRemainingSeconds / totalSeconds);
            var seconds = Mathf.CeilToInt(clampedRemainingSeconds);
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";

            if (normalizedRemainingTime > shakeStartNormalizedTime || Time.unscaledTime < nextShakeTime)
            {
                return;
            }

            var urgency = 1f - normalizedRemainingTime / shakeStartNormalizedTime;
            var strength = Mathf.Lerp(1f, maximumShakeStrength, urgency);
            var interval = Mathf.Lerp(0.72f, 0.16f, urgency);
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
    }
}
