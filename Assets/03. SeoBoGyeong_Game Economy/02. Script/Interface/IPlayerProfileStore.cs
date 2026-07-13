namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 지속 데이터 저장/로드 계약. 세이브 방식 교체 지점 —
    /// 1차 구현은 JsonPlayerProfileStore(로컬 JSON), 나중 Unity Cloud Save 등으로 교체 시
    /// 이 인터페이스 구현만 갈아끼운다(소비자 코드 무변경).
    /// </summary>
    public interface IPlayerProfileStore
    {
        /// <summary>세이브 로드. 세이브가 없거나 손상됐으면 새 기본값을 반환한다(null 반환 금지).</summary>
        PlayerProfileData Load();

        /// <summary>세이브 저장.</summary>
        void Save(PlayerProfileData data);
    }
}
