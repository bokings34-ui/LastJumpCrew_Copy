namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// "이펙트를 켜고 끄는" 표현(연출) 컴포넌트가 지켜야 할 최소 계약이다.
    /// NetworkedEffectToggle 은 구체 클래스(AnimateFireExtinguisher 등)를 직접 모르고
    /// 이 인터페이스만 호출한다 → 소화기·연기·전기불꽃 등 어떤 이펙트든 그대로 재사용된다.
    ///
    /// 구현체 예: AnimateFireExtinguisher(파티클 emission 을 서서히 올렸다/내렸다 한다).
    /// </summary>
    public interface IEffectPresenter
    {
        /// <summary>이펙트 재생 시작. (예: 파티클 Play + FadeIn)</summary>
        void PlayEffect();

        /// <summary>이펙트 정지. (예: 파티클 FadeOut 후 StopEmitting)</summary>
        void StopEffect();
    }
}
