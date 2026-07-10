using UnityEngine;
namespace LastJumpCrew.SeBoGyeong.Economy
{
    /// <summary>
    /// 네트워크에서 공유되는 화폐
    /// </summary>
    public class CreditWallet
    {
        private int credits; // $ : InGame Play의 일반 화폐. 공용.

        public CreditWallet(int initialCredits = 0)
        {
            Credits = initialCredits;
        }

        public int Credits
        {
            get => credits;
            set => credits = Mathf.Max(0, value); // 음수 방지
        }

        public void AddCredits(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[Credit] Add에 음수단위를 지원하지 않음.");
                return;
            }
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
    }
}