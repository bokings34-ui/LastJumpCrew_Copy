using UnityEngine;
using UnityEngine.Events;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;

public class MiniGameTerminal : MonoBehaviour, IMiniGameTarget, LastJumpCrew.ParkHanSol.Interaction.IInteractable, LastJumpCrew.Common.IInteractable
{
    public string MiniGameTargetId => gameObject.name;

    [Header("연결할 미니게임 타입 선택")]
    public MiniGameType miniGameType;

    [Header("상호작용 안내 문구")]
    [SerializeField] private string promptText = "단말기 조작하기";

    [Header("팀원 협업용 이벤트")]
    public UnityEvent onMiniGameOpened;
    public UnityEvent onMiniGameClosed;

    private NetworkPlayerController activePlayer;

    public string InteractionPrompt => promptText;

    // ==========================================
    // 1️⃣ 팀장님 전용 상호작용 규칙 
    // ==========================================
    public bool CanInteract(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder) => true;

    public void Interact(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder)
    {
        if (itemHolder is Component component)
        {
            activePlayer = component.GetComponent<NetworkPlayerController>();
            LockPlayerAndFixCamera();
        }
        OpenTerminal();
    }

    // ==========================================
    // 2️⃣ 공용(Common) 상호작용 규칙
    // ==========================================
    bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder) => true;

    void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
    {
        if (itemHolder is Component component)
        {
            activePlayer = component.GetComponent<NetworkPlayerController>();
            LockPlayerAndFixCamera();
        }
        OpenTerminal();
    }

    // ==========================================
    // 💡 [핵심] 플레이어 정지 + 카메라/마우스 강제 활성화 
    // ==========================================
    private void LockPlayerAndFixCamera()
    {
        if (activePlayer == null) return;

        // 1. 플레이어 조작 차단 (팀장님 코드 실행)
        activePlayer.SetGameplayInputEnabled(false);

        // 2. 오프라인 버그 방어: 꺼져버린 카메라 강제 켜기
        Camera playerCam = activePlayer.GetComponentInChildren<Camera>(true);
        if (playerCam != null) playerCam.enabled = true;

        // 3. 마우스 커서 강제 활성화 (클릭 조작 가능하게!)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ==========================================
    // 🚀 실제 미니게임 켜기 로직
    // ==========================================
    private void OpenTerminal()
    {
        if (MiniGameManager.Instance != null)
        {
            MiniGameManager.Instance.OpenMiniGame(miniGameType, this);
            onMiniGameOpened?.Invoke();
        }
    }

    // ==========================================
    // 🏆 미니게임 결과 수신부
    // ==========================================
    public void OnMiniGameSucceeded()
    {
        if (GetComponent<Renderer>() != null)
            GetComponent<Renderer>().material.color = Color.green;
        CloseTerminal();
    }

    public void OnMiniGameFailed()
    {
        if (GetComponent<Renderer>() != null)
            GetComponent<Renderer>().material.color = Color.red;
        CloseTerminal();
    }

    private void CloseTerminal()
    {
        if (activePlayer != null)
        {
            // 1. 플레이어 조작 복구
            activePlayer.SetGameplayInputEnabled(true);

            // 💡 2. 종료 후 카메라 꺼짐 버그 완벽 방어: 카메라 다시 강제 켜기
            Camera playerCam = activePlayer.GetComponentInChildren<Camera>(true);
            if (playerCam != null) playerCam.enabled = true;

            // 💡 3. 마우스 커서 다시 숨기기 및 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            activePlayer = null;
        }

        onMiniGameClosed?.Invoke();
    }
}