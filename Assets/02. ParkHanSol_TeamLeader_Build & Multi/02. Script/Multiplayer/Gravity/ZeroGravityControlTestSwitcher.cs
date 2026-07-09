using LastJumpCrew.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityControlTestSwitcher : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private ShipGravityZoneController shipGravityZoneController;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_FontAsset instructionFont;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private float testGravityStrength = 18f;

        private const string PanelName = "Zero Gravity Control Test Panel";

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<NetworkPlayerController>();
            }

            if (shipGravityZoneController == null)
            {
                shipGravityZoneController = FindAnyObjectByType<ShipGravityZoneController>();
            }

            EnsureInstructionPanel();
        }

        private void Start()
        {
            RefreshInstruction("4 중력 복귀");
        }

        private void Update()
        {
            if (Keyboard.current == null || playerController == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.Direct, "1 즉시반응형");
                return;
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.Inertia, "2 관성형");
                return;
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.Hybrid, "3 절충형");
                return;
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                ReturnToShipGravity();
            }
        }

        private void ApplyZeroGravityPreset(ZeroGravityControlPreset preset, string label)
        {
            playerController.SetZeroGravityControlPreset(preset);
            playerController.ApplyGravityState(GravityState.Spacewalk(100));
            RefreshInstruction(label);
        }

        private void ReturnToShipGravity()
        {
            shipGravityZoneController?.SetGravityEnabled(true);
            playerController.ApplyGravityState(
                new GravityState(GravityMode.ShipGravity, 100, Vector3.down, testGravityStrength));
            RefreshInstruction("4 중력 복귀");
        }

        private void EnsureInstructionPanel()
        {
            if (instructionText != null)
            {
                return;
            }

            if (targetCanvas == null)
            {
                targetCanvas = FindAnyObjectByType<Canvas>();
            }

            if (targetCanvas == null)
            {
                var canvasObject = new GameObject("ParkHanSol_GravityTestCanvas");
                targetCanvas = canvasObject.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var existingPanel = targetCanvas.transform.Find(PanelName);
            if (existingPanel != null)
            {
                instructionText = existingPanel.GetComponentInChildren<TMP_Text>(true);
                if (instructionText != null)
                {
                    ApplyInstructionFont();
                    return;
                }
            }

            var panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(targetCanvas.transform, false);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.5f);
            panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(24f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 290f);

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            var textObject = new GameObject("Instruction Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 12f);
            textRect.offsetMax = new Vector2(-16f, -12f);

            instructionText = textObject.GetComponent<TextMeshProUGUI>();
            ApplyInstructionFont();
            instructionText.fontSize = 18f;
            instructionText.color = Color.white;
            instructionText.alignment = TextAlignmentOptions.TopLeft;
            instructionText.enableWordWrapping = true;
        }

        private void ApplyInstructionFont()
        {
            if (instructionText != null && instructionFont != null)
            {
                instructionText.font = instructionFont;
            }
        }

        private void RefreshInstruction(string currentLabel)
        {
            if (instructionText == null)
            {
                return;
            }

            instructionText.text =
                $"ZERO-G CONTROL TEST\n" +
                $"현재: {currentLabel}\n\n" +
                $"WASD: 바라보는 방향 기준 이동\n" +
                $"Space 상승 / Ctrl 하강\n\n" +
                $"1 즉시반응형\n" +
                $"입력 즉시 이동, 손 떼면 바로 멈춤\n\n" +
                $"2 관성형\n" +
                $"입력 방향으로 계속 가속\n" +
                $"손 떼도 계속 떠감\n\n" +
                $"3 절충형\n" +
                $"부드럽게 가속, 천천히 감속\n" +
                $"Shift 부스터\n\n" +
                $"4 중력존 복귀\n\n" +
                $"구슬 충돌 테스트\n" +
                $"플레이어 질량 기준: 1\n" +
                $"구슬 질량: 0.5 / 1 / 2 / 3.5 / 5\n" +
                $"무중력: 부딪히면 계속 밀려남\n" +
                $"중력존: 테스트 중에도 켜짐, 들어가면 떨어져 굴러감";
        }
    }
}
