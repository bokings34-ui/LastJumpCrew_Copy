namespace LastJumpCrew.Common
{
    /// <summary>
    /// ScriptableObject 데이터가 공통 ID를 갖도록 하는 최소 규칙입니다.
    /// 아이템, 구역, 이벤트, 상점 데이터 조회에 사용합니다.
    /// </summary>
    public interface IGameData
    {
        /// <summary>
        /// 데이터 식별 번호입니다.
        /// </summary>
        int Id { get; }
    }
}
