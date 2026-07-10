using UnityEngine;
using UnityEngine.InputSystem; // 최신 입력 시스템 사용
using LastJumpCrew.Common;

// 👇 유니티가 MiniGameType을 인식할 수 있도록 목록(enum)을 여기에 명시합니다.
public enum MiniGameType
{
    DoorKeypad, // 문 장치 조작
    WireFix,    // 전기 선 복구
    PowerSync,  // 전력 맞추기
    Cannon      // 레이저 대포
}

public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance;

    [Header("UI 연결")]
    public GameObject canvasRoot; // 미니게임 캔버스 전체
    public MiniGameBase[] miniGames; // 미니게임 패널들 연결할 배열

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        canvasRoot.SetActive(false); // 시작할 땐 꺼둡니다.
    }

    private void Update()
    {
        // 키보드가 연결되어 있지 않으면 작동하지 않음
        if (Keyboard.current == null) return;

        // [숫자 1키]를 누르면 -> '문 장치 조작' 강제 실행
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("테스트: 숫자 1 눌림 -> 문 장치 조작 실행");
            OpenMiniGame(MiniGameType.DoorKeypad, null);
        }

        // [숫자 2키]를 누르면 -> '전기 선 복구' 강제 실행
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("테스트: 숫자 2 눌림 -> 전기 선 복구 실행");
            OpenMiniGame(MiniGameType.WireFix, null);
        }
    }

    public void OpenMiniGame(MiniGameType type, IMiniGameTarget target)
    {
        canvasRoot.SetActive(true);

        foreach (var mg in miniGames)
        {
            if (mg.gameType == type)
            {
                mg.gameObject.SetActive(true);
                mg.StartGame(target);
            }
            else
            {
                mg.gameObject.SetActive(false);
            }
        }
    }

    public void CloseAll()
    {
        canvasRoot.SetActive(false);
    }
}