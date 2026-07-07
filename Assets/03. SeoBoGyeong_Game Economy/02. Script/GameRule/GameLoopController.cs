using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 루프 전이 규칙을 처리한다.
    /// 상태 객체(GameLoopState)는 데이터만 갖고, 규칙/전이는 여기서 담당한다(SRP).
    /// 순서: 구역선택 -> 플레이 -> 사고 -> 점프. 점프 성공 시 클리어 수 +1 후 다음 단계 결정:
    ///   - 9구역 클리어 -> GameClear
    ///   - 3구역마다     -> Shop -> (상점 종료 후) ZoneSelect
    ///   - 그 외          -> ZoneSelect
    /// [SYNC] 실제 진행 판정은 NGO 병합 후 서버 권위로 처리해야 한다(클라이언트 임의 진행 방지).
    /// </summary>
    public class GameLoopController : MonoBehaviour
    {
        // 허브 싱글톤이 보유한 런타임 상태를 참조한다.
        private GameLoopState Loop => GameCore.Instance.Loop;

        /// <summary>게임 시작: 클리어 수 초기화 후 첫 구역 선택 단계로.</summary>
        public void StartGame()
        {
            Loop.ClearedZoneCount = 0;
            SetPhase(GamePhase.ZoneSelect);
        }

        /// <summary>구역 선택 완료 -> 플레이 시작.</summary>
        public void OnZoneSelected(int zoneId)
        {
            Loop.SelectedZoneId = zoneId;
            SetPhase(GamePhase.Play);
        }

        /// <summary>사고 발생 단계로.</summary>
        public void OnDisaster() => SetPhase(GamePhase.Disaster);

        /// <summary>점프 단계로.</summary>
        public void OnJump() => SetPhase(GamePhase.Jump);

        /// <summary>점프 성공 처리: 클리어 수 +1 후 다음 단계 결정.</summary>
        public void OnJumpCompleted()
        {
            Loop.ClearedZoneCount++;

            if (Loop.IsGameClear) SetPhase(GamePhase.GameClear);
            else if (Loop.IsShopDue) SetPhase(GamePhase.Shop);
            else SetPhase(GamePhase.ZoneSelect);
        }

        /// <summary>상점 종료 -> 다음 구역 선택.</summary>
        public void OnShopClosed() => SetPhase(GamePhase.ZoneSelect);

        private void SetPhase(GamePhase phase)
        {
            Loop.Phase = phase;
            Debug.Log($"[GameLoop] Phase={phase}, Cleared={Loop.ClearedZoneCount}");
            // TODO(NGO 병합 후): 서버에서 Phase/ClearedZoneCount 동기화 (NetworkVariable / RPC)
        }
    }
}
