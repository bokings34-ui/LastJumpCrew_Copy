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

    private Camera GetUICamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return canvas.worldCamera;
        }
        return null;
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

        UpdateDangerPulse();

        if (timeRemaining <= 0)
        {
            if (timerText != null) timerText.text = "시간 초과!";
            TriggerFailure();
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Camera uiCam = GetUICamera();

        // 마우스 클릭 시작
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            for (int i = 0; i < leftPoints.Length; i++)
            {
                if (leftPoints[i].color.a < 1f) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(leftPoints[i].rectTransform, mousePos, uiCam))
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
            // 💡 [핵심 해결 1] WorldPoint가 아니라 LocalPoint(2D 도화지 좌표)로 바로 변환합니다!
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, mousePos, uiCam, out Vector2 localMousePos);

            UpdateWire(localMousePos);
        }

        // 마우스 클릭 해제
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (draggingIndex != -1)
            {
                CheckConnection(mousePos, uiCam);
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

    // 💡 [핵심 해결 2] 인자값을 Vector3(월드)에서 Vector2(로컬)로 바꾸고 공식을 극도로 단순화했습니다.
    private void UpdateWire(Vector2 targetLocalPos)
    {
        if (currentLineRect == null) return;

        // 시작점을 월드 좌표에서 변환할 필요 없이, 선의 현재 로컬 좌표를 그대로 쓰면 됩니다.
        Vector2 localStart = currentLineRect.localPosition;
        Vector2 localEnd = targetLocalPos;

        Vector2 dir = localEnd - localStart;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        currentLineRect.localRotation = Quaternion.Euler(0, 0, angle);
        currentLineRect.sizeDelta = new Vector2(dir.magnitude, wireThickness);
    }

    private void CheckConnection(Vector2 dropPosition, Camera uiCam)
    {
        Color draggedColor = leftPoints[draggingIndex].color;
        bool isConnected = false;
        bool droppedOnWrong = false;

        for (int i = 0; i < rightPoints.Length; i++)
        {
            if (rightPoints[i].color.a < 1f) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(rightPoints[i].rectTransform, dropPosition, uiCam))
            {
                if (SameColor(rightPoints[i].color, draggedColor))
                {
                    isConnected = true;
                    connectedWires++;

                    // 💡 [핵심 해결 3] 도착점의 월드 좌표도 패널 안의 2D 로컬 좌표로 변환해서 넘겨줍니다.
                    Vector2 endLocalPos = ((RectTransform)transform).InverseTransformPoint(rightPoints[i].transform.position);
                    UpdateWire(endLocalPos);

                    completedLines[draggingIndex] = currentDrawingLine;

                    PlaySFX(clickClip, true);
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