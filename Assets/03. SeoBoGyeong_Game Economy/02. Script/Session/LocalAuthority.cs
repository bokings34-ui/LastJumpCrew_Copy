namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>단일 머신(로컬) 권위. 항상 서버로 취급. 나중 NGO 구현체로 교체.</summary>
    public sealed class LocalAuthority : IAuthority
    {
        public bool IsServer => true;
    }
}
