using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using LastJumpCrew.Common;

public class WireFixGame : MiniGameBase
{
    [Header("UI 연결")]
    public TextMeshProUGUI timerText;

    [Header("왼쪽 시작점들 (5개)")]
    public Image[] leftPoints;

    [Header("오른쪽 끝점들 (5개)")]
    public Image[] rightPoints;

    [Header("게임 설정")]
    public float timeLimit = 5.0f; // 💡 인스펙터에서 바꿀 수 있는 제한 시간 (기본값 5초)
    public float wireThickness = 15f; // 드래그할 때 나오는 선의 굵기

    private float timeRemaining; // 실제 줄어드는 시간 계산용
    private bool isGameActive = false;
    private int connectedWires = 0;
    private int draggingIndex = -1;

    // 선 그리기 관련 변수
    private GameObject currentDrawingLine;
    private RectTransform currentLineRect;
    private GameObject[] completedLines;

    private List<Color> baseColors = new List<Color>
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        new Color(1f, 0.5f, 0f)
    };

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target);

        // 💡 인스펙터에서 설정한 시간으로 초기화
        timeRemaining = timeLimit;
        connectedWires = 0;
        isGameActive = true;
        draggingIndex = -1;
        completedLines = new GameObject[leftPoints.Length];

        // 게임 시작 시 이전에 그려둔 선이 있다면 전부 지우기
        foreach (Transform child in transform)
        {
            if (child.name == "WireLine") Destroy(child.gameObject);
        }

        SetupRandomColors();
    }

    private void SetupRandomColors()
    {
        List<Color> leftColors = new List<Color>(baseColors);
        ShuffleList(leftColors);
        for (int i = 0; i < leftPoints.Length; i++)
        {
            leftPoints[i].color = new Color(leftColors[i].r, leftColors[i].g, leftColors[i].b, 1f);
        }

        List<Color> rightColors = new List<Color>(baseColors);
        ShuffleList(rightColors);
        for (int i = 0; i < rightPoints.Length; i++)
        {
            rightPoints[i].color = new Color(rightColors[i].r, rightColors[i].g, rightColors[i].b, 1f);
        }
    }

    private void ShuffleList(List<Color> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Color temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void Update()
    {
        if (!isGameActive || Mouse.current == null) return;

        // 1. 타이머 로직
        timeRemaining -= Time.deltaTime;
        if (timerText != null) timerText.text = $"남은 시간: {timeRemaining:F1}초";

        if (timeRemaining <= 0)
        {
            if (timerText != null) timerText.text = "시간 초과!";
            isGameActive = false;
            Invoke(nameof(GameFail), 1.0f);
            return;
        }

        // 2. 마우스 입력 및 선 그리기 로직
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            for (int i = 0; i < leftPoints.Length; i++)
            {
                if (leftPoints[i].color.a < 1f) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(leftPoints[i].rectTransform, mousePos, null))
                {
                    draggingIndex = i;
                    CreateWire(i);
                    break;
                }
            }
        }

        if (draggingIndex != -1 && Mouse.current.leftButton.isPressed)
        {
            UpdateWire(mousePos);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (draggingIndex != -1)
            {
                CheckConnection(mousePos);
                draggingIndex = -1;
            }
        }
    }

    private void CreateWire(int index)
    {
        currentDrawingLine = new GameObject("WireLine");
        currentDrawingLine.transform.SetParent(this.transform, false);
        currentDrawingLine.transform.SetAsFirstSibling();

        Image wireImage = currentDrawingLine.AddComponent<Image>();
        wireImage.color = leftPoints[index].color;

        currentLineRect = currentDrawingLine.GetComponent<RectTransform>();
        currentLineRect.pivot = new Vector2(0, 0.5f);
        currentLineRect.position = leftPoints[index].transform.position;
        currentLineRect.sizeDelta = new Vector2(0, wireThickness);
    }

    private void UpdateWire(Vector2 targetPos)
    {
        if (currentLineRect == null) return;

        Vector3 startPos = currentLineRect.position;
        Vector3 dir = (Vector3)targetPos - startPos;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        currentLineRect.rotation = Quaternion.Euler(0, 0, angle);

        float distance = dir.magnitude;
        currentLineRect.sizeDelta = new Vector2(distance, wireThickness);
    }

    private void CheckConnection(Vector2 dropPosition)
    {
        Color draggedColor = leftPoints[draggingIndex].color;
        bool isConnected = false;

        for (int i = 0; i < rightPoints.Length; i++)
        {
            if (rightPoints[i].color.a < 1f) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(rightPoints[i].rectTransform, dropPosition, null))
            {
                if (SameColor(rightPoints[i].color, draggedColor))
                {
                    isConnected = true;
                    connectedWires++;

                    UpdateWire(rightPoints[i].transform.position);
                    completedLines[draggingIndex] = currentDrawingLine;

                    SetImageAlpha(leftPoints[draggingIndex], 0.3f);
                    SetImageAlpha(rightPoints[i], 0.3f);

                    if (connectedWires >= leftPoints.Length)
                    {
                        if (timerText != null) timerText.text = "복구 완료!";
                        isGameActive = false;
                        Invoke(nameof(GameSucceed), 0.5f);
                    }
                    break;
                }
            }
        }

        if (!isConnected && currentDrawingLine != null)
        {
            Destroy(currentDrawingLine);
        }
    }

    private bool SameColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}