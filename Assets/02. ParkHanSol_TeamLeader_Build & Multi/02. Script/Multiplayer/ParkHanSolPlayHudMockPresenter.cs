using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolPlayHudMockPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private TMP_Text bankText;
        [FormerlySerializedAs("quotaText")]
        [SerializeField] private TMP_Text warpGaugeText;
        [SerializeField] private TMP_Text shipHpText;
        [SerializeField] private TMP_Text timeLimitText;
        [SerializeField] private GameObject speakingPlayerPanel;
        [SerializeField] private TMP_Text speakingPlayerText;
        [SerializeField] private RectTransform speakingPlayersContent;
        [SerializeField] private SpeakingPlayerView speakingPlayerTemplate;
        [SerializeField, Min(0.1f)] private float speakingPlayerVisibleSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float speakingPlayerBlinkSpeed = 6f;
        [SerializeField] private TMP_Text heldItemText;
        [SerializeField] private Image heldItemIconImage;
        [SerializeField] private TMP_Text heldItemDurabilityText;
        [SerializeField] private PHSHudFeedbackController hudFeedbackController;

        private readonly List<SpeakingPlayerView> speakingPlayerViews = new();
        private float speakingPlayerHideTime;

        private void Awake()
        {
            if (hudFeedbackController == null)
            {
                hudFeedbackController = GetComponent<PHSHudFeedbackController>();
            }

            ResetPlaceholders();
        }

        private void Update()
        {
            if (speakingPlayerPanel != null && speakingPlayerPanel.activeSelf && Time.unscaledTime >= speakingPlayerHideTime)
            {
                HideSpeakingPlayer();
            }

            RefreshSpeakingPlayerBlink();
        }

        public void SetVitals(int health, int maxHealth, int stamina, int maxStamina)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetVitals(health, maxHealth, stamina, maxStamina);
                return;
            }

            SetText(healthText, $"+{health}<size=26>/{maxHealth}</size>");
            SetText(staminaText, $"ST {stamina}<size=24>/{maxStamina}</size>");
        }

        public void SetThrusterFuel(int currentFuel, int maxFuel)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetThrusterFuel(currentFuel, maxFuel);
                return;
            }

            SetText(staminaText, $" {currentFuel}<size=22>/{maxFuel}</size>");
        }

        public void SetEconomy(int money, int bank)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetEconomy(money, bank);
                return;
            }

            SetText(bankText, $"${bank}");
        }

        public void SetWarpGauge(float normalizedValue)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetWarpGauge(normalizedValue);
                return;
            }

            SetText(warpGaugeText, $"{Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f)}%");
        }

        public void SetShipHp(int current, int max)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetShipHp(current, max);
                return;
            }

            SetText(shipHpText, $"SHIP HP {current}<size=24>/{max}</size>");
        }

        public void SetTimeLimit(float seconds)
        {
            if (hudFeedbackController != null)
            {
                hudFeedbackController.SetTimeLimit(seconds);
                return;
            }

            var time = Mathf.Max(0, Mathf.CeilToInt(seconds));
            SetText(timeLimitText, $"{time / 60:00}:{time % 60:00}");
        }

        public void SetHeldItem(string itemName)
        {
            SetText(heldItemText, string.IsNullOrWhiteSpace(itemName) ? "EMPTY" : itemName);
        }

        public void SetHeldItem(UtilityItemPrefabData itemPrefabData)
        {
            if (itemPrefabData == null)
            {
                ClearHeldItem();
                return;
            }

            SetHeldItem(itemPrefabData.DisplayName);
            SetHeldItemIcon(itemPrefabData.Icon);
            SetHeldItemDurability(itemPrefabData);
            hudFeedbackController?.PlayHeldItemChanged(true);
        }

        public void ClearHeldItem()
        {
            SetHeldItem(string.Empty);
            SetHeldItemIcon(null);
            SetHeldItemDurability(null);
            hudFeedbackController?.PlayHeldItemChanged(false);
        }

        public void SetInteractionPrompt(string inputLabel, string prompt)
        {
            hudFeedbackController?.SetInteractionPrompt(inputLabel, prompt);
        }

        public void SetGravityWarning(bool isVisible)
        {
            hudFeedbackController?.SetGravityWarning(isVisible);
        }

        public void SetHazardWarning(string message)
        {
            hudFeedbackController?.SetHazardWarning(message);
        }

        public void ClearHazardWarning()
        {
            hudFeedbackController?.ClearHazardWarning();
        }

        public void SetRespawnCountdown(float seconds)
        {
            if (!RequireHudFeedbackController(nameof(SetRespawnCountdown)))
            {
                return;
            }

            hudFeedbackController.SetRespawnCountdown(seconds);
        }

        public void SetWarpRespawnPending()
        {
            if (!RequireHudFeedbackController(nameof(SetWarpRespawnPending)))
            {
                return;
            }

            hudFeedbackController.SetWarpRespawnPending();
        }

        public void ClearRespawnStatus()
        {
            if (!RequireHudFeedbackController(nameof(ClearRespawnStatus)))
            {
                return;
            }

            hudFeedbackController.ClearRespawnStatus();
        }

        private bool RequireHudFeedbackController(string operation)
        {
            if (hudFeedbackController != null)
            {
                return true;
            }

            Debug.LogError($"PHS_HUD_RESPAWN_SETUP_FAILED reason=hud_feedback_controller_missing operation={operation} presenter={name}", this);
            return false;
        }

        public void ShowSpeakingPlayer(string playerName)
        {
            ShowSpeakingPlayer(playerName, speakingPlayerVisibleSeconds);
        }

        public void ShowSpeakingPlayer(string playerName, float visibleSeconds)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                HideSpeakingPlayer();
                return;
            }

            SetSpeakingPlayers(new[] { playerName });
            speakingPlayerHideTime = Time.unscaledTime + Mathf.Max(0.1f, visibleSeconds);
        }

        public void SetSpeakingPlayers(IReadOnlyList<string> playerNames)
        {
            RebuildSpeakingPlayerViews(playerNames);
            speakingPlayerHideTime = float.PositiveInfinity;
        }

        public void HideSpeakingPlayer()
        {
            SetText(speakingPlayerText, string.Empty);
            ClearSpeakingPlayerViews();

            if (speakingPlayerPanel != null)
            {
                speakingPlayerPanel.SetActive(false);
            }
        }

        private void ResetPlaceholders()
        {
            SetVitals(100, 100, 40, 40);
            SetEconomy(0, 0);
            SetShipHp(100, 100);
            SetTimeLimit(0f);
            ClearHeldItem();
            HideSpeakingPlayer();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void SetHeldItemIcon(Sprite icon)
        {
            if (heldItemIconImage == null)
            {
                Debug.LogError($"PHS_HELD_ITEM_UI_FAILED reason=heldItemIconImage_missing target={name}");
                return;
            }

            heldItemIconImage.sprite = icon;
            heldItemIconImage.enabled = true;
            heldItemIconImage.color = icon == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }

        private void SetHeldItemDurability(UtilityItemPrefabData itemPrefabData)
        {
            if (heldItemDurabilityText == null)
            {
                if (itemPrefabData != null && itemPrefabData.HasDurability)
                {
                    Debug.LogError($"PHS_HELD_ITEM_UI_FAILED reason=heldItemDurabilityText_missing target={name} item={itemPrefabData.ItemId}");
                }

                return;
            }

            if (itemPrefabData == null || !itemPrefabData.HasDurability)
            {
                heldItemDurabilityText.gameObject.SetActive(false);
                SetText(heldItemDurabilityText, string.Empty);
                return;
            }

            heldItemDurabilityText.gameObject.SetActive(true);
            SetText(heldItemDurabilityText, $"DUR {itemPrefabData.MaxDurability}/{itemPrefabData.MaxDurability}");
        }

        private void RebuildSpeakingPlayerViews(IReadOnlyList<string> playerNames)
        {
            ClearSpeakingPlayerViews();

            if (speakingPlayerPanel == null || speakingPlayersContent == null || !speakingPlayerTemplate.HasRoot)
            {
                Debug.LogError("PHS_SPEAKING_UI_NOT_READY panel/content/template missing");
                return;
            }

            if (playerNames == null || playerNames.Count == 0)
            {
                speakingPlayerPanel.SetActive(false);
                return;
            }

            speakingPlayerTemplate.SetActive(false);

            for (var i = 0; i < playerNames.Count; i++)
            {
                var playerName = playerNames[i];
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    continue;
                }

                var view = speakingPlayerTemplate.CreateRuntimeView(speakingPlayersContent);
                view.SetActive(true);
                view.Refresh(playerName);
                speakingPlayerViews.Add(view);
            }

            speakingPlayerPanel.SetActive(speakingPlayerViews.Count > 0);
        }

        private void ClearSpeakingPlayerViews()
        {
            for (var i = 0; i < speakingPlayerViews.Count; i++)
            {
                speakingPlayerViews[i].DestroyRuntime();
            }

            speakingPlayerViews.Clear();
        }

        private void RefreshSpeakingPlayerBlink()
        {
            if (speakingPlayerViews.Count == 0)
            {
                return;
            }

            var blink = Mathf.PingPong(Time.unscaledTime * speakingPlayerBlinkSpeed, 1f);

            for (var i = 0; i < speakingPlayerViews.Count; i++)
            {
                speakingPlayerViews[i].SetBlink(blink);
            }
        }

        [System.Serializable]
        private struct SpeakingPlayerView
        {
            [SerializeField] private GameObject root;
            [SerializeField] private TMP_Text nameText;
            [SerializeField] private Image iconImage;

            public bool HasRoot => root != null;

            public SpeakingPlayerView CreateRuntimeView(Transform parent)
            {
                var instance = Instantiate(root, parent);
                instance.name = "Speaking Player";

                return new SpeakingPlayerView
                {
                    root = instance,
                    nameText = FindChildComponent<TMP_Text>(instance.transform, "Speaking Player Name"),
                    iconImage = FindChildComponent<Image>(instance.transform, "Speaking Player Icon")
                };
            }

            public void Refresh(string playerName)
            {
                SetText(nameText, playerName);
                SetBlink(0f);
            }

            public void SetBlink(float value)
            {
                var color = Color.Lerp(new Color(1f, 0.49f, 0f, 0.95f), Color.white, Mathf.Clamp01(value));

                if (nameText != null)
                {
                    nameText.color = color;
                }

                if (iconImage != null)
                {
                    iconImage.color = color;
                }
            }

            public void SetActive(bool active)
            {
                if (root != null)
                {
                    root.SetActive(active);
                }
            }

            public void DestroyRuntime()
            {
                if (root == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    Destroy(root);
                    return;
                }

                DestroyImmediate(root);
            }

            private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
            {
                foreach (Transform child in parent)
                {
                    if (child.name == childName)
                    {
                        return child.GetComponent<T>();
                    }

                    var nested = FindChildComponent<T>(child, childName);
                    if (nested != null)
                    {
                        return nested;
                    }
                }

                return null;
            }
        }
    }
}
