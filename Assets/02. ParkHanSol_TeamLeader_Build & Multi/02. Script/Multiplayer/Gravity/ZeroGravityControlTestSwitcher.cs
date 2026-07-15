using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityControlTestSwitcher : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private Transform resetPoint;
        private void Awake()
        {
            if (playerController == null)
            {
                Debug.LogError($"PHS_ZERO_GRAVITY_SWITCHER_SETUP_FAILED reason=player_controller_missing switcher={name}");
            }

            if (resetPoint == null)
            {
                Debug.LogError($"PHS_ZERO_GRAVITY_SWITCHER_SETUP_FAILED reason=reset_point_missing switcher={name}");
            }

        }

        private void Start()
        {
            if (playerController == null)
            {
                return;
            }

            ResetPlayerForCinematicControlTest();
        }

        private void ResetPlayerForCinematicControlTest()
        {
            if (resetPoint == null)
            {
                Debug.LogError("PHS_ZERO_GRAVITY_TEST_SETUP_FAILED reason=reset_point_missing");
                return;
            }

            playerController.RequestTestTeleport(resetPoint.position, resetPoint.rotation);
            Debug.Log($"PHS_ZERO_GRAVITY_TEST_READY control=cinematic_spacebar_thruster position={resetPoint.position}");
        }
    }
}
