using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DoorKeypadGame : MiniGameBase
{
    [Header("UI 컴포넌트 연결")]
    [SerializeField] private TextMeshProUGUI statusText; // 상단 안내 및 상태 텍스트
    [SerializeField] private Button[] keypadButtons;     // 3x3 배열의 버튼 9개

    [Header("하이퍼캐주얼 시각 피드백 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color correctColor = Color.green;
    [SerializeField] private Color wrongColor = Color.red;

    private int currentExpectedNumber = 1; // 현재 눌러야 하는 숫자 (1부터 시작)

    // MiniGameBase의 StartGame을 오버라이드
    public override void StartGame(LastJumpCrew.Common.IMiniGameTarget target)
    {
        base.StartGame(target); // 부모 클래스의 타겟 등록 실행

        currentExpectedNumber = 1;

        if (statusText != null)
        {
            statusText.text = "1부터 순서대로 누르십시오.";
            statusText.color = Color.white;
        }

        ShuffleAndSetupButtons();
    }

    private void ShuffleAndSetupButtons()
    {
        // 1. 1부터 9까지의 숫자가 담긴 리스트 생성 후 랜덤 셔플
        List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (int i = 0; i < numbers.Count; i++)
        {
            int temp = numbers[i];
            int randomIndex = Random.Range(i, numbers.Count);
            numbers[i] = numbers[randomIndex];
            numbers[randomIndex] = temp;
        }

        // 2. 섞인 숫자를 각 버튼에 매핑 및 초기화
        for (int i = 0; i < keypadButtons.Length; i++)
        {
            Button btn = keypadButtons[i];
            Image btnImage = btn.GetComponent<Image>();

            // 버튼 색상 초기화
            if (btnImage != null) btnImage.color = normalColor;

            // TextMeshPro 텍스트 변경
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            int assignedNumber = numbers[i];
            if (btnText != null) btnText.text = assignedNumber.ToString();

            // 기존 클릭 이벤트 제거 후 새로 할당
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnKeypadClicked(assignedNumber, btnImage));
        }
    }

    private void OnKeypadClicked(int number, Image clickedImage)
    {
        // 올바른 숫자를 누른 경우
        if (number == currentExpectedNumber)
        {
            if (clickedImage != null) clickedImage.color = correctColor;
            currentExpectedNumber++;

            if (statusText != null) statusText.text = $"{currentExpectedNumber}번 입력 대기 중...";

            // 9까지 모두 올바르게 누른 경우 성공 처리
            if (currentExpectedNumber > 9)
            {
                if (statusText != null)
                {
                    statusText.text = "인증 성공";
                    statusText.color = Color.green;
                }
                // 초록색 불빛을 잠깐 보여준 뒤 종료되도록 0.2초 딜레이 호출
                Invoke(nameof(DelayedSucceed), 0.2f);
            }
        }
        // 잘못된 숫자를 누른 경우 즉시 실패 처리
        else
        {
            if (clickedImage != null) clickedImage.color = wrongColor;
            if (statusText != null)
            {
                statusText.text = "인증 실패!";
                statusText.color = Color.red;
            }
            // 빨간색 불빛을 잠깐 보여준 뒤 종료되도록 0.2초 딜레이 호출
            Invoke(nameof(DelayedFail), 0.2f);
        }
    }

    private void DelayedSucceed() => GameSucceed();
    private void DelayedFail() => GameFail();
}