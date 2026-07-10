namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 함선 생존 여부 조회. 점프 검사(생존)와 실패(파괴) 판정에 쓰인다.
    /// 지금은 목(항상 생존). 나중 실제 함선의 LastJumpCrew.Common.IDamageable.IsAlive 로 연결(협의 A7).
    /// </summary>
    public interface IShipStatus
    {
        bool IsShipAlive { get; }
    }
}
