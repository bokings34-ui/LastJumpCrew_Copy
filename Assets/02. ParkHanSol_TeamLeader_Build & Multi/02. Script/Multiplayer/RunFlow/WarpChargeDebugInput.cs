using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class WarpChargeDebugInput : MonoBehaviour
    {
        [SerializeField] private Key inputKey = Key.Digit1;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!Debug.isDebugBuild
                || keyboard == null
                || !keyboard[inputKey].wasPressedThisFrame)
            {
                return;
            }

            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogWarning("PHS_RUN_FLOW_DEBUG_CHARGE_REJECTED reason=coordinator_missing", this);
                return;
            }

            if (!coordinator.RequestCompleteWarpChargeForDebug())
            {
                Debug.LogWarning(
                    $"PHS_RUN_FLOW_DEBUG_CHARGE_REJECTED reason=request_unavailable phase={coordinator.Phase}",
                    this);
            }
        }
    }
}
