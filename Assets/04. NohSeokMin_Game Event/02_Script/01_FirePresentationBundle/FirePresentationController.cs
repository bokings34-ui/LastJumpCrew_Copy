using UnityEngine;
using DG.Tweening;

namespace SM
{
    // Presentation-only. 서버 상태/피해/확산 판정 없음. Collider/NetworkObject 없음.
    // TuriShader(Mesh + Material + Light) 기반. _Verticalcut으로 나타남/사라짐,
    // _TurbulenceSpeed + HDR Color로 강도 표현.
    public class FirePresentationController : MonoBehaviour
    {
        [Header("비주얼 구성")]
        [SerializeField] private MeshRenderer fireMeshRenderer;
        [SerializeField] private Light fireLight;
        [SerializeField] private AudioSource fireAudio;

        private static readonly int VerticalCutId = Shader.PropertyToID("_Verticalcut");
        private static readonly int TurbulenceSpeedId = Shader.PropertyToID("_TurbulenceSpeed");
        private static readonly int ColorOutId = Shader.PropertyToID("_ColorOut");
        private static readonly int ColorInId = Shader.PropertyToID("_ColorIn");

        [Header("강도별 설정")]
        [SerializeField] private IntensitySettings smallSettings;
        [SerializeField] private IntensitySettings mediumSettings;
        [SerializeField] private IntensitySettings largeSettings;

        [Header("전환 시간")]
        [SerializeField] private float telegraphDuration = 1.5f;
        [SerializeField] private float extinguishDuration = 1.2f;
        [SerializeField] private float intensityTransitionDuration = 0.8f;

        [Header("Telegraph 시 노출 정도 (0=완전노출, 1=완전은폐)")]
        [SerializeField] private float telegraphVerticalCut = 0.7f; // 살짝만 보이는 상태

        [System.Serializable]
        public class IntensitySettings
        {
            public float lightIntensity = 2f;
            public float lightRange = 4f;
            public float meshScale = 1f;
            public float turbulenceSpeed = 1f;
            [ColorUsage(true, true)] public Color colorOut = new Color(1f, 0.4f, 0f, 1f);
            [ColorUsage(true, true)] public Color colorIn = new Color(1f, 0.8f, 0f, 1f);
            public float audioVolume = 0.7f;
        }

        private Material _materialInstance;
        private Sequence _activeSequence;
        private FireIntensity _currentIntensity;
        private bool _isActive;
        private bool _isInitialized;
        private Vector3 _baseMeshScale;

        private void Awake()
        {
            EnsureInitialized();
        }

        // ---- 외부 진입점 (박한솔님 Runtime이 호출) ----

        public void Telegraph()
        {
            EnsureInitialized();
            KillActiveTween();
            ResetVisualsToZero();

            fireLight.enabled = true;

            _activeSequence = DOTween.Sequence();
            _activeSequence.Join(DOTween.To(() => fireLight.intensity, v => fireLight.intensity = v, 0.5f, telegraphDuration).SetEase(Ease.InSine));
            _activeSequence.Join(DOTween.To(
                () => _materialInstance.GetFloat(VerticalCutId),
                v => _materialInstance.SetFloat(VerticalCutId, v),
                telegraphVerticalCut, telegraphDuration));
        }

        public void Activate(FireIntensity intensity)
        {
            KillActiveTween();

            _isActive = true;
            _currentIntensity = intensity;

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
            _activeSequence.Join(DOTween.To(() => fireLight.intensity, v => fireLight.intensity = v, target.lightIntensity, intensityTransitionDuration));
            _activeSequence.Join(DOTween.To(() => fireLight.range, v => fireLight.range = v, target.lightRange, intensityTransitionDuration));
            _activeSequence.Join(fireMeshRenderer.transform.DOScale(_baseMeshScale * target.meshScale, intensityTransitionDuration));
            _activeSequence.Join(DOTween.To(
                () => _materialInstance.GetFloat(TurbulenceSpeedId),
                v => _materialInstance.SetFloat(TurbulenceSpeedId, v),
                target.turbulenceSpeed, intensityTransitionDuration));
            _activeSequence.Join(_materialInstance.DOColor(target.colorOut, ColorOutId, intensityTransitionDuration));
            _activeSequence.Join(_materialInstance.DOColor(target.colorIn, ColorInId, intensityTransitionDuration));
            _activeSequence.Join(fireAudio.DOFade(target.audioVolume, intensityTransitionDuration));
        }

        public void Extinguish()
        {
            KillActiveTween();
            _isActive = false;

            _activeSequence = DOTween.Sequence();
            _activeSequence.Join(DOTween.To(() => fireLight.intensity, v => fireLight.intensity = v, 0f, extinguishDuration));
            _activeSequence.Join(fireMeshRenderer.transform.DOScale(0f, extinguishDuration));
            _activeSequence.Join(DOTween.To(
                () => _materialInstance.GetFloat(VerticalCutId),
                v => _materialInstance.SetFloat(VerticalCutId, v),
                1f, extinguishDuration)); // 완전히 잘려서 안 보이게
            _activeSequence.Join(fireAudio.DOFade(0f, extinguishDuration));
            _activeSequence.OnComplete(() =>
            {
                fireLight.enabled = false;
                fireAudio.Stop();
            });
        }

        // 재사용(풀링) 대비 완전 초기화
        public void ResetPresentation()
        {
            EnsureInitialized();
            KillActiveTween();

            fireAudio.Stop();
            fireAudio.volume = 0f;

            fireLight.enabled = false;
            fireLight.intensity = 0f;

            fireMeshRenderer.transform.localScale = _baseMeshScale;
            _materialInstance.SetFloat(VerticalCutId, 1f); // 완전히 안 보이는 상태로 초기화
            _materialInstance.SetFloat(TurbulenceSpeedId, 1f);

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
            _materialInstance.SetFloat(VerticalCutId, 1f);
        }

        private void ApplyIntensityInstant(IntensitySettings settings)
        {
            fireLight.intensity = settings.lightIntensity;
            fireLight.range = settings.lightRange;
            fireMeshRenderer.transform.localScale = _baseMeshScale * settings.meshScale;
            _materialInstance.SetFloat(TurbulenceSpeedId, settings.turbulenceSpeed);
            _materialInstance.SetFloat(VerticalCutId, 0f); // 완전히 보이는 상태
            _materialInstance.SetColor(ColorOutId, settings.colorOut);
            _materialInstance.SetColor(ColorInId, settings.colorIn);
            fireAudio.volume = settings.audioVolume;
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

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            _baseMeshScale = fireMeshRenderer.transform.localScale;
            _materialInstance = fireMeshRenderer.material;
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            KillActiveTween();

            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
            }
        }
    }
}
