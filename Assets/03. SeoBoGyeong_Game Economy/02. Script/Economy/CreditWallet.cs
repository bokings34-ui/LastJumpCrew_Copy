using System;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    /// <summary>
    /// 런타임 세션 재화(Credit) 지갑. 판 안에서만 존재하는 파티 공유 재화 — 세션이 소유하고
    /// StartGame() 시 초기화, 세션 종료 시 소멸한다(저장하지 않음).
    /// 나중 NGO 연결 시 NetworkGameSession 에서 NetworkVariable&lt;int&gt; 로 승격한다.
    /// </summary>
    public class CreditWallet : IWallet
    {
        private int balance;

        public int Balance => balance;

        /// <summary>잔액 변경 시 발행(UI 갱신용).</summary>
        public event Action<int> BalanceChanged;

        public CreditWallet(int initialBalance = 0)
        {
            balance = Mathf.Max(0, initialBalance);
        }

        public void Add(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[Credit] Add 에 음수는 사용할 수 없다.");
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

        /// <summary>잔액을 지정값으로 재설정. 세션 StartGame() 초기화 전용(소유자만 호출).</summary>
        public void ResetBalance(int value)
        {
            balance = Mathf.Max(0, value);
            BalanceChanged?.Invoke(balance);
        }
    }
}
