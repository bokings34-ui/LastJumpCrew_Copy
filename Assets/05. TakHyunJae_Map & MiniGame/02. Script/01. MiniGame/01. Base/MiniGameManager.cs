using LastJumpCrew.Common;
using SM;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance;

    [Header("UI 연결")]
    public GameObject canvasRoot;
    public MiniGameBase[] miniGames;

    [Header("결과 피드백 연출")]
    public Image flashScreen;           // 번쩍일 전체 화면 이미지

    [Header("애니메이션 설정")]
    public float slideDuration = 0.25f; // 오르내리는 속도

    private MiniGameBase activeGame = null;
    private bool isFlashing = false;    // 연출 중 키보드 입력 방지
    private Coroutine slideCoroutine = null;

    // 석민 추가 (미니게임과 이벤트 1:1 연결 매핑)
    private readonly Dictionary<MiniGameType, string> _gameToEventMap = new()
    {
        { MiniGameType.Cannon, "MeteorAttack" },
        { MiniGameType.WireFix, "EmpAttack" },
        { MiniGameType.PowerSync, "EnemyScout" }
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        canvasRoot.SetActive(false);
        if (flashScreen != null) flashScreen.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 💡 연출(점멸 및 슬라이드)이 진행 중일 때는 모든 입력을 막아서 버그 방지
        if (Keyboard.current == null || isFlashing) return;

        // ❌ [삭제됨] 기존에 있던 1, 2, 3, 4 숫자 키 입력 코드를 완전히 삭제했습니다!

        // 💡 (선택) 미니게임 도중 ESC를 누르면 강제로 실패(종료) 처리하는 기능만 남겨두었습니다.
        if (Keyboard.current.escapeKey.wasPressedThisFrame && activeGame != null)
        {
            activeGame.ForceFail();
        }
    }

    // 💡 큐브 단말기(MiniGameTerminal)에서 쏘아 올려줄 핵심 오픈 함수!
    public void OpenMiniGame(MiniGameType type, IMiniGameTarget target)
    {
        // 석민 추가 (이벤트 -> 미니게임 연결)
        if (target == null && _gameToEventMap.TryGetValue(type, out string targetId))
        {
            target = EventManager.Instance.GetMiniGameTarget(targetId);
        }

        canvasRoot.SetActive(true);

        foreach (var mg in miniGames)
        {
            if (mg.gameType == type)
            {
                mg.gameObject.SetActive(true);
                mg.StartGame(target); // 미니게임 시작 및 큐브(Target) 연결
                activeGame = mg;

                // 열릴 때: 위에서 아래로 스무스하게 떨어지기
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideDownRoutine(mg.GetComponent<RectTransform>()));
            }
            else
            {
                mg.gameObject.SetActive(false); // 선택되지 않은 다른 미니게임은 확실히 꺼둠
            }
        }
    }

    private IEnumerator SlideDownRoutine(RectTransform panelRect)
    {
        if (panelRect == null) yield break;

        Vector2 startPos = new Vector2(0, 1200f);
        Vector2 endPos = Vector2.zero;

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

    // 💡 미니게임 종료 시 호출부
    public void EndMiniGame(bool isSuccess)
    {
        if (isFlashing) return;
        StartCoroutine(FlashAndSlideUpRoutine(isSuccess));
    }

    // 💡 번쩍임 -> 위로 슬라이드 -> 닫기 시퀀스
    private IEnumerator FlashAndSlideUpRoutine(bool isSuccess)
    {
        isFlashing = true; // 연출 시작 (입력 차단)

        // 1단계: 화면 번쩍임 (성공 시 파란색 / 실패 시 빨간색)
        if (flashScreen != null)
        {
            flashScreen.color = isSuccess ? new Color(0f, 0.5f, 1f, 0.7f) : new Color(1f, 0f, 0f, 0.7f);
            flashScreen.gameObject.SetActive(true);
        }

        // 0.3초 대기
        yield return new WaitForSeconds(0.3f);

        // 번쩍임 끄기
        if (flashScreen != null) flashScreen.gameObject.SetActive(false);

        // 2단계: 패널 위로 올라가기
        if (activeGame != null)
        {
            RectTransform panelRect = activeGame.GetComponent<RectTransform>();
            yield return StartCoroutine(SlideUpRoutine(panelRect)); // 다 올라갈 때까지 기다림
        }

        // 3단계: 완전히 닫고 초기화
        CloseAll();
        isFlashing = false; // 연출 끝 (입력 허용)
    }

    // 💡 밑에서 위로 슉! 올라가는 닫기 애니메이션
    private IEnumerator SlideUpRoutine(RectTransform panelRect)
    {
        if (panelRect == null) yield break;

        Vector2 startPos = Vector2.zero;           // 현재 위치(중앙)
        Vector2 endPos = new Vector2(0, 1200f);    // 다시 올라갈 화면 밖 좌표

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

    public void CloseAll()
    {
        activeGame = null;
        canvasRoot.SetActive(false);
        foreach (var mg in miniGames) mg.gameObject.SetActive(false);
    }
}