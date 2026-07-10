using LastJumpCrew.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityControlTestSwitcher : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerController playerController;
        private void Awake()
        {
            if (playerController == null)
            {
                Debug.LogError($"PHS_ZERO_GRAVITY_SWITCHER_SETUP_FAILED reason=player_controller_missing switcher={name}");
            }

        }

        private void Start()
        {
            if (playerController == null)
            {
                return;
            }

            ApplyZeroGravityPreset(ZeroGravityControlPreset.Thruster);
        }

        private void Update()
        {
            if (Keyboard.current == null || playerController == null)
            {
                return;
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ApplyZeroGravityPreset(ZeroGravityControlPreset.Thruster);
            }
        }

        private void ApplyZeroGravityPreset(ZeroGravityControlPreset preset)
        {
            playerController.SetZeroGravityControlPreset(preset);
            playerController.ApplyGravityState(GravityState.Spacewalk(100));
        }
    }
}
