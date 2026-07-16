using UnityEngine;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;

// 💡 [핵심] 미니게임처럼 IInteractable 명찰을 똑같이 달아줍니다!
public class WarpTerminal : MonoBehaviour, LastJumpCrew.ParkHanSol.Interaction.IInteractable, LastJumpCrew.Common.IInteractable
{
    [Header("상호작용 안내 문구")]
    [SerializeField] private string promptText = "워프 장치 가동하기";

    private bool isWarping = false; // 현재 워프 상태 저장

    // 플레이어가 가까이 가면 화면에 띄워줄 문구
    public string InteractionPrompt => promptText;

    // ==========================================
    // 1️⃣ 팀장님 전용 상호작용 규칙 (F키 누르면 여기가 실행됨!)
    // ==========================================
    public bool CanInteract(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder) => true;

    public void Interact(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder)
    {
        ToggleWarpEffect();
    }

    // ==========================================
    // 2️⃣ 공용(Common) 상호작용 규칙
    // ==========================================
    bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder) => true;

    void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
    {
        ToggleWarpEffect();
    }

    // ==========================================
    // 🚀 워프 켜고 끄는 핵심 로직
    // ==========================================
    private void ToggleWarpEffect()
    {
        isWarping = !isWarping; // 상태 뒤집기 (켬 <-> 끔)

        if (WarpManager.Instance != null)
        {
            if (isWarping)
            {
                WarpManager.Instance.StartWarp();
                Debug.Log("워프 가동!");
            }
            else
            {
                WarpManager.Instance.StopWarp();
                Debug.Log("워프 중지!");
            }
        }
    }
}