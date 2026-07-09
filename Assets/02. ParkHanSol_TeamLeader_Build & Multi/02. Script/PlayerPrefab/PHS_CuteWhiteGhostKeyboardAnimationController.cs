using UnityEngine;
using Unity.Netcode;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Animator))]
    public sealed class PHS_CuteWhiteGhostKeyboardAnimationController : MonoBehaviour
    {
        [SerializeField] private PHS_CuteWhiteGhostFirstPersonController controller;
        [SerializeField] private MonoBehaviour movementSourceBehaviour;
        [SerializeField] private float crossFadeTime = 0.12f;

        private Animator animator;
        private NetworkPlayerController networkController;
        private int currentState;

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Fall = Animator.StringToHash("Fall");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            networkController = movementSourceBehaviour as NetworkPlayerController;
            if (controller == null)
            {
                controller = GetComponent<PHS_CuteWhiteGhostFirstPersonController>();
            }

            if (networkController == null)
            {
                networkController = GetComponent<NetworkPlayerController>();
            }
        }

        private void Update()
        {
            if (ShouldUseNetworkMovement())
            {
                PlayNetworkMovement();
                return;
            }

            if (controller == null)
            {
                Play(Idle);
                return;
            }

            if (!controller.IsGrounded)
            {
                Play(controller.VerticalVelocity > 0.05f ? Jump : Fall);
                return;
            }

            if (!controller.HasMoveInput)
            {
                Play(Idle);
                return;
            }

            Play(controller.IsRunning ? Run : Walk);
        }

        private bool ShouldUseNetworkMovement()
        {
            return networkController != null && networkController.enabled;
        }

        private void PlayNetworkMovement()
        {
            if (!networkController.IsGrounded)
            {
                Play(networkController.VerticalVelocity > 0.05f ? Jump : Fall);
                return;
            }

            if (!networkController.HasMoveInput)
            {
                Play(Idle);
                return;
            }

            Play(networkController.IsRunning ? Run : Walk);
        }

        private void Play(int stateHash)
        {
            if (currentState == stateHash)
            {
                return;
            }

            currentState = stateHash;
            animator.CrossFade(stateHash, crossFadeTime);
        }
    }
}
