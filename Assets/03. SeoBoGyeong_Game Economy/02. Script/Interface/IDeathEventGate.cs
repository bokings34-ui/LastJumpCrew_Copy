namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 제한시간 초과 시 실행할 "확정 사망 이벤트" 진입점.
    /// 지금은 목(로그). 나중 노석민 이벤트(EventId 스폰) 또는 자체 연출로 연결(협의 A3).
    /// </summary>
    public interface IDeathEventGate
    {
        void ExecuteConfirmedDeath();
    }
}
