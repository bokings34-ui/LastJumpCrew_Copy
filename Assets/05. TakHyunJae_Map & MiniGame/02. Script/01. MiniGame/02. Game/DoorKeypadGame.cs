using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // 코루틴(애니메이션)을 사용하기 위해 필수입니다.
using System.Collections.Generic;
using LastJumpCrew.Common;

public class DoorKeypadGame : MiniGameBase
{
    [Header("UI 컴포넌트 연결")]
    [SerializeField] private TextMeshProUGUI statusText; // 상단 안내 및 상태 텍스트
    [SerializeField] private TextMeshProUGUI timerText;  // 남은 시간을 표시할 텍스트 UI
    [SerializeField] private Button[] keypadButtons;     // 3x3 배열의 버튼 9개

    [Header("하이퍼캐주얼 시각 피드백 색상")]
    [SerializeField] private Color normalColor = Color.white;  // 버튼 기본 색상
    [SerializeField] private Color correctColor = Color.green; // 정답 버튼 색상
    [SerializeField] private Color wrongColor = Color.red;     // 오답 버튼 색상

    private int currentExpectedNumber = 1; // 현재 플레이어가 입력해야 할 올바른 순서의 숫자

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target); // 부모 클래스의 초기화 설정을 먼저 호출합니다.
        currentExpectedNumber = 1; // 시작 숫자를 1로 초기화

        if (statusText != null)
        {
            statusText.text = "1부터 순서대로 누르십시오.";
            statusText.color = Color.white;
            statusText.rectTransform.localScale = Vector3.one; // 텍스트 크기 초기화
        }

        ShuffleAndSetupButtons(); // 버튼 숫자를 무작위로 섞고 세팅합니다.
    }

    private void Update()
    {
        if (!isGameActive) return;

        // 1. 타이머 계산
        timeRemaining -= Time.deltaTime;

        // 💡 [여기 다시 추가되었습니다!] 
        // 남은 시간이 2초 이하로 떨어지면 부모의 포스트 프로세싱 피 비네트 효과를 가동합니다.
        UpdateDangerPulse();

        if (timerText != null)
            timerText.text = $"남은 시간: {timeRemaining:F1}초";

        // 2. 시간 초과 판정
        if (timeRemaining <= 0)
        {
            if (statusText != null)
            {
                statusText.text = "시간 초과!";
                statusText.color = Color.red;
                PunchUI(statusText.rectTransform, 1.3f, 0.2f); // 안내 글씨 크게 튕기기
            }
            if (timerText != null) timerText.text = "0.0초";

            TriggerFailure(); // 부모 클래스의 전체 실패 시퀀스 호출
        }
    }

    // 버튼들의 숫자를 무작위로 배치하고 클릭 이벤트를 매핑하는 함수
    private void ShuffleAndSetupButtons()
    {
        List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (int i = 0; i < numbers.Count; i++)
        {
            int temp = numbers[i];
            int randomIndex = Random.Range(i, numbers.Count);
            numbers[i] = numbers[randomIndex];
            numbers[randomIndex] = temp;
        }

        for (int i = 0; i < keypadButtons.Length; i++)
        {
            Button btn = keypadButtons[i];
            Image btnImage = btn.GetComponent<Image>();
            RectTransform btnRect = btn.GetComponent<RectTransform>();

            // 게임이 재시작되거나 초기화될 때를 대비해 상태를 초기 상태로 복구합니다.
            btn.interactable = true;
            btnRect.localRotation = Quaternion.identity;
            btnRect.localScale = Vector3.one;

            if (btnImage != null)
            {
                btnImage.color = new Color(normalColor.r, normalColor.g, normalColor.b, 1f);
            }

            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            int assignedNumber = numbers[i];
            if (btnText != null) btnText.text = assignedNumber.ToString();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnKeypadClicked(assignedNumber, btnImage, btnRect));
        }
    }

    // 키패드 버튼이 클릭되었을 때 호출되는 판정 및 연출 함수
    private void OnKeypadClicked(int number, Image clickedImage, RectTransform clickedRect)
    {
        if (!isGameActive) return;

        PlaySFX(clickClip, true); // 피치가 랜덤하게 변하는 클릭음 재생

        // 정답 숫자를 맞췄을 때
        if (number == currentExpectedNumber)
        {
            PlayParticle(clickedRect); // 정답 버튼 정중앙 좌표에서 UI 파티클 폭발!
            currentExpectedNumber++;

            if (statusText != null)
            {
                statusText.text = $"{currentExpectedNumber}번 입력 대기 중...";
                PunchUI(statusText.rectTransform, 1.2f, 0.15f); // 글자가 갱신될 때마다 퉁퉁 튀는 연출
            }

            // 버튼이 Y축으로 회전하면서 크기가 작아지고 반투명해지는 3D 플립 연출 시작
            StartCoroutine(FlipAndFadeRoutine(clickedRect, clickedImage));

            // 1부터 9까지 모두 순서대로 클리어했을 때 최종 성공 판정
            if (currentExpectedNumber > 9)
            {
                if (statusText != null)
                {
                    statusText.text = "인증 성공";
                    statusText.color = Color.green;
                    PunchUI(statusText.rectTransform, 1.4f, 0.2f);
                }
                TriggerSuccess(); // 부모 클래스의 성공 시퀀스 일괄 호출
            }
        }
        else // 오답을 눌러 실패했을 때
        {
            if (clickedImage != null) clickedImage.color = wrongColor; // 버튼을 빨간색으로 강제 변경
            PunchUI(clickedRect); // 틀린 버튼 자체를 강하게 진동

            if (statusText != null)
            {
                statusText.text = "인증 실패!";
                statusText.color = Color.red;
                PunchUI(statusText.rectTransform, 1.4f, 0.2f);
            }
            TriggerFailure(); // 즉시 실패 처리 (글리치, 화면 지진 등 발동)
        }
    }

    // 정답 버튼을 부드럽게 반회전시키며 비활성화 처리하는 고급 애니메이션 코루틴
    private IEnumerator FlipAndFadeRoutine(RectTransform rect, Image img)
    {
        float duration = 0.15f;
        float elapsed = 0f;

        // 1단계: 0도에서 90도 회전하여 옆면으로 칼날처럼 얇아지게 만듭니다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Lerp(0f, 90f, elapsed / duration);
            rect.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        // 완전히 90도가 되어 앞면이 보이지 않는 찰나에 색상을 초록색(정답 색)으로 스위칭합니다.
        if (img != null) img.color = correctColor;

        // 2단계: 다시 90도에서 0도로 눕히면서 동시에 크기를 0.7배로 줄이고 흐릿하게 흐려지게 만듭니다.
        elapsed = 0f;
        Vector3 originalScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float angle = Mathf.Lerp(90f, 0f, t);
            rect.localRotation = Quaternion.Euler(0f, angle, 0f);

            rect.localScale = Vector3.Lerp(originalScale, originalScale * 0.7f, t);

            if (img != null)
            {
                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0.3f, t); // 투명도를 30% 수준으로 떨어뜨림
                img.color = c;
            }
            yield return null;
        }

        // 처리가 완벽히 끝난 버튼은 중복 터치가 들어가지 않도록 물리 인터랙션을 원천 차단합니다.
        Button btn = rect.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
    }
}