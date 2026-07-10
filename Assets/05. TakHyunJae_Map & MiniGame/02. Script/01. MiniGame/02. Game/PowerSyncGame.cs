using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LastJumpCrew.Common;

public class PowerSyncGame : MiniGameBase
{
    [Header("UI 연결")]
    public Slider powerSlider;        // 움직이는 게이지 바
    public Image safeZoneImage;       // 초록색(하늘색) 안전 구간 이미지
    public TextMeshProUGUI statusText; // 상단 안내 및 상태 텍스트
    public TextMeshProUGUI timerText;  // 남은 시간을 표시할 텍스트 UI

    [Header("이펙트 설정")]
    public RectTransform handleRect; // 움직이는 빨간색 막대기(핸들)의 위치 데이터
    public Color normalSafeColor = new Color(0f, 1f, 1f, 1f); // 진입 전 기본 하늘색 (Cyan)
    public Color highlightSafeColor = Color.green;            // 핸들이 진입했을 때 빛날 형광 초록색

    [Header("게임 추가 설정")]
    public float speed = 2.0f;        // 게이지 바가 움직이는 속도
    public float safeZoneSize = 0.15f; // 안전 구간의 크기 (전체의 15%)

    private float safeZoneMin; // 안전 구간의 최소값 (0~1 사이)
    private float safeZoneMax; // 안전 구간의 최대값 (0~1 사이)

    private bool wasInSafeZone = false; // 바로 전 프레임에 핸들이 안전 구역에 있었는지 기억하는 변수

