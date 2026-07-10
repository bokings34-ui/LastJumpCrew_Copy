using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>확정 사망 이벤트 목 — 로그만 남긴다. 협의(A3) 후 실제 연출/이벤트로 교체.</summary>
    public sealed class MockDeathEventGate : IDeathEventGate
    {
        public void ExecuteConfirmedDeath()
        {
            Debug.Log("[DeathEvent] 확정 사망 이벤트 실행(목): 제한시간 초과로 탈출 실패");
        }
    }
}
