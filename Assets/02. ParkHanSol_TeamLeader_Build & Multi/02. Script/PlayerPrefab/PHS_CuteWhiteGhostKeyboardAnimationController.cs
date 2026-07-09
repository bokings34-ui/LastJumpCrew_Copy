using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Animator))]
    public sealed class PHS_CuteWhiteGhostKeyboardAnimationController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour movementSourceBehaviour;
        [SerializeField] private float crossFadeTime = 0.12f;

        private Animator animator;
        private IPlayerMovementAnimationSource movementSource;
        private int currentState;

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Fall = Animator.StringToHash("Fall");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (movementSourceBehaviour != null)
            {
                movementSource = movementSourceBehaviour as IPlayerMovementAnimationSource;
            }

            if (movementSource == null)
            {
                foreach (var behaviour in GetComponents<MonoBehaviour>())
                {
                    if (!behaviour.isActiveAndEnabled)
                    {
                        continue;
                    }

                    movementSource = behaviour as IPlayerMovementAnimationSource;
                    if (movementSource != null)
                    {
                        movementSourceBehaviour = behaviour;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (movementSource == null)
            {
                Play(Idle);
                return;
            }

            if (!movementSource.IsGrounded)
            {
                Play(movementSource.VerticalVelocity > 0.05f ? Jump : Fall);
                return;
            }

            if (!movementSource.HasMoveInput)
            {
                Play(Idle);
                return;
            }

            Play(movementSource.IsRunning ? Run : Walk);
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
