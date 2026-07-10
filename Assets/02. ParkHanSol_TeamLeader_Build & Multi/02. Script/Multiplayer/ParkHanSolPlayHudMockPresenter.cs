using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolPlayHudMockPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text bankText;
        [SerializeField] private TMP_Text quotaText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text shipHpText;
        [SerializeField] private TMP_Text timeLimitText;
        [SerializeField] private GameObject speakingPlayerPanel;
        [SerializeField] private TMP_Text speakingPlayerText;
        [SerializeField] private RectTransform speakingPlayersContent;
        [SerializeField] private SpeakingPlayerView speakingPlayerTemplate;
        [SerializeField, Min(0.1f)] private float speakingPlayerVisibleSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float speakingPlayerBlinkSpeed = 6f;
        [SerializeField] private Image warpGaugeFill;
        [SerializeField] private TMP_Text heldItemText;
        [SerializeField] private Image heldItemIconImage;
        [SerializeField] private TMP_Text heldItemDurabilityText;
        [SerializeField] private TMP_Text[] partyFeedTexts;

        private readonly List<SpeakingPlayerView> speakingPlayerViews = new();
        private float speakingPlayerHideTime;

        private void Awake()
        {
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
            SetText(healthText, $"+{health}<size=26>/{maxHealth}</size>");
            SetText(staminaText, $"ST {stamina}<size=24>/{maxStamina}</size>");
        }

        public void SetThrusterFuel(int currentFuel, int maxFuel)
        {
            SetText(staminaText, $"추진제 {currentFuel}<size=24>/{maxFuel}</size>");
        }

        public void SetEconomy(int money, int bank)
        {
            SetText(moneyText, $"${money}");
            SetText(bankText, $"BANK ${bank}");
        }

        public void SetQuota(int current, int target)
        {
            SetText(quotaText, $"{current}<color=#ff7a00>/{target}</color>");
        }

        public void SetSubtitle(string value)
        {
            SetText(subtitleText, value);
        }

        public void SetShipHp(int current, int max)
        {
            SetText(shipHpText, $"SHIP HP {current}<size=24>/{max}</size>");
        }

        public void SetTimeLimit(float seconds)
        {
            var time = Mathf.Max(0, Mathf.CeilToInt(seconds));
            SetText(timeLimitText, $"{time / 60:00}:{time % 60:00}");
        }

        public void SetWarpGauge(float normalizedValue)
        {
            if (warpGaugeFill != null)
            {
                warpGaugeFill.fillAmount = Mathf.Clamp01(normalizedValue);
            }
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
        }

        public void ClearHeldItem()
        {
            SetHeldItem(string.Empty);
            SetHeldItemIcon(null);
            SetHeldItemDurability(null);
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

        public void SetPartyFeedLine(int index, string value)
        {
            if (partyFeedTexts == null || index < 0 || index >= partyFeedTexts.Length)
            {
                return;
            }

            SetText(partyFeedTexts[index], value);
        }

        private void ResetPlaceholders()
        {
            SetVitals(100, 100, 40, 40);
            SetEconomy(0, 0);
            SetQuota(0, 1);
            SetSubtitle(string.Empty);
            SetShipHp(100, 100);
            SetTimeLimit(0f);
            SetWarpGauge(0f);
            ClearHeldItem();
            HideSpeakingPlayer();

            if (partyFeedTexts == null)
            {
                return;
            }

            for (var i = 0; i < partyFeedTexts.Length; i++)
            {
                SetText(partyFeedTexts[i], string.Empty);
            }
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
