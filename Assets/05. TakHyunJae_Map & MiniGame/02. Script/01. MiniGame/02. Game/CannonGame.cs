using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using LastJumpCrew.Common;

public class CannonGame : MiniGameBase
{
    [Header("UI 연결")]
    public Button[] targetButtons; // 쏴서 없애야 할 과녁 버튼들
    public TextMeshProUGUI scoreText;

    [Header("이동 속도 설정")]
    public float minMoveSpeed = 150f;
    public float maxMoveSpeed = 350f;
    public float targetSpacing = 20f;

    [Header("상하좌우 여백 자유 조절 (화면 밖으로 안 나가게 방어)")]
    public float paddingLeft = 100f;
    public float paddingRight = 100f;
    public float paddingTop = 180f;
    public float paddingBottom = 100f;

    private int targetsDestroyed = 0; // 맞춘 과녁 개수

    // 과녁들이 돌아다닐 수 있는 우리(울타리)의 최소/최대 좌표값
    private float boundMinX, boundMaxX, boundMinY, boundMaxY;

    // 움직이는 각각의 과녁의 '정보'를 묶어서 관리하기 위한 작은 주머니(클래스)
    private class MovingTarget
    {
        public RectTransform rect;   // 과녁의 위치와 크기 정보
        public Vector2 velocity;     // 과녁이 날아가고 있는 방향과 속도(힘)
    }

