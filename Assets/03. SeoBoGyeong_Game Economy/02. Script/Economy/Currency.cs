using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    public class Currency
    {
        private int credits; // $ : InGame Play의 일반 화폐. 공용.
        private int tokens; // 특수 화폐 . 치장 아이템용(?). 개별

        public int Credits
        {
            get => credits;
            set => credits = Mathf.Max(0, value); // 음수 방지
        }

        public int Tokens
        {
            get => tokens;
            set => tokens = Mathf.Max(0, value);
        }

        public Currency(int initialCredits = 0, int initialTokens = 0)
        {
            Credits = initialCredits;
            Tokens = initialTokens;
        }

        public void AddCredits(int amount)
        {
            Credits += amount;
        }

        public bool SpendCredits(int amount)
        {
            if (Credits >= amount)
            {
                Credits -= amount;
                return true;
            }
            return false;
        }

        public void AddTokens(int amount)
        {
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

