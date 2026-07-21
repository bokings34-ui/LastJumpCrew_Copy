using System.Collections.Generic;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 클라이언트 의도(명령). 상태를 직접 바꾸지 않고 권위측에 요청만 한다.
    /// 지금은 즉시 실행, 나중 NGO 연결 시 각 메서드를 [ServerRpc] 로 매핑한다.
    /// </summary>
    public interface IGameCommands
    {
        void StartGame();
        void SelectZone(int zoneId);
        void RequestJump();                          // 점프(워프) 버튼
        void SetStageTimerPaused(bool paused);       // 안전구역 재정비 중 스테이지 타이머 정지
        void CloseShop();
        void ReportGameOver(GameOverReason reason);  // 함선 파괴/전멸 등 외부 실패 보고

        /// <summary>
        /// 상점 구매 요청. 권위측이 가격·잔액을 검증해 Credit 차감 후
        /// 결과를 IGameStateProvider.PurchaseResolved 로 알린다.
        /// </summary>
        void RequestPurchase(List<int> itemId);
    }
}
