using UnityEngine;
using UnityEngine.Events;
using LastJumpCrew.Common; // 팀원의 인터페이스가 있는 네임스페이스

// 💡 큐브가 미니게임 타겟이자, 동시에 플레이어가 F키로 누를 수 있는 오브젝트가 됩니다.
public class MiniGameTerminal : MonoBehaviour, IMiniGameTarget, IInteractable
{
    public string MiniGameTargetId => gameObject.name;

    [Header("연결할 미니게임 (UI 패널)")]
    public MiniGameBase targetMiniGame;

    [Header("상호작용 설정")]
    [SerializeField] private string promptText = "단말기 해킹하기";

    [Header("팀원 협업용 이벤트")]
    public UnityEvent onMiniGameOpened; // 미니게임 켜질 때 호출 (이동 정지용)
    public UnityEvent onMiniGameClosed; // 미니게임 꺼질 때 호출 (이동 복구용)

    // ==========================================
    // 🛠️ 팀원의 IInteractable 인터페이스 구현부
    // ==========================================

    // 1. 화면에 띄울 안내 문구
    public string InteractionPrompt => promptText;

    // 2. 상호작용 가능 여부 체크
    public bool CanInteract(IItemHolder itemHolder)
    {
        // 💡 나중에 퓨즈나 카드키가 있어야만 열리게 하려면 여기서 itemHolder를 검사하면 됩니다!
        // 지금은 다가가서 F를 누르면 무조건 작동하도록 true를 반환합니다.
        return true;
    }

    // 3. F키를 눌렀을 때 팀원의 플레이어 코드가 실행시켜 줄 핵심 함수!
    public void Interact(IItemHolder itemHolder)
    {
        // 미니게임이 없거나 이미 켜져 있으면 무시
        if (targetMiniGame == null || targetMiniGame.gameObject.activeSelf) return;

        // 미니게임 UI 켜고 시작 (성공/실패 결과를 이 큐브가 받도록 this 전달)
        targetMiniGame.gameObject.SetActive(true);
        targetMiniGame.StartGame(this);

        // 다른 팀원의 스크립트(마우스 락 해제, 이동 정지 등)가 인스펙터에서 실행되도록 신호
        onMiniGameOpened?.Invoke();
    }

    // ==========================================
    // 🏆 미니게임 결과 수신부 (IMiniGameTarget)
    // ==========================================

    public void OnMiniGameSucceeded()
    {
        Debug.Log($"{gameObject.name} 해킹 성공! 전력이 복구되거나 문이 열립니다.");
        GetComponent<Renderer>().material.color = Color.green; // 성공 시 초록색 변신
        CloseTerminal();
    }

    public void OnMiniGameFailed()
    {
        Debug.Log($"{gameObject.name} 해킹 실패! 시스템이 다운됩니다.");
        GetComponent<Renderer>().material.color = Color.red; // 실패 시 빨간색 변신
        CloseTerminal();
    }

    private void CloseTerminal()
    {
        // 미니게임 끄기
        targetMiniGame.gameObject.SetActive(false);

        // 팀원의 스크립트(다시 1인칭 시점 복구, 조작 활성화 등) 실행 신호
        onMiniGameClosed?.Invoke();
    }
}