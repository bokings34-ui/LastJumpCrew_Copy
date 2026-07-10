using UnityEngine;
namespace LastJumpCrew.SeBoGyeong.Economy
{
    /// <summary>
    /// 플레이어 개인이 소유하는 화폐
    /// 치장 아이템 구매용도?
    /// </summary>
    public class TokenWallet
    {
        private int tokens; // 특수 화폐 . 치장 아이템용(?). 개별

        public TokenWallet(int initialTokens = 0)
        {
            Tokens = initialTokens;
        }

        public int Tokens
        {
            get => tokens;
            set => tokens = Mathf.Max(0, value);
        }

        public void AddTokens(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[Token] Add에 음수단위를 지원하지 않음.");
                return;
            }
            Tokens += amount;
        }

        public bool SpendTokens(int amount)
        {
            if (Tokens >= amount)
            {
                Tokens -= amount;
                return true;
            }
            return false;
        }
    }
}
