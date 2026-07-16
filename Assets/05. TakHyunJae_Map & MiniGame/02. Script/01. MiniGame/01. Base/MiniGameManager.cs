using LastJumpCrew.Common;
using SM;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
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
    private readonly Dictionary<MiniGameType, EventId> _gameToEventMap = new()
    {
        { MiniGameType.Cannon, EventId.MeteorAttack },
        { MiniGameType.WireFix, EventId.EmpAttack },
        { MiniGameType.PowerSync, EventId.EnemyScout }
    };

    private sealed class NetworkEventMiniGameTarget : IMiniGameTarget
    {
        private readonly NetworkEventCoordinator coordinator;
        private readonly EventId eventId;
        private readonly IMiniGameTarget terminalTarget;

        public NetworkEventMiniGameTarget(
            NetworkEventCoordinator coordinator,
            EventId eventId,
            IMiniGameTarget terminalTarget)
        {
            this.coordinator = coordinator;
            this.eventId = eventId;
            this.terminalTarget = terminalTarget;
        }

        public string MiniGameTargetId => $"NetworkEvent:{eventId}";

        public void OnMiniGameSucceeded()
        {
            SubmitResult(true);
        }

        public void OnMiniGameFailed()
        {
            SubmitResult(false);
        }

        private void SubmitResult(bool succeeded)
        {
            if (!coordinator.RequestMiniGameResult(eventId, succeeded))
            {
                Debug.LogWarning(
                    $"[MiniGameManager] 서버가 {eventId} 미니게임 결과를 받지 못했습니다.");
            }

            if (succeeded)
            {
                terminalTarget?.OnMiniGameSucceeded();
            }
            else
            {
                terminalTarget?.OnMiniGameFailed();
            }
        }
    }

    private sealed class CompositeMiniGameTarget : IMiniGameTarget
    {
        private readonly IMiniGameTarget eventTarget;
        private readonly IMiniGameTarget terminalTarget;

        public CompositeMiniGameTarget(IMiniGameTarget eventTarget, IMiniGameTarget terminalTarget)
        {
            this.eventTarget = eventTarget;
            this.terminalTarget = terminalTarget;
        }

        public string MiniGameTargetId => $"{eventTarget.MiniGameTargetId}+{terminalTarget.MiniGameTargetId}";

        public void OnMiniGameSucceeded()
        {
            eventTarget.OnMiniGameSucceeded();
            terminalTarget.OnMiniGameSucceeded();
        }

        public void OnMiniGameFailed()
        {
            eventTarget.OnMiniGameFailed();
            terminalTarget.OnMiniGameFailed();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (canvasRoot == null)
        {
            Debug.LogError("[MiniGameManager] canvasRoot가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

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
    public bool IsMiniGameAvailable(MiniGameType type)
    {
        if (!_gameToEventMap.TryGetValue(type, out var eventId))
        {
            return true;
        }

        var networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            var coordinator = NetworkEventCoordinator.Instance;
            return coordinator != null
                && coordinator.IsSpawned
                && coordinator.IsEventActive(eventId);
        }

        var eventManager = EventManager.Instance;
        return eventManager != null
            && eventManager.GetMiniGameTarget(eventId.ToString()) != null;
    }

    public bool IsEventDrivenMiniGame(MiniGameType type)
    {
        return _gameToEventMap.ContainsKey(type);
    }

    public void OpenMiniGame(MiniGameType type, IMiniGameTarget target)
    {
        TryOpenMiniGame(type, target);
    }

    public bool TryOpenMiniGame(MiniGameType type, IMiniGameTarget target)
    {
        if (canvasRoot == null || miniGames == null)
        {
            Debug.LogError("[MiniGameManager] UI 또는 miniGames 설정이 없습니다.", this);
            return false;
        }

        if (!IsMiniGameAvailable(type))
        {
            Debug.LogWarning($"[MiniGameManager] {type} 미니게임 활성 조건이 충족되지 않았습니다.", this);
            return false;
        }

        MiniGameBase selectedGame = null;
        foreach (var miniGame in miniGames)
        {
            if (miniGame != null && miniGame.gameType == type)
            {
                selectedGame = miniGame;
                break;
            }
        }

        if (selectedGame == null)
        {
            Debug.LogError($"[MiniGameManager] {type} 미니게임이 등록되지 않았습니다.", this);
            return false;
        }

        IMiniGameTarget resolvedTarget = ResolveTarget(type, target);
        if (resolvedTarget == null)
        {
            Debug.LogError($"[MiniGameManager] {type} 미니게임 결과를 받을 대상이 없습니다.", this);
            return false;
        }

        canvasRoot.SetActive(true);

        foreach (var mg in miniGames)
        {
            if (mg == null)
            {
                continue;
            }

            if (mg.gameType == type)
            {
                mg.gameObject.SetActive(true);
                mg.StartGame(resolvedTarget); // 미니게임 시작 및 큐브/이벤트 대상 연결
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

        return true;
    }

    private IMiniGameTarget ResolveTarget(MiniGameType type, IMiniGameTarget terminalTarget)
    {
        if (!_gameToEventMap.TryGetValue(type, out var eventId))
        {
            return terminalTarget;
        }

        var coordinator = NetworkEventCoordinator.Instance;
        if (coordinator != null && coordinator.IsEventActive(eventId))
        {
            return new NetworkEventMiniGameTarget(coordinator, eventId, terminalTarget);
        }

        EventManager eventManager = EventManager.Instance;
        IMiniGameTarget eventTarget = eventManager != null
            ? eventManager.GetMiniGameTarget(eventId.ToString())
            : null;

        if (eventTarget == null)
        {
            return null;
        }

        if (terminalTarget == null || ReferenceEquals(eventTarget, terminalTarget))
        {
            return eventTarget;
        }

        return new CompositeMiniGameTarget(eventTarget, terminalTarget);
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
