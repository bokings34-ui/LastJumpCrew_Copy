using System;

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
    }
}
