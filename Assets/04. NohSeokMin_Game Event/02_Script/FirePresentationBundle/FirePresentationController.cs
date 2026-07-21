using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace SM
{
    // Presentation-only. 서버 상태/피해/확산 판정 없음. Collider/NetworkObject 없음.
    public class FirePresentationController : MonoBehaviour
    {
        [Header("VFX")]
        [SerializeField] private ParticleSystem flameVfx;
        [SerializeField] private ParticleSystem smokeVfx;
        [SerializeField] private ParticleSystem emberVfx;
        [SerializeField] private Light fireLight;
        [SerializeField] private AudioSource fireAudio;

        [Header("강도별 설정")]
        [SerializeField] private IntensitySettings smallSettings;
        [SerializeField] private IntensitySettings mediumSettings;
        [SerializeField] private IntensitySettings largeSettings;

        [Header("전환 시간")]
        [SerializeField] private float telegraphDuration = 1.5f;
        [SerializeField] private float extinguishDuration = 1.2f;
        [SerializeField] private float intensityTransitionDuration = 0.8f;

        [System.Serializable]
        public class IntensitySettings
        {
            public float lightIntensity = 2f;
            public float lightRange = 4f;
            public float flameScale = 1f;
            public float smokeEmissionRate = 5f;
            public float audioVolume = 0.7f;
        }

        private Sequence _activeSequence;
        private FireIntensity _currentIntensity;
        private bool _isActive;

        // ---- 외부 진입점 (박한솔님 Runtime이 호출) ----

        public void Telegraph()
        {
            KillActiveTween();
            ResetVisualsToZero();

            fireLight.enabled = true;
            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(fireLight.DOIntensity(0.5f, telegraphDuration).SetEase(Ease.InSine));
            _activeSequence.Join(smokeVfx.transform.DOScale(0.3f, telegraphDuration));
            _activeSequence.OnStart(() =>
            {
                smokeVfx.Play();
            });
        }

        public void Activate(FireIntensity intensity)
        {
            KillActiveTween();

            _isActive = true;
            _currentIntensity = intensity;

            flameVfx.Play();
            emberVfx.Play();
            fireAudio.Play();

            ApplyIntensityInstant(GetSettings(intensity));
        }

        // 서버가 Heat/Intensity 변화를 알려줄 때마다 호출
        public void SetIntensity(FireIntensity intensity)
        {
            if (!_isActive) return;
            if (_currentIntensity == intensity) return;

            _currentIntensity = intensity;
            KillActiveTween();

            var target = GetSettings(intensity);

            _activeSequence = DOTween.Sequence();
            _activeSequence.Join(fireLight.DOIntensity(target.lightIntensity, intensityTransitionDuration));

            // TODO :: 0721 수정할 것
            //_activeSequence.Join(fireLight.DOTweenValueTo(fireLight.range, target.lightRange, intensityTransitionDuration, v => fireLight.range = v));
            _activeSequence.Join(flameVfx.transform.DOScale(target.flameScale, intensityTransitionDuration));
            _activeSequence.Join(fireAudio.DOFade(target.audioVolume, intensityTransitionDuration));
            _activeSequence.OnUpdate(() =>
            {
                var emission = smokeVfx.emission;
                emission.rateOverTime = Mathf.Lerp(emission.rateOverTime.constant, target.smokeEmissionRate, Time.deltaTime * 4f);
            });
        }

        public void Extinguish()
        {
            KillActiveTween();
            _isActive = false;

            _activeSequence = DOTween.Sequence();
            _activeSequence.Join(fireLight.DOIntensity(0f, extinguishDuration));
            _activeSequence.Join(flameVfx.transform.DOScale(0f, extinguishDuration));
            _activeSequence.Join(fireAudio.DOFade(0f, extinguishDuration));
            _activeSequence.OnComplete(() =>
            {
                flameVfx.Stop();
                emberVfx.Stop();
                fireAudio.Stop();
                fireLight.enabled = false;
            });
        }

        // 재사용 대비 완전 초기화. 파티클/오디오/트윈.
        public void ResetPresentation()
        {
            KillActiveTween();

            flameVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emberVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            fireAudio.Stop();
            fireAudio.volume = 0f;

            fireLight.enabled = false;
            fireLight.intensity = 0f;

            _isActive = false;
            _currentIntensity = FireIntensity.Small;
        }

        // ---- 내부 유틸 ----

        private void KillActiveTween()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }

        private void ResetVisualsToZero()
        {
            fireLight.intensity = 0f;
            fireAudio.volume = 0f;
        }

        private void ApplyIntensityInstant(IntensitySettings settings)
        {
            fireLight.intensity = settings.lightIntensity;
            fireLight.range = settings.lightRange;
            flameVfx.transform.localScale = Vector3.one * settings.flameScale;
            fireAudio.volume = settings.audioVolume;

            var emission = smokeVfx.emission;
            emission.rateOverTime = settings.smokeEmissionRate;
        }

        private IntensitySettings GetSettings(FireIntensity intensity)
        {
            switch (intensity)
            {
                case FireIntensity.Small: return smallSettings;
                case FireIntensity.Medium: return mediumSettings;
                default: return largeSettings;
            }
        }

        private void OnDestroy()
        {
            KillActiveTween();
        }
    }
}