using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastJumpCrew.Common;

public class CannonGame : MiniGameBase
{
    [Header("UI 연결")]
    public Button[] targetButtons;
    public TextMeshProUGUI scoreText;

    [Header("게임 설정")]
    public float timeLimit = 5.0f;

    [Header("💡 상하좌우 여백 자유 조절")]
    public float paddingLeft = 100f;
    public float paddingRight = 100f;
    public float paddingTop = 180f;   // 텍스트 공간 확보를 위해 기본값 크게 설정
    public float paddingBottom = 100f;

    private float timeRemaining;
    private int targetsDestroyed = 0;
    private bool isGameActive = false;

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target);

        timeRemaining = timeLimit;
        targetsDestroyed = 0;
        isGameActive = true;

        UpdateScoreText();

        RectTransform panelRect = GetComponent<RectTransform>();
        float halfWidth = panelRect.rect.width / 2f;
        float halfHeight = panelRect.rect.height / 2f;

        // 과녁 이미지 자체의 크기 여유분 (반지름 약 60f)
        float buttonOffset = 60f;

        // 💡 설정한 패딩 값을 기준으로 최소/최대 생성 범위 계산
        float minX = -halfWidth + paddingLeft + buttonOffset;
        float maxX = halfWidth - paddingRight - buttonOffset;
        float minY = -halfHeight + paddingBottom + buttonOffset;
        float maxY = halfHeight - paddingTop - buttonOffset;

        // 패딩 값이 너무 커서 범위가 뒤집히는 에러 방어 코드
        if (minX > maxX) minX = maxX;
        if (minY > maxY) minY = maxY;

        foreach (Button btn in targetButtons)
        {
            btn.gameObject.SetActive(true);
            RectTransform btnRect = btn.GetComponent<RectTransform>();

            // 💡 정밀하게 계산된 상하좌우 영역 안에서만 랜덤 배치
            float randomX = Random.Range(minX, maxX);
            float randomY = Random.Range(minY, maxY);

            btnRect.anchoredPosition = new Vector2(randomX, randomY);

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnTargetClicked(btn));
        }
    }

    private void Update()
    {
        if (!isGameActive) return;

        timeRemaining -= Time.deltaTime;
        UpdateScoreText();

        if (timeRemaining <= 0)
        {
            isGameActive = false;
            if (scoreText != null) scoreText.text = "시간 초과!";
            Invoke(nameof(GameFail), 0.5f);
        }
    }

    private void OnTargetClicked(Button clickedButton)
    {
        if (!isGameActive) return;

        clickedButton.gameObject.SetActive(false);
        targetsDestroyed++;
        UpdateScoreText();

        if (targetsDestroyed >= targetButtons.Length)
        {
            isGameActive = false;
            if (scoreText != null) scoreText.text = "위협 제거 완료!";
            Invoke(nameof(GameSucceed), 0.5f);
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"남은 시간: {timeRemaining:F1}초\n제거한 위협: {targetsDestroyed} / {targetButtons.Length}";
    }
}