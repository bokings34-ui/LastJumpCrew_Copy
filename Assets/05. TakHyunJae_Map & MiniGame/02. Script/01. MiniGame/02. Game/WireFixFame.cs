using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using LastJumpCrew.Common;

public class WireFixGame : MiniGameBase
{
    [Header("UI 연결")]
    public TextMeshProUGUI timerText;
    public Image[] leftPoints;
    public Image[] rightPoints;

    [Header("게임 추가 설정")]
    public float wireThickness = 15f;

    private int connectedWires = 0;
    private int draggingIndex = -1;

    private GameObject currentDrawingLine;
    private RectTransform currentLineRect;
    private GameObject[] completedLines;

    private List<Color> baseColors = new List<Color>
    {
        Color.red, Color.blue, Color.green, Color.yellow, new Color(1f, 0.5f, 0f)
    };

    public override void StartGame(IMiniGameTarget target)
    {
        base.StartGame(target);

        connectedWires = 0;
        draggingIndex = -1;
        completedLines = new GameObject[leftPoints.Length];

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
            leftPoints[i].rectTransform.localScale = Vector3.one;
        }

        List<Color> rightColors = new List<Color>(baseColors);
        ShuffleList(rightColors);
        for (int i = 0; i < rightPoints.Length; i++)
        {
            rightPoints[i].color = new Color(rightColors[i].r, rightColors[i].g, rightColors[i].b, 1f);
            rightPoints[i].rectTransform.localScale = Vector3.one;
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

        timeRemaining -= Time.deltaTime;
        if (timerText != null) timerText.text = $"남은 시간: {timeRemaining:F1}초";

        UpdateDangerPulse(); // 💡 붉은 깜빡임 활성화

        if (timeRemaining <= 0)
        {
            if (timerText != null) timerText.text = "시간 초과!";
            TriggerFailure();
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 마우스 클릭 시작
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            for (int i = 0; i < leftPoints.Length; i++)
            {
                if (leftPoints[i].color.a < 1f) continue;

                // 💡 [카메라 캔버스 버그 해결] 세 번째 인자에 Camera.main 적용
                if (RectTransformUtility.RectangleContainsScreenPoint(leftPoints[i].rectTransform, mousePos, Camera.main))
                {
                    draggingIndex = i;
                    CreateWire(i);
                    PunchUI(leftPoints[i].rectTransform, 1.2f, 0.1f);
                    PlaySFX(clickClip, true);
                    break;
                }
            }
        }

        // 드래그 중
        if (draggingIndex != -1 && Mouse.current.leftButton.isPressed)
        {
            // 💡 [선 튕김 버그 해결] 화면 좌표를 월드 좌표로 변환하여 계산
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform, mousePos, Camera.main, out Vector3 worldMousePos);

            UpdateWire(worldMousePos);
        }

        // 마우스 클릭 해제
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

    // 💡 [로컬 좌표 정렬 공식] 캔버스 모드가 바뀌어도 선이 일직선으로 예쁘게 따라옵니다.
    private void UpdateWire(Vector3 targetWorldPos)
    {
        if (currentLineRect == null) return;

        RectTransform panelRect = (RectTransform)transform;

        Vector3 localStart = panelRect.InverseTransformPoint(currentLineRect.position);
        Vector3 localEnd = panelRect.InverseTransformPoint(targetWorldPos);

        Vector3 dir = localEnd - localStart;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        currentLineRect.localRotation = Quaternion.Euler(0, 0, angle);
        currentLineRect.sizeDelta = new Vector2(dir.magnitude, wireThickness);
    }

    private void CheckConnection(Vector2 dropPosition)
    {
        Color draggedColor = leftPoints[draggingIndex].color;
        bool isConnected = false;
        bool droppedOnWrong = false;

        for (int i = 0; i < rightPoints.Length; i++)
        {
            if (rightPoints[i].color.a < 1f) continue;

            // 💡 여기도 똑같이 Camera.main 적용
            if (RectTransformUtility.RectangleContainsScreenPoint(rightPoints[i].rectTransform, dropPosition, Camera.main))
            {
                if (SameColor(rightPoints[i].color, draggedColor))
                {
                    isConnected = true;
                    connectedWires++;

                    UpdateWire(rightPoints[i].transform.position);
                    completedLines[draggingIndex] = currentDrawingLine;

                    PlaySFX(clickClip, true);

                    // 💡 [파티클 추가] 선이 성공적으로 달라붙은 우측 도착점에서 파티클 폭발!
                    PlayParticle(rightPoints[i].rectTransform);

                    PunchUI(leftPoints[draggingIndex].rectTransform, 1.4f, 0.2f);
                    PunchUI(rightPoints[i].rectTransform, 1.4f, 0.2f);

                    Image wireImg = currentDrawingLine.GetComponent<Image>();
                    StartCoroutine(WireFlashRoutine(wireImg, draggedColor));

                    SetImageAlpha(leftPoints[draggingIndex], 0.3f);
                    SetImageAlpha(rightPoints[i], 0.3f);

                    if (connectedWires >= leftPoints.Length)
                    {
                        if (timerText != null) timerText.text = "복구 완료!";
                        TriggerSuccess();
                    }
                    break;
                }
                else
                {
                    droppedOnWrong = true;
                }
            }
        }

        if (!isConnected)
        {
            if (droppedOnWrong)
            {
                PlaySFX(failClip, true);
                StartCoroutine(ShakeUI(GetComponent<RectTransform>(), 8f, 0.15f));
            }

            if (currentDrawingLine != null) Destroy(currentDrawingLine);
        }
    }

    private IEnumerator WireFlashRoutine(Image wireImg, Color targetColor)
    {
        if (wireImg == null) yield break;

        wireImg.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        float elapsed = 0f;
        float duration = 0.2f;
        while (elapsed < duration)
        {
            if (wireImg == null) break;
            elapsed += Time.deltaTime;
            wireImg.color = Color.Lerp(Color.white, targetColor, elapsed / duration);
            yield return null;
        }

        if (wireImg != null)
        {
            Color finalColor = targetColor;
            finalColor.a = 0.3f;
            wireImg.color = finalColor;
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