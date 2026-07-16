namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 루프의 단계.
    /// 순서: 구역선택(ZoneSelect) -> 플레이/생존(Play) -> 점프(플레이어 버튼). 조건부: 상점(Shop).
    /// 종료: 9구역 클리어 시 GameClear, 실패 시 GameOver.
    /// (Disaster/Jump 값은 향후 확장용으로 남겨둔다.)
    /// </summary>
    public enum GamePhase { ZoneSelect, Play, Disaster, Jump, Shop, GameClear, GameOver }

    /// <summary>게임오버 사유. 함선 파괴 / 크루 전멸 / 제한시간 초과.</summary>
    public enum GameOverReason { ShipDestroyed, CrewWipedOut, TimeOver }

    /// <summary>
    /// 게임 루프의 런타임 상태(데이터만 보관). 전이 규칙은 GameLoopController 가 처리(SRP).
    /// [SYNC] 표시 필드는 나중 NGO 연결 시 NetworkVariable 로 승격해 서버 권위 동기화 대상이 된다.
    /// </summary>
    public class GameLoopState
    {
        public const int SHOP_INTERVAL = 4;         // 4구역 클리어마다 상점
        public const int TOTAL_ZONES = 9;           // 9구역 클리어 시 게임 클리어
        public const float STAGE_TIME_LIMIT = 300f; // 스테이지 제한시간(고정, 초)

        // [SYNC] 아래 필드들은 나중 NGO 연결 시 서버 권위 동기화 대상
        public GamePhase Phase;
        public int ClearedZoneCount;              // 클리어(점프 성공) 시 +1 (0~9)
        public int SelectedZoneId;                // 현재 선택된 구역
        public float StageTimeRemaining;          // 남은 제한시간(데드라인). 서버만 감소
        public GameOverReason LastGameOverReason; // 마지막 게임오버 사유

        /// <summary>9구역을 모두 클리어했는가.</summary>
        public bool IsGameClear => ClearedZoneCount >= TOTAL_ZONES;

        /// <summary>이번 클리어 직후 상점에 들러야 하는가(3구역마다, 단 게임 클리어 제외).</summary>
        public bool IsShopDue => ClearedZoneCount > 0
                              && ClearedZoneCount % SHOP_INTERVAL == 0
                              && !IsGameClear;
    }
}
