using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSHudFeedbackController : MonoBehaviour, IHudFeedback
    {
        [Header("Value Motion")]
        [SerializeField] private ParkHanSolHudTextMotion healthMotion;
        [SerializeField] private ParkHanSolHudTextMotion boostMotion;
        [SerializeField] private ParkHanSolHudTextMotion bankMotion;
        [FormerlySerializedAs("quotaMotion")]
        [SerializeField] private ParkHanSolHudTextMotion warpMotion;
        [SerializeField] private ParkHanSolHudTextMotion shipHpMotion;
        [SerializeField] private ParkHanSolHudTimerMotion timerMotion;

        [Header("Held Item")]
        [SerializeField] private RectTransform heldItemRoot;
        [SerializeField] private CanvasGroup heldItemGroup;

        [Header("Interaction Prompt")]
        [SerializeField] private RectTransform interactionPromptRoot;
        [SerializeField] private CanvasGroup interactionPromptGroup;
        [SerializeField] private TMP_Text interactionInputText;
        [SerializeField] private TMP_Text interactionPromptText;

        [Header("Ship Alert")]
        [SerializeField] private RectTransform gravityWarningRoot;
        [SerializeField] private CanvasGroup gravityWarningGroup;
        [SerializeField] private TMP_Text gravityWarningText;

        [Header("Respawn Status")]
        [SerializeField] private GameObject respawnStatusPanel;
        [SerializeField] private TMP_Text respawnStatusText;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float showDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.12f;
        [SerializeField, Min(1f)] private float timeLimitTotalSeconds = 300f;

        private int previousHealth;
        private int previousBoost;
        private int previousBank;
        private int previousWarpPercent;
        private int previousShipHp;
        private bool hasVitals;
        private bool hasBoost;
        private bool hasEconomy;
        private bool hasWarpValue;
        private bool hasShipHp;
        private bool hasHeldItemState;
        private bool previousHeldItemState;
        private string previousInteractionPrompt;
        private bool isGravityWarningVisible;
        private bool isHazardWarningVisible;
        private bool isWarningPanelVisible;
        private string defaultGravityWarningText;
        private Vector2 interactionPromptShownPosition;
        private Vector2 gravityWarningShownPosition;

        private void Awake()
        {
            if (interactionPromptRoot != null)
            {
                interactionPromptShownPosition = interactionPromptRoot.anchoredPosition;
            }

            if (gravityWarningRoot != null)
            {
                gravityWarningShownPosition = gravityWarningRoot.anchoredPosition;
            }

            if (gravityWarningText == null)
            {
                Debug.LogError($"PHS_HUD_SETUP_FAILED reason=gravity_warning_text_missing hud={name}", this);
            }
            else
            {
                defaultGravityWarningText = gravityWarningText.text;
            }

            SetPanelImmediate(interactionPromptRoot, interactionPromptGroup, false, interactionPromptShownPosition);
            SetPanelImmediate(gravityWarningRoot, gravityWarningGroup, false, gravityWarningShownPosition);
            ClearRespawnStatus();
        }

        public void SetVitals(int health, int maxHealth, int stamina, int maxStamina)
        {
            var safeMaxHealth = Mathf.Max(1, maxHealth);
            healthMotion?.SetValue($"+{health}<size=24>/{safeMaxHealth}</size>", (float)health / safeMaxHealth);
            PlayValueFeedback(healthMotion, hasVitals, previousHealth, health, true);
            previousHealth = health;
            hasVitals = true;

            if (!hasBoost)
            {
                SetThrusterFuel(stamina, maxStamina);
            }
        }

        public void SetThrusterFuel(int currentFuel, int maxFuel)
        {
            var safeMaxFuel = Mathf.Max(1, maxFuel);
            boostMotion?.SetValue($" {currentFuel}<size=22>/{safeMaxFuel}</size>", (float)currentFuel / safeMaxFuel);
            PlayValueFeedback(boostMotion, hasBoost, previousBoost, currentFuel, false);
            previousBoost = currentFuel;
            hasBoost = true;
        }

        public void SetEconomy(int money, int bank)
        {
            bankMotion?.SetValue($"${bank:N0}", 1f);

            if (hasEconomy && bank != previousBank)
            {
                if (bank > previousBank) bankMotion?.PlayIncreaseFeedback();
                else bankMotion?.PlayDecreaseFeedback();
            }

            previousBank = bank;
            hasEconomy = true;
        }

        public void SetWarpGauge(float normalizedValue)
        {
            var clampedValue = Mathf.Clamp01(normalizedValue);
            var percentage = Mathf.RoundToInt(clampedValue * 100f);
            warpMotion?.SetValue($"{percentage}%", clampedValue);

            if (hasWarpValue && percentage != previousWarpPercent)
            {
                if (percentage > previousWarpPercent) warpMotion?.PlayIncreaseFeedback();
                else warpMotion?.PlayDecreaseFeedback();
            }

            previousWarpPercent = percentage;
            hasWarpValue = true;
        }

        public void SetShipHp(int current, int max)
        {
            var safeMax = Mathf.Max(1, max);
            shipHpMotion?.SetValue($"SHIP {current}<size=20>/{safeMax}</size>", (float)current / safeMax);
            PlayValueFeedback(shipHpMotion, hasShipHp, previousShipHp, current, true);
            previousShipHp = current;
            hasShipHp = true;
        }

        public void SetTimeLimit(float seconds)
        {
            timerMotion?.SetTime(seconds, timeLimitTotalSeconds);
        }

        public void PlayHeldItemChanged(bool hasItem)
        {
            if (heldItemRoot == null)
            {
                return;
            }

            if (hasHeldItemState && previousHeldItemState == hasItem)
            {
                return;
            }

            hasHeldItemState = true;
            previousHeldItemState = hasItem;
            heldItemRoot.DOKill();
            heldItemGroup?.DOKill();
            heldItemRoot.localScale = hasItem ? Vector3.one * 0.88f : Vector3.one * 0.96f;
            heldItemRoot.DOScale(Vector3.one, 0.18f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (heldItemGroup != null)
            {
                heldItemGroup.alpha = hasItem ? 0.55f : 0.72f;
                heldItemGroup.DOFade(1f, 0.14f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        public void SetInteractionPrompt(string inputLabel, string prompt)
        {
            var normalizedPrompt = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim();
            if (previousInteractionPrompt == normalizedPrompt)
            {
                return;
            }

            previousInteractionPrompt = normalizedPrompt;
            if (interactionInputText != null) interactionInputText.text = string.IsNullOrWhiteSpace(inputLabel) ? "F" : inputLabel;
            if (interactionPromptText != null) interactionPromptText.text = normalizedPrompt;
            AnimatePanel(interactionPromptRoot, interactionPromptGroup, !string.IsNullOrEmpty(normalizedPrompt), interactionPromptShownPosition);
        }

        public void SetGravityWarning(bool isVisible)
        {
            if (isGravityWarningVisible == isVisible)
            {
                return;
            }

            isGravityWarningVisible = isVisible;
            RefreshWarningPanel();
        }

        public void SetHazardWarning(string message)
        {
            if (gravityWarningText == null)
            {
                Debug.LogError($"PHS_HUD_HAZARD_WARNING_FAILED reason=text_missing hud={name}", this);
                return;
            }

            isHazardWarningVisible = !string.IsNullOrWhiteSpace(message);
            gravityWarningText.text = isHazardWarningVisible ? message.Trim() : defaultGravityWarningText;
            RefreshWarningPanel();
        }

        public void ClearHazardWarning()
        {
            isHazardWarningVisible = false;
            if (gravityWarningText != null)
            {
                gravityWarningText.text = defaultGravityWarningText;
            }

            RefreshWarningPanel();
        }

        public void SetRespawnCountdown(float seconds)
        {
            if (!RequireRespawnUi(nameof(SetRespawnCountdown)))
            {
                return;
            }

            var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            respawnStatusText.text = $"부활까지 {remainingSeconds}초";
            respawnStatusPanel.SetActive(true);
        }

        public void SetWarpRespawnPending()
        {
            if (!RequireRespawnUi(nameof(SetWarpRespawnPending)))
            {
                return;
            }

            respawnStatusText.text = "워프 완료 후 자동 부활";
            respawnStatusPanel.SetActive(true);
        }

        public void ClearRespawnStatus()
        {
            if (!RequireRespawnUi(nameof(ClearRespawnStatus)))
            {
                return;
            }

            respawnStatusText.text = string.Empty;
            respawnStatusPanel.SetActive(false);
        }

        private bool RequireRespawnUi(string operation)
        {
            var isReady = true;
            if (respawnStatusPanel == null)
            {
                Debug.LogError($"PHS_HUD_RESPAWN_SETUP_FAILED reason=respawn_status_panel_missing operation={operation} hud={name}", this);
                isReady = false;
            }

            if (respawnStatusText == null)
            {
                Debug.LogError($"PHS_HUD_RESPAWN_SETUP_FAILED reason=respawn_status_text_missing operation={operation} hud={name}", this);
                isReady = false;
            }

            return isReady;
        }

        private void RefreshWarningPanel()
        {
            var shouldShow = isGravityWarningVisible || isHazardWarningVisible;
            if (isWarningPanelVisible == shouldShow)
            {
                return;
            }

            isWarningPanelVisible = shouldShow;
            AnimatePanel(gravityWarningRoot, gravityWarningGroup, shouldShow, gravityWarningShownPosition);
            if (shouldShow && gravityWarningRoot != null)
            {
                gravityWarningRoot.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), 0.22f, 5, 0.55f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private static void PlayValueFeedback(ParkHanSolHudTextMotion motion, bool initialized, int previous, int current, bool damageBurst)
        {
            if (!initialized || motion == null || previous == current)
            {
                return;
            }

            if (current < previous)
            {
                motion.PlayDamageFeedback(damageBurst);
                return;
            }

            motion.PlayIncreaseFeedback();
        }

        private void AnimatePanel(RectTransform root, CanvasGroup group, bool show, Vector2 shownPosition)
        {
            if (root == null || group == null)
            {
                return;
            }

            root.DOKill();
            group.DOKill();
            root.gameObject.SetActive(true);

            if (show)
            {
                root.anchoredPosition = shownPosition + new Vector2(0f, -18f);
                group.alpha = 0f;
                var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
                sequence.Join(root.DOAnchorPos(shownPosition, showDuration).SetEase(Ease.OutCubic));
                sequence.Join(group.DOFade(1f, showDuration));
                return;
            }

            var hideSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            hideSequence.Join(root.DOAnchorPos(shownPosition + new Vector2(0f, -10f), hideDuration).SetEase(Ease.InCubic));
            hideSequence.Join(group.DOFade(0f, hideDuration));
            hideSequence.OnComplete(() =>
            {
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                    root.anchoredPosition = shownPosition;
                }
            });
        }

        private static void SetPanelImmediate(RectTransform root, CanvasGroup group, bool visible, Vector2 shownPosition)
        {
            if (root == null || group == null)
            {
                return;
            }

            root.anchoredPosition = shownPosition;
            group.alpha = visible ? 1f : 0f;
            root.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            heldItemRoot?.DOKill();
            heldItemGroup?.DOKill();
            interactionPromptRoot?.DOKill();
            interactionPromptGroup?.DOKill();
            gravityWarningRoot?.DOKill();
            gravityWarningGroup?.DOKill();
        }
    }
}
