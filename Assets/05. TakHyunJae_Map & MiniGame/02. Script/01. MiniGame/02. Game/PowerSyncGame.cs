using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LastJumpCrew.Common;

public class PowerSyncGame : MiniGameBase
{
    [Header("UI 연결")]
    public Slider powerSlider;
    public Image safeZoneImage;       // 💡 인스펙터에서 초록색 이미지를 직접 연결하세요!
    public TextMeshProUGUI statusText;

    [Header("난이도 설정")]
    public float speed = 2.0f;
    public float safeZoneSize = 0.15f; // 안전 구간 크기 (0~1 사이)

    private float safeZoneMin;
    private float safeZoneMax;
    private bool isGameActive = false;

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target);

        isGameActive = true;
        powerSlider.value = 0f;

        // 1. 랜덤 안전 구간 생성
        float randomStart = Random.Range(0.1f, 0.8f);
        safeZoneMin = randomStart;
        safeZoneMax = randomStart + safeZoneSize;

        // 2. 안전 구간 이미지 시각화 업데이트
        // RectTransform을 사용하여 슬라이더 위에서의 위치와 크기를 실시간 조정
        RectTransform sliderRect = powerSlider.GetComponent<RectTransform>();
        RectTransform safeRect = safeZoneImage.GetComponent<RectTransform>();

        float sliderWidth = sliderRect.rect.width;
        safeRect.anchoredPosition = new Vector2(randomStart * sliderWidth, 0);
        safeRect.sizeDelta = new Vector2(safeZoneSize * sliderWidth, safeRect.sizeDelta.y);

        if (statusText != null)
            statusText.text = "타이밍에 맞춰 [스페이스바]를 누르세요!";
    }

    private void Update()
    {
        // 1번이나 2번 키가 눌려 게임이 강제 종료되는 상황을 위해 activeGame 체크
        if (!isGameActive || Keyboard.current == null) return;

        // 1. 게이지 왕복 이동
        powerSlider.value = Mathf.PingPong(Time.time * speed, 1f);

        // 2. 스페이스바 입력
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckTiming();
        }
    }

    private void CheckTiming()
    {
        isGameActive = false; // 입력 즉시 정지

        if (powerSlider.value >= safeZoneMin && powerSlider.value <= safeZoneMax)
        {
            if (statusText != null) statusText.text = "전력 동기화 성공!";
            Invoke(nameof(GameSucceed), 0.5f); // 성공 연출 호출
        }
        else
        {
            if (statusText != null) statusText.text = "동기화 실패! 전력 과부하!";
            Invoke(nameof(GameFail), 0.5f); // 실패 연출 호출
        }
    }
}