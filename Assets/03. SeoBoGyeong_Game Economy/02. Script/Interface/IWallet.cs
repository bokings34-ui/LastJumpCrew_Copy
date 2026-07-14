using System;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 재화 지갑의 공용 계약. 소비자(상점·UI)는 구체 지갑 대신 이 인터페이스만 참조한다.
    /// - Credit 지갑: 런타임 세션 소유(판 안에서만 존재, 파티 공유). 나중 NGO 연결 시 NetworkVariable&lt;int&gt; 로 승격.
    /// - Token 지갑: PlayerProfile 소유(개인, 판 사이 영구 저장).
    /// </summary>
    public interface IWallet
    {
        /// <summary>현재 잔액.</summary>
        int Balance { get; }

        /// <summary>잔액이 충분하면 차감 후 true, 부족하면 차감 없이 false.</summary>
        bool TrySpend(int amount);

        /// <summary>재화 추가(음수 불가).</summary>
        void Add(int amount);

        /// <summary>잔액 변경 시 발행(UI 갱신용). 나중 NetworkVariable.OnValueChanged 로 매핑.</summary>
        event Action<int> BalanceChanged;
    }
}
