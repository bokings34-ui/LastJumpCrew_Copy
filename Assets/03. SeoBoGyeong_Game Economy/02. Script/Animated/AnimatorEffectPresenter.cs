using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// NetworkedEffectToggle 의 on/off 를 "애니메이터 bool 파라미터"로 바꿔주는 표현 컴포넌트.
    /// 토글이 켜지면 bool=true → 애니메이션 재생 시작, 꺼지면 bool=false → 애니메이션 종료.
    ///
    /// [파티클 타이밍] 파티클은 이 컴포넌트가 직접 켜지 않는다.
    ///  애니메이션 클립 안의 Animation Event 가 정확한 프레임에 파티클을 발화한다(이미 그렇게 제작됨).
    ///  → 각 클라이언트가 같은(동기화된) 애니메이션을 재생하므로 파티클도 알아서 같은 타이밍에 뜬다.
    ///
    /// [배치] Animator 와 같은 GameObject 에 붙이고, NetworkedEffectToggle 의 Presenter Sources 에 넣는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorEffectPresenter : MonoBehaviour, IEffectPresenter
    {
        [SerializeField] private Animator animator;

        [Tooltip("켜짐/꺼짐을 나타내는 애니메이터 bool 파라미터 이름.")]
        [SerializeField] private string boolParameter = "IsActive";

        // 매번 문자열 비교를 피하려고 해시로 캐싱한다.
        private int _paramHash;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            _paramHash = Animator.StringToHash(boolParameter);

            if (animator == null)
            {
                Debug.LogError($"ANIMATOR_PRESENTER_FAILED reason=animator_missing obj={name}", this);
            }
        }

        // 토글이 켜질 때 호출 → 애니메이션 시작.
        public void PlayEffect()
        {
            if (animator == null)
            {
                return;
            }
            animator.SetBool(_paramHash, true);
        }

        // 토글이 꺼질 때 호출 → 애니메이션 종료(파티클은 클립의 종료 이벤트/자체 페이드로 정리).
        public void StopEffect()
        {
            if (animator == null)
            {
                return;
            }
            animator.SetBool(_paramHash, false);
        }
    }
}
