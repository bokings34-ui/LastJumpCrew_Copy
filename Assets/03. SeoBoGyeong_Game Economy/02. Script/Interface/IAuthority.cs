namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 권위(누가 상태를 바꿀 수 있는가) 판정.
    /// 지금 로컬 구현은 항상 true. 나중 NGO 연결 시 NetworkBehaviour.IsServer 로 매핑한다.
    /// </summary>
    public interface IAuthority
    {
        bool IsServer { get; }
    }
}
