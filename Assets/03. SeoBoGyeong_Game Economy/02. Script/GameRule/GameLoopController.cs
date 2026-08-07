using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 루프 전이 규칙(순수 클래스, SRP). 상태(GameLoopState)와 목/실제 의존을 주입받아 규칙만 처리한다.
    /// 권위측(서버)에서만 호출되는 것을 전제로 한다(호출자가 IAuthority 로 게이트).
    /// 제한시간 카운트다운은 공용 CountdownTimer 로 위임한다.
    ///
    /// 클리어: 플레이(Play) 단계에서 플레이어가 점프 버튼 -> TryJump()
    ///   (제한시간>0 && 함선 생존) 이면 클리어 수 +1 후 다음 단계 결정:
    ///     - 4구역 클리어 -> GameClear
    ///     - 4구역마다     -> Shop -> (상점 종료 후) ZoneSelect
    ///     - 그 외          -> ZoneSelect
    /// 실패: 제한시간 초과(TickStageTimer) / 외부 보고(ForceGameOver) -> GameOver.
    /// </summary>
    public class GameLoopController
    {
        private readonly GameLoopState loop;
        private readonly IShipStatus ship;
        private readonly IDeathEventGate deathGate;
        private readonly CountdownTimer stageTimer = new();
        private bool stageTimerPaused;

        public GameLoopController(GameLoopState loop, IShipStatus ship, IDeathEventGate deathGate)
        {
            this.loop = loop;
            this.ship = ship;
            this.deathGate = deathGate;
        }

        /// <summary>게임 시작: 클리어 수 초기화 후 첫 구역 선택 단계로.</summary>
        public void StartGame()
        {
            loop.ClearedZoneCount = 0;
            loop.SelectedZoneId = 0;
            loop.StageTimeRemaining = 0f;
            loop.LastGameOverReason = GameOverReason.None;
            stageTimer.Stop();
            stageTimerPaused = false;
            SetPhase(GamePhase.ZoneSelect);
        }

        /// <summary>구역 선택 완료 -> 플레이 시작. 제한시간을 고정값으로 초기화.</summary>
        public void OnZoneSelected(int zoneId)
        {
            loop.SelectedZoneId = zoneId;
            stageTimerPaused = false;
            stageTimer.Start(GameLoopState.STAGE_TIME_LIMIT);
            loop.StageTimeRemaining = stageTimer.Remaining;
            SetPhase(GamePhase.Play);
        }

        /// <summary>
        /// 서버 tick: 플레이 단계에서 제한시간을 감소시킨다(카운트다운은 CountdownTimer 위임).
        /// 방금 만료 시 게임오버(TimeOver) + 확정 사망 이벤트 실행.
        /// 반환값: 상태가 바뀌었으면 true(호출자가 변경 이벤트 발행).
        /// </summary>
        public bool TickStageTimer(float deltaTime)
        {
            if (loop.Phase != GamePhase.Play) return false;
            if (stageTimerPaused) return false;
            if (!stageTimer.IsRunning) return false;

            bool justExpired = stageTimer.Tick(deltaTime);
            loop.StageTimeRemaining = stageTimer.Remaining;   // 상태 미러링(나중 NetworkVariable)

            if (justExpired)
            {
                var shipSystems = NetworkShipSystemsState.Instance;
                var damageReason = "ship_systems_missing";
                if (shipSystems != null
                    && shipSystems.TryApplyShipDamage(999, "stage_timeout", out damageReason))
                {
                    Debug.Log("PHS_STAGE_TIMEOUT_SHIP_DAMAGE_APPLIED amount=999");
                    return true;
                }

                Debug.LogError($"PHS_STAGE_TIMEOUT_SHIP_DAMAGE_FAILED reason={damageReason}");
                GameOver(GameOverReason.TimeOver);
                deathGate.ExecuteConfirmedDeath();
            }
            return true;
        }

        public void SetStageTimerPaused(bool paused)
        {
            stageTimerPaused = paused;
        }

        /// <summary>
        /// 점프(워프) 버튼: 제한시간 내 && 함선 생존이면 클리어. 아니면 거부(false).
        /// 반환값: 클리어 처리(상태 변경)됐으면 true.
        /// </summary>
        public bool TryJump()
        {
            if (loop.Phase != GamePhase.Play) return false;
            if (stageTimer.IsExpired) return false;
            if (!ship.IsShipAlive) return false;

            stageTimer.Stop();
            loop.ClearedZoneCount++;
            if (loop.IsGameClear) SetPhase(GamePhase.GameClear);
            else if (loop.IsShopDue) SetPhase(GamePhase.Shop);
            else SetPhase(GamePhase.ZoneSelect);
            return true;
        }

        /// <summary>상점 종료 -> 다음 구역 선택.</summary>
        public void OnShopClosed() => SetPhase(GamePhase.ZoneSelect);

        /// <summary>외부(함선 파괴/전멸) 실패 보고 -> 게임오버.</summary>
        public void ForceGameOver(GameOverReason reason) => GameOver(reason);

        private void GameOver(GameOverReason reason)
        {
            stageTimer.Stop();
            loop.LastGameOverReason = reason;
            SetPhase(GamePhase.GameOver);
        }

        private void SetPhase(GamePhase phase)
        {
            loop.Phase = phase;
            Debug.Log($"[GameLoop] Phase={phase}, Cleared={loop.ClearedZoneCount}, Time={loop.StageTimeRemaining:F1}");
        }
    }
}
