using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime
{
    public class PHSGlitchEffectManager : MonoBehaviour
    {
        public static PHSGlitchEffectManager Instance;

        public Volume globalVolume;

        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;
        private Vignette vignette; // 💡 비네트 효과 변수 추가

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (globalVolume != null && globalVolume.profile != null)
            {
                globalVolume.profile.TryGet(out chromaticAberration);
                globalVolume.profile.TryGet(out lensDistortion);
                globalVolume.profile.TryGet(out vignette); // 💡 비네트 연결
            }
        }

        // 💡 시간이 촉박할 때(2초 이하) 부를 함수
        // 💡 시간이 촉박할 때(2초 이하) 부를 함수
        // 💡 시간이 촉박할 때(2초 이하) 부를 함수
        public void UpdateVignettePulse(float timeRemaining)
        {
            if (vignette == null) return;

            if (timeRemaining > 0 && timeRemaining <= 2.0f)
            {
                float pulseSpeed = 5f + (2.0f - timeRemaining) * 1.5f;
                float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

                // 💡 여기를 수정했습니다! 최대값을 0.35f에서 0.65f로 대폭 올렸습니다.
                // 만약 더 좁아지길 원하시면 0.8f까지 올려보셔도 좋습니다.
                vignette.intensity.value = Mathf.Lerp(0.15f, 0.65f, pulse);
            }
            else
            {
                vignette.intensity.value = 0f;
            }
        }

        // 기존의 글리치(실패) 연출
        public void TriggerGlitch(float duration = 0.3f)
        {
            StartCoroutine(GlitchRoutine(duration));
        }

        private IEnumerator GlitchRoutine(float duration)
        {
            if (chromaticAberration == null || lensDistortion == null) yield break;

            float halfDuration = duration / 2f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, t);
                lensDistortion.intensity.value = Mathf.Lerp(0f, -0.5f, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                chromaticAberration.intensity.value = Mathf.Lerp(1f, 0f, t);
                lensDistortion.intensity.value = Mathf.Lerp(-0.5f, 0f, t);
                yield return null;
            }

            chromaticAberration.intensity.value = 0f;
            lensDistortion.intensity.value = 0f;
        }
    }
}