    private List<MovingTarget> movingTargets = new List<MovingTarget>();

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target);

        targetsDestroyed = 0;
        movingTargets.Clear();

        UpdateScoreText();

        RectTransform panelRect = GetComponent<RectTransform>();
        float halfWidth = panelRect.rect.width / 2f;
        float halfHeight = panelRect.rect.height / 2f;

        float buttonOffset = 60f;
        foreach (Button btn in targetButtons)
        {
            if (btn == null) continue;
            buttonOffset = Mathf.Max(buttonOffset, GetTargetRadius(btn.GetComponent<RectTransform>()));
        }

        // 인스펙터에서 설정한 패딩값들을 바탕으로 과녁이 돌아다닐 '가상의 벽' 좌표를 계산합니다.
        boundMinX = -halfWidth + paddingLeft + buttonOffset;
        boundMaxX = halfWidth - paddingRight - buttonOffset;
        boundMinY = -halfHeight + paddingBottom + buttonOffset;
        boundMaxY = halfHeight - paddingTop - buttonOffset;

        // 만약 패딩값을 너무 크게 적어서 왼쪽 벽이 오른쪽 벽을 넘어가는 오류를 수학적으로 방지합니다.
        if (boundMinX > boundMaxX) boundMinX = boundMaxX;
        if (boundMinY > boundMaxY) boundMinY = boundMaxY;

        List<Vector2> occupiedPositions = new List<Vector2>();
        List<float> occupiedRadii = new List<float>();

        // 모든 과녁 버튼들을 순회하면서 위치와 속도를 세팅합니다.
        foreach (Button btn in targetButtons)
        {
            btn.gameObject.SetActive(true); // 버튼을 켜줌
            RectTransform btnRect = btn.GetComponent<RectTransform>();

            float targetRadius = GetTargetRadius(btnRect);
            Vector2 spawnPosition = FindNonOverlappingSpawnPosition(
                targetRadius,
                occupiedPositions,
                occupiedRadii);

            btnRect.anchoredPosition = spawnPosition;
            occupiedPositions.Add(spawnPosition);
            occupiedRadii.Add(targetRadius);

            // Random.insideUnitCircle.normalized는 무작위 방향(360도)으로 화살표를 만들어줍니다.
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float randomSpeed = Random.Range(minMoveSpeed, maxMoveSpeed); // 랜덤 속도 뽑기

            // 방금 만든 방향 * 속도를 곱해서 이 과녁의 최종 '이동 에너지(velocity)'를 저장해 둡니다.
            movingTargets.Add(new MovingTarget
            {
                rect = btnRect,
                velocity = randomDirection * randomSpeed
            });

            // 과녁을 클릭했을 때 실행될 이벤트 연결
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnTargetClicked(btn));
        }
    }

    private void Update()
    {
        if (!isGameActive) return;

        timeRemaining -= Time.deltaTime;
        UpdateDangerPulse(); //2초 이하로 남았을때 화면이 붉게 깜빡입니다.
        UpdateScoreText();

        if (timeRemaining <= 0)
        {
            if (scoreText != null) scoreText.text = "시간 초과!";
            TriggerFailure();
        }

        // 🚀 핵심: 모든 과녁들을 실시간으로 이동시키고 벽에 튕기게 하는 로직
        foreach (var target in movingTargets)
        {
            // 클릭해서 이미 터진 과녁은 검사하지 않고 패스(continue)
            if (!target.rect.gameObject.activeSelf) continue;

            Vector2 pos = target.rect.anchoredPosition; // 현재 과녁 위치

            // 기존 위치에 '이동 에너지 * 프레임 시간'을 더해서 앞으로 나아가게 합니다.
            pos += target.velocity * Time.deltaTime;

            // X축(가로) 벽에 부딪혔을 때 검사 (왼쪽 벽을 넘었거나 오른쪽 벽을 넘었다면?)
            if (pos.x <= boundMinX || pos.x >= boundMaxX)
            {
                target.velocity.x *= -1; // 💡 X축 방향 에너지를 반대로 뒤집음! (-1을 곱하면 반대로 튕겨나감)
                pos.x = Mathf.Clamp(pos.x, boundMinX, boundMaxX); // 벽 밖으로 나간 이미지를 벽 안으로 강제 소환
            }

            // Y축(세로) 벽에 부딪혔을 때 검사 (아래 벽 또는 위 벽)
            if (pos.y <= boundMinY || pos.y >= boundMaxY)
            {
                target.velocity.y *= -1; // Y축 방향 반대로 뒤집기!
                pos.y = Mathf.Clamp(pos.y, boundMinY, boundMaxY);
            }

            // 튕기기 계산이 끝난 최종 좌표를 과녁에 다시 적용합니다.
            target.rect.anchoredPosition = pos;
        }

        ResolveTargetOverlaps();
    }

    private Vector2 FindNonOverlappingSpawnPosition(
        float targetRadius,
        List<Vector2> occupiedPositions,
        List<float> occupiedRadii)
    {
        const int maxAttempts = 32;
        Vector2 bestCandidate = Vector2.zero;
        float bestClearance = float.MinValue;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(boundMinX, boundMaxX),
                Random.Range(boundMinY, boundMaxY));

            float minimumClearance = float.MaxValue;
            bool overlaps = false;

            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                float requiredDistance = targetRadius + occupiedRadii[i] + targetSpacing;
                float clearance = Vector2.Distance(candidate, occupiedPositions[i]) - requiredDistance;
                minimumClearance = Mathf.Min(minimumClearance, clearance);

                if (clearance < 0f)
                {
                    overlaps = true;
                }
            }

            if (!overlaps)
            {
                return candidate;
            }

            if (minimumClearance > bestClearance)
            {
                bestClearance = minimumClearance;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private void ResolveTargetOverlaps()
    {
        for (int i = 0; i < movingTargets.Count; i++)
        {
            MovingTarget first = movingTargets[i];
            if (!first.rect.gameObject.activeSelf) continue;

            for (int j = i + 1; j < movingTargets.Count; j++)
            {
                MovingTarget second = movingTargets[j];
                if (!second.rect.gameObject.activeSelf) continue;

                Vector2 firstPosition = first.rect.anchoredPosition;
                Vector2 secondPosition = second.rect.anchoredPosition;
                Vector2 delta = firstPosition - secondPosition;

                float firstRadius = GetTargetRadius(first.rect);
                float secondRadius = GetTargetRadius(second.rect);
                float minimumDistance = firstRadius + secondRadius + targetSpacing;
                float distance = delta.magnitude;

                if (distance >= minimumDistance) continue;

                Vector2 normal = distance > 0.001f ? delta / distance : Vector2.right;
                float correction = (minimumDistance - distance) * 0.5f;

                firstPosition += normal * correction;
                secondPosition -= normal * correction;

                first.rect.anchoredPosition = new Vector2(
                    Mathf.Clamp(firstPosition.x, boundMinX, boundMaxX),
                    Mathf.Clamp(firstPosition.y, boundMinY, boundMaxY));
                second.rect.anchoredPosition = new Vector2(
                    Mathf.Clamp(secondPosition.x, boundMinX, boundMaxX),
                    Mathf.Clamp(secondPosition.y, boundMinY, boundMaxY));

                Vector2 relativeVelocity = first.velocity - second.velocity;
                if (Vector2.Dot(relativeVelocity, normal) < 0f)
                {
                    first.velocity = Vector2.Reflect(first.velocity, normal);
                    second.velocity = Vector2.Reflect(second.velocity, normal);
                }
            }
        }
    }

    private static float GetTargetRadius(RectTransform targetRect)
    {
        Vector2 size = targetRect.rect.size;
        return Mathf.Max(size.x, size.y) * 0.5f;
    }

    private void OnTargetClicked(Button clickedButton)
    {
        if (!isGameActive) return;

        PlaySFX(clickClip, true); // 타격음
        PunchUI(clickedButton.GetComponent<RectTransform>()); // 과녁 찌그러짐

        // 💡 [여기 추가!] 버튼 중앙에서 파티클 펑!
        PlayParticle(clickedButton.GetComponent<RectTransform>());

        clickedButton.gameObject.SetActive(false); // 과녁 숨기기
        targetsDestroyed++;
        UpdateScoreText();

        if (targetsDestroyed >= targetButtons.Length)
        {
            if (scoreText != null) scoreText.text = "위협 제거 완료!";
            TriggerSuccess();
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"남은 시간: {timeRemaining:F1}초\n제거한 위협: {targetsDestroyed} / {targetButtons.Length}";
    }
}
