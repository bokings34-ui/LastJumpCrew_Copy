using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WarpController : MonoBehaviour
{
    [Header("연결할 컴포넌트들")]
    public Volume globalVolume;
    public ParticleSystem warpParticles;
    public CanvasGroup fadeCanvasGroup;
    public Camera mainCamera;

    [Header("파티클 늘이기 설정")]
    public float particleStretchMin = 5f;
    public float particleStretchMax = 50f;
    public float particleStretchDuration = 1.0f;

    [Header("파티클 색상 설정")]
    [Tooltip("파티클 색상이 무작위로 서서히 변하는 데 걸리는 시간(초)")]
    public float colorChangeDuration = 1.0f; // 인스펙터에서 조절 가능

    private LensDistortion lensDistortion;
    private ChromaticAberration chromaticAberration;
    private float defaultFOV;

    void Start()
    {
        // 1. 카메라 및 볼륨 컴포넌트 초기 세팅
        if (mainCamera != null) defaultFOV = mainCamera.fieldOfView;

        if (globalVolume.profile.TryGet(out lensDistortion) == false)
            globalVolume.profile.Add<LensDistortion>();

        if (globalVolume.profile.TryGet(out chromaticAberration) == false)
            globalVolume.profile.Add<ChromaticAberration>();

        // 2. 실행 시 연출 시작
        StartCoroutine(WarpTestSequence());
    }

    private IEnumerator WarpTestSequence()
    {
        var particleRenderer = warpParticles.GetComponent<ParticleSystemRenderer>();

        // 파티클 늘이기 코루틴과 색상 변경 코루틴을 동시에 실행!
        StartCoroutine(StretchParticles(particleRenderer));
        StartCoroutine(ChangeParticleColor(warpParticles, colorChangeDuration));

        float elapsedTime = 0f;
        float warpDuration = 1.0f;

        // 메인 연출: 시야각 넓어짐 + 시공간 일그러짐
        while (elapsedTime < warpDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / warpDuration;

            mainCamera.fieldOfView = Mathf.Lerp(defaultFOV, defaultFOV + 40f, t);
            lensDistortion.intensity.value = Mathf.Lerp(0, -0.6f, t);
            chromaticAberration.intensity.value = Mathf.Lerp(0, 1f, t);

            yield return null;
        }

        // 화면 암전 (페이드 아웃)
        elapsedTime = 0f;
        float fadeDuration = 0.5f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, elapsedTime / fadeDuration);
            yield return null;
        }

        Debug.Log("워프 연출 테스트 완료! (무작위 색상 변경 포함)");
    }

    // 파티클의 Length Scale을 늘이는 코루틴
    private IEnumerator StretchParticles(ParticleSystemRenderer renderer)
    {
        float elapsedTime = 0f;
        while (elapsedTime < particleStretchDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / particleStretchDuration;
            renderer.lengthScale = Mathf.Lerp(particleStretchMin, particleStretchMax, t);
            yield return null;
        }
        renderer.lengthScale = particleStretchMax;
    }

    // [새로 추가됨] 파티클 색상을 서서히 무작위로 변경하는 코루틴
    private IEnumerator ChangeParticleColor(ParticleSystem ps, float duration)
    {
        // 파티클 시스템의 메인 모듈 접근
        var main = ps.main;

        // 현재 설정되어 있는 초기 색상 저장
        Color initialColor = main.startColor.color;

        // Random.ColorHSV(HueMin, HueMax, SaturationMin, SaturationMax, ValueMin, ValueMax)
        // 채도(Saturation)와 명도(Value)를 높게 설정하여 어둡거나 탁한 색이 나오지 않게 방지합니다.
        Color randomTargetColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Color.Lerp를 통해 시작 색상에서 목표 무작위 색상으로 서서히 전환
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(initialColor, randomTargetColor, t));
            yield return null;
        }

        // 시간이 다 끝나면 목표 색상으로 완벽하게 고정
        main.startColor = new ParticleSystem.MinMaxGradient(randomTargetColor);
    }
}