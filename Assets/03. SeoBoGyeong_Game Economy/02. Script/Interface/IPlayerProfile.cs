using System;
using System.Collections.Generic;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 지속(Persistent) 데이터의 접근 창구 — Token / 보유 치장 아이템.
    /// 플레이어 개인 소지, 판 사이 영구 저장(로컬 세이브). 인게임 네트워크 상태가 아니다.
    /// 접근: GameCore.Instance.Profile
    /// </summary>
    public interface IPlayerProfile
    {
        /// <summary>보유 Token(메타 치장 재화).</summary>
        int Tokens { get; }

        /// <summary>보유 중인 치장 아이템 Id 목록.</summary>
        IReadOnlyCollection<int> OwnedCosmetics { get; }

        /// <summary>Token 추가(음수 불가). 변경 즉시 저장.</summary>
        void AddTokens(int amount);

        /// <summary>잔액이 충분하면 차감 후 true(즉시 저장). 부족하면 차감 없이 false.</summary>
        bool TrySpendTokens(int amount);

        /// <summary>치장 아이템 해금(중복 해금은 무시). 변경 즉시 저장.</summary>
        void UnlockCosmetic(int cosmeticId);

        /// <summary>해당 치장 아이템 보유 여부.</summary>
        bool HasCosmetic(int cosmeticId);

        /// <summary>프로필이 바뀔 때마다 발행(로비 UI 갱신용).</summary>
        event Action ProfileChanged;
    }
}
