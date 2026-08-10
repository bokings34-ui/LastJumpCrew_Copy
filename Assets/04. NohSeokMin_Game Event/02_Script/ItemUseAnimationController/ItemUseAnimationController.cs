using LastJumpCrew.SeoBoGyeong;
using System.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [RequireComponent(typeof(Animator))]
    public sealed class ItemUseAnimationController : MonoBehaviour
    {
        [SerializeField] private float crossFadeTime = 0.05f;
        [SerializeField] private NetworkPlayerLocomotionAnimator locomotionAnimator;

        private Animator animator;
        private Coroutine activeRoutine;

        private static readonly int Throw = Animator.StringToHash("Armature|Throw");
        private static readonly int Swing = Animator.StringToHash("Armature|Swing_DEF");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            locomotionAnimator ??= GetComponent<NetworkPlayerLocomotionAnimator>();
        }

        public void PlayThrow()
        {
            PlayAction(Throw);
        }

        public void PlaySwing()
        {
            PlayAction(Swing);
        }

        private void PlayAction(int stateHash)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }
            activeRoutine = StartCoroutine(PlayActionRoutine(stateHash));
        }

        private IEnumerator PlayActionRoutine(int stateHash)
        {
            locomotionAnimator?.SetSuspended(true);

            animator.Play(stateHash, 0, 0f);
            yield return null;

            var length = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(length);

            locomotionAnimator?.SetSuspended(false);
            activeRoutine = null;
        }
    }
}