using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudGaugeMotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform gaugeRoot;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image changeImage;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private RectTransform fullGlowRoot;
        [SerializeField] private CanvasGroup fullGlowGroup;

        [Header("State")]
        [SerializeField] private Color emptyValueColor = new(0.05f, 0.16f, 0.42f, 1f);
        [SerializeField] private Color fullValueColor = new(0.12f, 0.72f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float fullFlashDuration = 0.42f;
        [SerializeField, Min(0.01f)] private float increaseFillDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float decreaseFillDuration = 0.18f;
        [SerializeField, Min(0f)] private float changeHoldDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float changeCatchupDuration = 0.34f;
        [SerializeField] private Color damageChangeColor = new(1f, 0.12f, 0.08f, 0.9f);
        [SerializeField] private Color recoveryChangeColor = new(0.28f, 1f, 0.43f, 0.78f);

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float punchDuration = 0.16f;
        [SerializeField] private Vector3 consumePunch = new(0.08f, 0.08f, 0f);
        [SerializeField] private Vector3 recoveryPunch = new(0.12f, 0.12f, 0f);

        private float currentValue;
        private bool isInitialized;
        private Sequence changeSequence;

        public void SetValue(float normalizedValue)
        {
            if (fillImage == null)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=fillImage_missing target={name}");
                return;
            }

            var targetValue = Mathf.Clamp01(normalizedValue);
            var displayedValue = fillImage.fillAmount;
            var changed = isInitialized && !Mathf.Approximately(currentValue, targetValue);
            currentValue = targetValue;

            fillImage.DOKill();
            changeImage?.DOKill();
            changeSequence?.Kill();
            changeSequence = null;
            var targetColor = Color.Lerp(emptyValueColor, fullValueColor, currentValue);
            if (changed)
            {
                var duration = targetValue > displayedValue ? increaseFillDuration : decreaseFillDuration;
                PlayChangeTrail(displayedValue, targetValue);
                fillImage.DOFillAmount(targetValue, duration)
                    .SetEase(targetValue > displayedValue ? Ease.OutCubic : Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                fillImage.DOColor(targetColor, duration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
            else
            {
                fillImage.fillAmount = targetValue;
                fillImage.color = targetColor;
                if (changeImage != null)
                {
                    changeImage.fillAmount = targetValue;
                    changeImage.color = Color.clear;
                }
            }

            if (currentValue < 1f)
            {
                StopFullGlow();
            }

            isInitialized = true;
        }

        public void PlayConsumeFeedback()
        {
            PlayPunch(consumePunch);
        }

        public void PlayRecoveryFeedback()
        {
            PlayPunch(recoveryPunch);
        }

        public void PlayFullFeedback()
        {
            if (!isInitialized)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=value_not_initialized target={name}");
                return;
            }

            if (fullGlowRoot == null || fullGlowGroup == null)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=fullGlow_reference_missing target={name}");
                return;
            }

            fillImage.DOKill();
            fillImage.color = fullValueColor;
            StopFullGlow();
            fullGlowGroup.alpha = 0.12f;
            fullGlowRoot.localScale = Vector3.one;
            fullGlowGroup.DOFade(0.72f, fullFlashDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
            fullGlowRoot.DOScale(1.1f, fullFlashDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
            PlayPunch(new Vector3(0.045f, 0.045f, 0f));
        }

        public void PlayEmptyFeedback()
        {
            if (!isInitialized)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=value_not_initialized target={name}");
                return;
            }

            PlayPunch(recoveryPunch);
        }

        public void SetValueText(string value)
        {
            if (valueText == null)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=valueText_missing target={name}");
                return;
            }

            valueText.text = value;
        }

        private void PlayPunch(Vector3 punch)
        {
            if (gaugeRoot == null)
            {
                Debug.LogError($"PHS_HUD_GAUGE_MOTION_FAILED reason=gaugeRoot_missing target={name}");
                return;
            }

            gaugeRoot.DOKill();
            gaugeRoot.DOPunchScale(punch, punchDuration, 6, 0.65f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayChangeTrail(float previousValue, float targetValue)
        {
            if (changeImage == null)
            {
                return;
            }

            changeImage.DOKill();
            var isRecovery = targetValue > previousValue;
            changeImage.fillAmount = isRecovery ? targetValue : previousValue;
            changeImage.color = isRecovery ? recoveryChangeColor : damageChangeColor;

            changeSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            changeSequence.AppendInterval(changeHoldDuration);
            if (isRecovery)
            {
                changeSequence.Append(changeImage.DOColor(Color.clear, changeCatchupDuration));
            }
            else
            {
                changeSequence.Append(changeImage.DOFillAmount(targetValue, changeCatchupDuration)
                    .SetEase(Ease.OutCubic));
                changeSequence.Join(changeImage.DOColor(Color.clear, changeCatchupDuration));
            }
        }

        private void OnDestroy()
        {
            if (gaugeRoot != null)
            {
                gaugeRoot.DOKill();
            }

            changeImage?.DOKill();
            changeSequence?.Kill();

            StopFullGlow();
        }

        private void StopFullGlow()
        {
            if (fullGlowGroup != null)
            {
                fullGlowGroup.DOKill();
                fullGlowGroup.alpha = 0f;
            }

            if (fullGlowRoot != null)
            {
                fullGlowRoot.DOKill();
                fullGlowRoot.localScale = Vector3.one;
            }
        }
    }
}
