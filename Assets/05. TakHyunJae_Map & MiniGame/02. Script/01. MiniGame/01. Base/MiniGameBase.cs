using UnityEngine;
using LastJumpCrew.Common; // 팀장님의 공통 네임스페이스 적용

public abstract class MiniGameBase : MonoBehaviour
{
    public MiniGameType gameType;

    // 미니게임 결과를 돌려받을 3D 월드의 장치
    protected IMiniGameTarget currentTarget;

    public virtual void StartGame(IMiniGameTarget target)
    {
        currentTarget = target;
        if (currentTarget != null)
        {
            Debug.Log($"[{currentTarget.MiniGameTargetId}] 장치의 미니게임 시작됨.");
        }
        else
        {
            Debug.Log($"{gameType} UI 단독 테스트 모드 시작 (Target: null)");
        }
    }

    // 👇 이 함수가 누락되었거나 public이 아니면 매니저에서 빨간 줄이 뜹니다!
    public void ForceFail()
    {
        Debug.Log($"{gameType} 게임 강제 종료 감지.");
        GameFail(); // 밑에 있는 실패 로직을 그대로 실행시킵니다.
    }

    // 미니게임 내부에서 성공 조건을 달성했을 때 호출
    protected void GameSucceed()
    {
        if (currentTarget != null) currentTarget.OnMiniGameSucceeded();
        MiniGameManager.Instance.EndMiniGame(true); // 매니저의 성공 연출(초록색) 호출
    }

    // 미니게임 내부에서 실패했을 때 호출
    protected void GameFail()
    {
        if (currentTarget != null) currentTarget.OnMiniGameFailed();
        MiniGameManager.Instance.EndMiniGame(false); // 매니저의 실패 연출(빨간색) 호출
    }
}