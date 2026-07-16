using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 서버 권위 상태의 읽기 전용 창구. 소비자(UI·플레이어·이벤트)는 여기서 읽기만 한다.
    /// 지금은 plain 필드 getter, 나중 NGO 연결 시 NetworkVariable.Value 로 매핑.
    /// StateChanged 는 나중 NetworkVariable.OnValueChanged 로 매핑한다.
    /// </summary>
    public interface IGameStateProvider
    {
        GamePhase Phase { get; }
        int ClearedZoneCount { get; }
        int SelectedZoneId { get; }
        float StageTimeRemaining { get; }
        GameOverReason LastGameOverReason { get; }

        /// <summary>상태가 바뀔 때마다 발행(UI 갱신용).</summary>
        event Action StateChanged;

        /// <summary>
        /// 구매 요청(RequestPurchase)의 결과 통지 — (itemId, 성공 여부).
        /// 로컬에선 동기 발행, 나중 NGO 연결 시 ClientRpc 수신으로 매핑한다.
        /// </summary>
        event Action<List<int>, bool> PurchaseResolved;
    }
}
