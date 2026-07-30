using System.Collections;
using UnityEngine;

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

    public void ResetImmediate()
    {
       
    }

    public void PlayEffect()
    {
        activeEffect.Play();
        StartCoroutine(FadeIn());
    }

    public void StopEffect()
    {
        StartCoroutine(FadeOut());
        activeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
