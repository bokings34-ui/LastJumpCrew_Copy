using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using LastJumpCrew.Common;

// 1. 매니저가 사용할 미니게임 종류 정의
public enum MiniGameType
{
    DoorKeypad, // 1번 키
    WireFix,    // 2번 키
    PowerSync,  // 3번 키
    Cannon      // 4번 키
}

public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance;

    [Header("UI 연결")]
    public GameObject canvasRoot;       // 미니게임들 담고 있는 캔버스 패널
    public MiniGameBase[] miniGames;    // 각 미니게임 스크립트 연결

    [Header("결과 피드백 연출")]
    public Image flashScreen;           // 성공/실패 점멸용 이미지

    [Header("애니메이션 설정")]
    public float slideDuration = 0.25f; // 위에서 내려오는 속도

    private MiniGameBase activeGame = null;
    private bool isFlashing = false;    // 연출 중 입력 방지용
    private Coroutine slideCoroutine = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 초기 세팅: 캔버스 끄고 대기
        canvasRoot.SetActive(false);
        if (flashScreen != null) flashScreen.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 입력 시스템: 1~4번 키 처리 및 ESC 취소
        if (Keyboard.current == null || isFlashing) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) HandleInput(MiniGameType.DoorKeypad);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) HandleInput(MiniGameType.WireFix);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) HandleInput(MiniGameType.PowerSync);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) HandleInput(MiniGameType.Cannon);

        if (Keyboard.current.escapeKey.wasPressedThisFrame && activeGame != null)
        {
            activeGame.ForceFail();
        }
    }

    private void HandleInput(MiniGameType type)
    {
        // 이미 다른 게임이 켜져 있으면 실패 처리 후 새 게임 열기
        if (activeGame != null) activeGame.ForceFail();
        else OpenMiniGame(type, null);
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
                activeGame = mg;

                // 패널 애니메이션: 위에서 슉! 내려오기
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideDownRoutine(mg.GetComponent<RectTransform>()));
            }
            else
            {
                mg.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator SlideDownRoutine(RectTransform panelRect)
    {
        if (panelRect == null) yield break;

        Vector2 startPos = new Vector2(0, 1200f); // 위쪽 좌표
        Vector2 endPos = Vector2.zero;           // 중앙 좌표

        panelRect.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        panelRect.anchoredPosition = endPos;
    }

    public void EndMiniGame(bool isSuccess)
    {
        if (isFlashing) return;
        StartCoroutine(FlashAndCloseRoutine(isSuccess));
    }

    private IEnumerator FlashAndCloseRoutine(bool isSuccess)
    {
        isFlashing = true;
        if (flashScreen != null)
        {
            flashScreen.color = isSuccess ? Color.green : Color.red;
            flashScreen.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(0.3f);

        if (flashScreen != null) flashScreen.gameObject.SetActive(false);
        CloseAll();
        isFlashing = false;
    }

    public void CloseAll()
    {
        activeGame = null;
        canvasRoot.SetActive(false);
        foreach (var mg in miniGames) mg.gameObject.SetActive(false);
    }
}