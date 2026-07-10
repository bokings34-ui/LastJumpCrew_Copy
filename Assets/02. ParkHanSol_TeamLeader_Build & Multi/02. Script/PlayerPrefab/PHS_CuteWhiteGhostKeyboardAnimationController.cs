using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PHS_CuteWhiteGhostKeyboardAnimationController : MonoBehaviour
    {
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
            if (networkController == null)
            {
                networkController = GetComponent<NetworkPlayerController>();
            }
        }

        private void Update()
        {
            if (networkController == null)
            {
                Play(Idle);
                return;
            }

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
