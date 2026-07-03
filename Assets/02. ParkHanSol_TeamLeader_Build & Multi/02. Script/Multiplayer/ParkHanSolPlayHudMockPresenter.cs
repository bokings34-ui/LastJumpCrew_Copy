using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolPlayHudMockPresenter : MonoBehaviour
    {
        [SerializeField] private Text healthText;
        [SerializeField] private Text staminaText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text quotaText;
        [SerializeField] private Text subtitleText;

        [SerializeField, Min(1f)] private float tickInterval = 1f;

        private float nextTickTime;
        private int health = 100;
        private int stamina = 40;
        private int money = 0;
        private int quota;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }

            nextTickTime = Time.time + tickInterval;
            health = health <= 74 ? 100 : health - 3;
            stamina = stamina <= 12 ? 40 : stamina - 2;
            money = (money + 7) % 1000;
            quota = quota == 0 ? 1 : 0;
            Refresh();
        }

        private void Refresh()
        {
            SetText(healthText, $"+{health}<size=16>/100</size>");
            SetText(staminaText, $"⚡{stamina}<size=16>/40</size>");
            SetText(moneyText, $"${money}");
            SetText(quotaText, $"{quota}<color=#ff7a00>/1</color>");
            SetText(subtitleText, quota == 0 ? "예..." : "준비됐어");
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
