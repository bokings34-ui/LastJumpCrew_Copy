using System;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    /// <summary>
    /// 지속 재화(Token) 지갑. 플레이어 개인 소지 — 로비에서 치장 아이템 구매에 사용하고
    /// 판 사이에 영구 저장된다(PlayerProfile 이 소유, 세이브 대상).
    /// 인게임 네트워크 상태가 아니다(NetworkVariable 아님).
    /// </summary>
    public class TokenWallet : IWallet
    {
        private int balance;

        public int Balance => balance;

        /// <summary>잔액 변경 시 발행(UI 갱신용).</summary>
        public event Action<int> BalanceChanged;

        public TokenWallet(int initialBalance = 0)
        {
            balance = Mathf.Max(0, initialBalance);
        }

        public void Add(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[Token] Add 에 음수는 사용할 수 없다.");
                return;
            }
            balance += amount;
            BalanceChanged?.Invoke(balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) return false;
            if (balance < amount) return false;

            balance -= amount;
            BalanceChanged?.Invoke(balance);
            return true;
        }
    }
}
