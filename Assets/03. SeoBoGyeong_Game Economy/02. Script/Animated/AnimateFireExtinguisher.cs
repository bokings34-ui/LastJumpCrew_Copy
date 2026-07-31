using System.Collections;
using UnityEngine;

// PlayEffect/StopEffect 는 "애니메이션 이벤트"가 호출하는 파티클 제어 함수다.
// 토글은 이 클래스를 직접 부르지 않는다(중복 발화 방지). 토글 → 애니메이션 → (이벤트) → 이 함수 순서.
public class AnimateFireExtinguisher : MonoBehaviour
{
    //[SerializeField] private Transform effectPoint;
    [SerializeField] private ParticleSystem activeEffect;
    [SerializeField] private float _targetRate = 230;

    [SerializeField] private float _fadeInDuration = 1.0f;
    [SerializeField] private float _fadeOutDuration = 1.0f;
    private ParticleSystem.EmissionModule _emission;

    private void Awake()
    {
        _emission = activeEffect.emission;
    }
   IEnumerator FadeIn()
    {
        float progress  = 0f;
        while (progress < _fadeInDuration)
        {
            progress += Time.deltaTime;

            _emission.rateOverTime = Mathf.Lerp(0f,_targetRate, progress / _fadeInDuration);
            yield return null;
        }
        _emission.rateOverTime = _targetRate;
    }

    IEnumerator FadeOut()
    {
        float progress = 0f;
        while (progress < _fadeOutDuration)
        {
            progress += Time.deltaTime;
            _emission.rateOverTime = Mathf.Lerp( _targetRate,0f, progress / _fadeOutDuration);
            yield return null;
        }
        _emission.rateOverTime = 0f;
    }

    // 페이드 없이 즉시 이펙트를 끈다. (늦게 접속했거나 강제로 상태를 맞춰야 할 때 사용)
    public void ResetImmediate()
    {
        StopAllCoroutines();                 // 진행 중인 FadeIn/FadeOut 코루틴 취소
        _emission.rateOverTime = 0f;         // 분사량 즉시 0
        activeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void StartEffect()
    {
        activeEffect.Play();
        StartCoroutine(FadeIn());
    }

    public void EndEffect()
    {
        StartCoroutine(FadeOut());
        activeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
