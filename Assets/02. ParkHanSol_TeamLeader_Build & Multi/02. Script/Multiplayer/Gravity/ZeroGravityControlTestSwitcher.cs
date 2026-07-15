using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityControlTestSwitcher : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private Transform resetPoint;
        [SerializeField] private ShipGravityZoneController shipGravityController;
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

        private void Update()
        {
            if (Keyboard.current == null || shipGravityController == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                shipGravityController.TurnGravityOn();
                Debug.Log("PHS_GRAVITY_TEST_MODE mode=ship_gravity input=1");
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                shipGravityController.TurnGravityOff();
                Debug.Log("PHS_GRAVITY_TEST_MODE mode=zero_gravity input=2");
            }
            else if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetPlayerForCinematicControlTest();
            }
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
