using LastJumpCrew.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityControlTestSwitcher : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private ShipGravityZoneController shipGravityZoneController;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text thrusterGaugeText;
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
            RefreshThrusterGaugeText();

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
                return;
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.Thruster, "5 추진제 게이지형");
                return;
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.ThrusterOnly, "6 추진제 전용형");
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
            if (instructionText == null)
            {
                Debug.LogError($"PHS_ZERO_GRAVITY_UI_SETUP_FAILED reason=instruction_text_missing switcher={name}");
                return;
            }

            ApplyInstructionFont();
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
                $"무중력 이동 테스트\n" +
                $"현재: {currentLabel}\n\n" +
                $"1~3 이동: WASD (바라보는 방향 기준)\n" +
                $"하강: Ctrl\n" +
                $"추진 게이지는 중력 상태에서도 자동 회복\n\n" +
                $"1 즉시반응형\n" +
                $"입력 즉시 이동, 손 떼면 바로 멈춤\n\n" +
                $"2 관성형\n" +
                $"입력 방향으로 계속 가속\n" +
                $"손 떼도 계속 떠감\n\n" +
                $"3 절충형\n" +
                $"부드럽게 가속, 천천히 감속\n" +
                $"Shift 부스터\n\n" +
                $"4 중력 복귀\n\n" +
                $"5 추진제 게이지형\n" +
                $"Space만 사용: 카메라 정면으로 추진\n" +
                $"WASD 이동 없음\n" +
                $"추진제 게이지 소모 후 자동 회복\n\n" +
                $"6 추진제 전용형\n" +
                $"WASD: 카메라 기준 방향으로 추진\n" +
                $"모든 이동에 추진제 게이지 소모\n\n" +
                $"데브리 충돌 테스트\n" +
                $"플레이어 질량: 1\n" +
                $"무중력: 충돌 후 계속 밀려남\n" +
                $"중력 구역: 진입하면 떨어지고 굴러감";
        }

        private void RefreshThrusterGaugeText()
        {
            if (thrusterGaugeText == null || playerController == null)
            {
                return;
            }

            thrusterGaugeText.text = $"추진제 게이지  {playerController.ThrusterFuelNormalized * 100f:0}%";
        }
    }
}
