using UnityEngine;
using LastJumpCrew.Common; // 팀장님의 공통 네임스페이스 적용

public abstract class MiniGameBase : MonoBehaviour
{
    public MiniGameType gameType;

    // 미니게임 결과를 돌려받을 3D 월드의 장치 (문, 배전반 등)
    protected IMiniGameTarget currentTarget;

    // 미니게임이 켜질 때 매니저가 호출해 주는 함수
    public virtual void StartGame(IMiniGameTarget target)
    {
        currentTarget = target;

        // target이 null인지 체크하는 방어 코드 추가 (T키 단독 테스트용)
        if (currentTarget != null)
        {
            Debug.Log($"[{currentTarget.MiniGameTargetId}] 장치의 미니게임 시작됨.");
        }
        else
        {
            // 3D 장치 없이 UI만 켰을 때 발생하는 에러 방지
            Debug.Log("UI 단독 테스트 모드로 미니게임이 시작됨 (Target: null)");
        }
    }

    // 미니게임 내부에서 성공 조건을 달성했을 때 호출
    protected void GameSucceed()
    {
        if (currentTarget != null)
        {
            currentTarget.OnMiniGameSucceeded(); // 팀장님 인터페이스의 성공 함수 호출
        }
        MiniGameManager.Instance.CloseAll(); // 미니게임 창 닫기
    }

    // 미니게임 내부에서 실패(시간초과, 오답 등)했을 때 호출
    protected void GameFail()
    {
        if (currentTarget != null)
        {
            currentTarget.OnMiniGameFailed(); // 팀장님 인터페이스의 실패 함수 호출
        }
        MiniGameManager.Instance.CloseAll(); // 미니게임 창 닫기
    }
}