    // 💡 [버그 해결 스위치] 스페이스바를 눌러서 0.2초 동안 '역경직(멈춤)' 상태에 들어갔는지 체크합니다.
    private bool isHitStopped = false;

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target); // 부모(MiniGameBase)의 타이머 초기화 등을 실행합니다.

        powerSlider.value = 0f; // 시작할 때 게이지 바 위치를 맨 왼쪽(0)으로 리셋
        wasInSafeZone = false;  // 상태 초기화
        isHitStopped = false;   // 💡 게임이 다시 켜질 때 멈춤 스위치를 깨끗하게 꺼줍니다.

        // 1. 랜덤 안전 구간 생성 (0.1 ~ 0.8 사이 어딘가에서 초록색 구간이 시작됨)
        float randomStart = Random.Range(0.1f, 0.8f);
        safeZoneMin = randomStart;
        safeZoneMax = randomStart + safeZoneSize;

        // 2. 화면에 보이는 안전 구간 이미지(UI)의 위치와 크기를 계산해서 씌워줍니다.
        RectTransform sliderRect = powerSlider.GetComponent<RectTransform>();
        RectTransform safeRect = safeZoneImage.GetComponent<RectTransform>();

        float sliderWidth = sliderRect.rect.width; // 슬라이더의 전체 가로 픽셀 길이

        // 뽑힌 랜덤 위치 * 전체 길이를 곱해서 픽셀 단위 위치를 잡습니다.
        safeRect.anchoredPosition = new Vector2(randomStart * sliderWidth, 0);
        safeRect.sizeDelta = new Vector2(safeZoneSize * sliderWidth, safeRect.sizeDelta.y);

        // 시작할 때 안전 구역 색상을 기본 하늘색으로 세팅
        if (safeZoneImage != null) safeZoneImage.color = normalSafeColor;

        if (statusText != null)
            statusText.text = "타이밍에 맞춰 [스페이스바]를 누르세요!";
    }

    private void Update()
    {
        // 💡 [버그 해결] 원래 isGameActive만 검사하던 곳에 isHitStopped 조건도 추가했습니다!
        // 스페이스바를 눌러서 멈춘 동안에는 타이머가 줄어들거나 게이지가 더 움직이지 않도록 완전 방어합니다.
        if (!isGameActive || isHitStopped || Keyboard.current == null) return;

        // 1. 타이머 계산 및 UI 표시
        timeRemaining -= Time.deltaTime;
        UpdateDangerPulse(); // 2초 이하로 남았을 때 화면이 붉게 깜빡이는 함수 호출 (부모 스크립트에 있음)

        if (timerText != null)
            timerText.text = $"남은 시간: {timeRemaining:F1}초";

        // 2. 시간이 0 이하가 되면 강제 실패 처리
        if (timeRemaining <= 0)
        {
            if (timerText != null) timerText.text = "시간 초과!";
            if (statusText != null) statusText.text = "시스템 다운!";
            TriggerFailure(); // 부모의 실패 연출 일괄 호출
            return;
        }

        // 3. 게이지 왕복 이동 (0에서 1 사이를 자연스럽게 왕복함)
        powerSlider.value = Mathf.PingPong(Time.time * speed, 1f);

        // 4. 안전 구역 진입/이탈 실시간 체크 (형광색 번쩍임 + 띡! 효과음)
        bool isCurrentlySafe = powerSlider.value >= safeZoneMin && powerSlider.value <= safeZoneMax;

        if (isCurrentlySafe && !wasInSafeZone)
        {
            // 방금 구역 안으로 쏙 들어온 순간! (형광초록 변경 + 통통 튀기 + 효과음)
            if (safeZoneImage != null) safeZoneImage.color = highlightSafeColor;
            PunchUI(safeZoneImage.rectTransform, 1.2f, 0.1f);
            PlaySFX(clickClip, true);
        }
        else if (!isCurrentlySafe && wasInSafeZone)
        {
            // 방금 구역 밖으로 나간 순간! (원래 하늘색으로 복구)
            if (safeZoneImage != null) safeZoneImage.color = normalSafeColor;
        }

        wasInSafeZone = isCurrentlySafe; // 현재 상태를 다음 프레임 비교를 위해 저장

        // 5. 스페이스바를 누른 순간 판정 검사 실행
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckTiming();
        }
    }

    private void CheckTiming()
    {
        // 💡 [버그 해결] 부모의 시스템을 끄는 대신, 이 게임의 멈춤 스위치만 켭니다.
        // 이렇게 해야 0.2초 뜸 들이는 도중에 부모 코드가 폭발 연출을 캔슬시키지 않습니다.
        isHitStopped = true;

        // 누른 순간 핸들이 통통 튀고, 그 위치에서 파티클이 폭발합니다!
        if (handleRect != null)
        {
            PunchUI(handleRect, 1.8f, 0.2f); // 빨간 막대기 크게 팅기기
            PlayParticle(handleRect);        // 💡 부모 스크립트에 새로 고친 강력한 UI 파티클 폭발 함수 호출!
        }

        // 0.2초 동안 그 자리에 딱 멈춰서 긴장감을 고조시키는 코루틴 시작
        StartCoroutine(HitStopRoutine());
    }

    // 역경직(뜸 들이기) 후 최종 결과를 판정하는 코루틴입니다.
    private System.Collections.IEnumerator HitStopRoutine()
    {
        // 0.2초 동안 멈춘 채로 숨을 참게 만듭니다.
        yield return new WaitForSeconds(0.2f);

        // 멈춘 순간의 슬라이더 값이 초록색 구간(최소값~최대값) 사이에 잘 들어왔다면? (성공!)
        if (powerSlider.value >= safeZoneMin && powerSlider.value <= safeZoneMax)
        {
            if (statusText != null) statusText.text = "전력 동기화 성공!";
            if (safeZoneImage != null) safeZoneImage.color = Color.green; // 성공 시 영구적으로 초록색 변경
            TriggerSuccess(); // 부모의 최종 성공 처리 (창 닫기 등)
        }
        else // 구간을 벗어났다면? (실패!)
        {
            if (statusText != null) statusText.text = "동기화 실패! 전력 과부하!";
            if (safeZoneImage != null) safeZoneImage.color = Color.red;   // 실패 시 영구적으로 빨간색 변경
            TriggerFailure(); // 부모의 최종 실패 처리 (화면 지진, 글리치 등)
        }
    }
}