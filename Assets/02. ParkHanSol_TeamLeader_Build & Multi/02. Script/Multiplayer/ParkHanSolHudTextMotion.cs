using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudTextMotion : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private RectTransform targetRoot;
        [SerializeField] private ParkHanSolHudBurstMotion damageBurstMotion;

        [Header("Value Colors")]
        [SerializeField] private Color emptyValueColor = new(1f, 0.18f, 0.16f, 1f);
        [SerializeField] private Color middleValueColor = new(1f, 0.84f, 0.18f, 1f);
        [SerializeField] private Color fullValueColor = new(0.28f, 1f, 0.43f, 1f);

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.04f;
        [SerializeField, Min(0.01f)] private float recoverDuration = 0.12f;
        [SerializeField] private Vector3 increasePunch = new(0.055f, 0.055f, 0f);
        [SerializeField] private Vector3 decreasePunch = new(0.065f, 0.065f, 0f);
        [SerializeField] private Vector2 hitShakeStrength = new(3.5f, 0.8f);
        [SerializeField] private Vector2 drainShakeStrength = new(2.4f, 0.4f);

        private float currentNormalizedValue = 1f;
        private Vector3 originScale;
        private Vector2 originAnchoredPosition;
        private bool isLayoutCaptured;

        private void Awake()
        {
            CaptureLayoutIfNeeded();
        }

        public void SetText(string value)
        {
            CaptureLayoutIfNeeded();
            if (targetText == null)
            {
                Debug.LogError($"PHS_HUD_TEXT_MOTION_FAILED reason=targetText_missing target={name}");
                return;
            }

            targetText.text = value;
        }

        public void SetValue(string value, float normalizedValue)
        {
            currentNormalizedValue = Mathf.Clamp01(normalizedValue);
            SetText(value);

            if (targetText != null)
            {
                targetText.color = EvaluateValueColor(currentNormalizedValue);
            }
        }

        public void PlayIncreaseFeedback()
        {
            PlayPunch(increasePunch);
        }

        public void PlayDecreaseFeedback()
        {
            PlayDamageFeedback(false);
        }

        public void PlayDamageFeedback(bool playBurst)
        {
            CaptureLayoutIfNeeded();
            if (targetText == null)
            {
                Debug.LogError($"PHS_HUD_TEXT_MOTION_FAILED reason=targetText_missing target={name}");
                return;
            }

            PlayPunch(decreasePunch);
            targetRoot.anchoredPosition = originAnchoredPosition;
            targetRoot.DOShakeAnchorPos(feedbackDuration, hitShakeStrength, 10, 90f, false, true)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
            targetText.DOKill();
            var flashSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            flashSequence.Append(targetText.DOColor(Color.white, flashDuration).SetEase(Ease.OutQuad));
            flashSequence.Append(targetText.DOColor(EvaluateValueColor(currentNormalizedValue), recoverDuration).SetEase(Ease.OutCubic));

            if (playBurst && damageBurstMotion != null)
            {
                damageBurstMotion.PlayBurst();
            }
        }

        public void PlayDrainFeedback()
        {
            CaptureLayoutIfNeeded();
            if (targetRoot == null)
            {
                Debug.LogError($"PHS_HUD_TEXT_MOTION_FAILED reason=targetRoot_missing target={name}");
                return;
            }

            targetRoot.DOKill();
            targetRoot.anchoredPosition = originAnchoredPosition;
            targetRoot.DOShakeAnchorPos(0.11f, drainShakeStrength, 9, 90f, false, true)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayPunch(Vector3 punch)
        {
            CaptureLayoutIfNeeded();
            if (targetRoot == null)
            {
                Debug.LogError($"PHS_HUD_TEXT_MOTION_FAILED reason=targetRoot_missing target={name}");
                return;
            }

            targetRoot.DOKill();
            targetRoot.localScale = originScale;
            var impactScale = new Vector3(
                originScale.x * (1f + punch.x),
                originScale.y * (1f - punch.y * 0.45f),
                originScale.z);
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Append(targetRoot.DOScale(impactScale, 0.045f).SetEase(Ease.OutQuad));
            sequence.Append(targetRoot.DOScale(originScale, 0.11f).SetEase(Ease.OutBack));
        }

        private void OnDestroy()
        {
            if (targetRoot != null)
            {
                targetRoot.DOKill();
            }
        }

        private Color EvaluateValueColor(float normalizedValue)
        {
            if (normalizedValue <= 0.5f)
            {
                return Color.Lerp(emptyValueColor, middleValueColor, normalizedValue * 2f);
            }

            return Color.Lerp(middleValueColor, fullValueColor, (normalizedValue - 0.5f) * 2f);
        }

        private void CaptureLayoutIfNeeded()
        {
            if (isLayoutCaptured || targetRoot == null)
            {
                return;
            }

            originScale = targetRoot.localScale;
            originAnchoredPosition = targetRoot.anchoredPosition;
            isLayoutCaptured = true;
        }
    }
}
