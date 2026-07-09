using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Animator))]
    public sealed class PHS_CuteWhiteGhostKeyboardAnimationController : MonoBehaviour
    {
        [SerializeField] private PHS_CuteWhiteGhostFirstPersonController controller;
        [SerializeField] private float crossFadeTime = 0.12f;

        private Animator animator;
        private int currentState;

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Fall = Animator.StringToHash("Fall");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (controller == null)
            {
                controller = GetComponent<PHS_CuteWhiteGhostFirstPersonController>();
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                Play(Idle);
                return;
            }

            if (!controller.IsGrounded)
            {
                Play(GetComponent<Rigidbody>().linearVelocity.y > 0.05f ? Jump : Fall);
                return;
            }

            if (!controller.HasMoveInput)
            {
                Play(Idle);
                return;
            }

            Play(controller.IsRunning ? Run : Walk);
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
