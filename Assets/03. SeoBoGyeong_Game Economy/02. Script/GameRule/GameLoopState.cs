namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 루프의 단계.
    /// 순서: 구역선택(ZoneSelect) -> 플레이(Play) -> 사고(Disaster) -> 점프(Jump).
    /// 조건부: 3구역마다 상점(Shop), 9구역 클리어 시 게임 클리어(GameClear).
    /// </summary>
    public enum GamePhase { ZoneSelect, Play, Disaster, Jump, Shop, GameClear }

    /// <summary>
    /// 게임 루프의 런타임 상태(데이터만 보관). 전이 규칙은 GameLoopController 가 처리(SRP).
    /// [SYNC] 표시 필드는 NGO 병합 후 NetworkVariable 로 승격해 서버 권위 동기화 대상이 된다.
    /// </summary>
    public class GameLoopState
    {
        public const int SHOP_INTERVAL = 3;   // 3구역 클리어마다 상점
        public const int TOTAL_ZONES = 9;   // 9구역 클리어 시 게임 클리어

        // [SYNC] 아래 3개는 NGO 병합 후 서버 권위 동기화 대상
        public GamePhase Phase;
        public int ClearedZoneCount;   // 점프 성공 시 +1 (0~9)
        public int SelectedZoneId;     // 현재 선택된 구역

        /// <summary>9구역을 모두 클리어했는가.</summary>
        public bool IsGameClear => ClearedZoneCount >= TOTAL_ZONES;

        /// <summary>이번 점프 직후 상점에 들러야 하는가(3구역마다, 단 게임 클리어 제외).</summary>
        public bool IsShopDue => ClearedZoneCount > 0
                              && ClearedZoneCount % SHOP_INTERVAL == 0
                              && !IsGameClear;
    }
}
