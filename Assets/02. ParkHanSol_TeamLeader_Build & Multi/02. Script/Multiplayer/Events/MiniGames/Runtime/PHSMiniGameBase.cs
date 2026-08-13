using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime
{
    // 💡 [여기 추가됨!] 모든 미니게임이 공통으로 참조할 수 있도록 Base 스크립트 최상단에 배치했습니다.
    public enum PHSMiniGameType
    {
        DoorKeypad = 0,
        WireFix = 1,
        PowerSync = 2,
        Cannon = 3
    }

    public abstract class PHSMiniGameBase : MonoBehaviour
    {
        public PHSMiniGameType gameType;
        protected IMiniGameTarget currentTarget;
        private readonly Dictionary<RectTransform, int> activePunchTargets = new();

        [Header("공통 설정")]
        public float timeLimit = 5.0f; // 제한 시간
        protected float timeRemaining;  // 남은 시간 계산용 변수
        protected bool isGameActive = false; // 미니게임이 작동 중인지 체크하는 마스터 스위치

        [Header("사운드 이펙트")]
        public AudioSource sfxSource;  // 소리를 출력할 오디오 소스 컴포넌트
        public AudioClip clickClip;    // 일반 조작 / 클릭 소리
        public AudioClip successClip;  // 미니게임 최종 성공 소리
        public AudioClip failClip;     // 미니게임 최종 실패 소리

        [Header("시각 이펙트 프리팹")]
        public GameObject clickParticlePrefab; // 유니티에서 프리팹으로 만든 파티클 에셋을 연결하는 칸

        public virtual void StartGame(IMiniGameTarget target)
        {
            currentTarget = target;
            timeRemaining = timeLimit;
            isGameActive = true;
        }

        // 💡 [버그 완전 해결] UI 구조를 부수지 않고, 눈앞에 확실하게 파티클을 띄우는 만능 함수
        public void PlayParticle(RectTransform targetUI)
        {
            if (clickParticlePrefab == null || targetUI == null) return;

            // 1. 파티클 오브젝트 복제 생성
            GameObject effectGo = Instantiate(clickParticlePrefab, targetUI.position, Quaternion.identity);

            // 2. UI 레이아웃 정렬을 위해 부모를 타겟 UI의 부모와 똑같이 맞춰줍니다.
            effectGo.transform.SetParent(targetUI.parent);
            effectGo.transform.localScale = Vector3.one; // 크기가 찌그러지는 현상 완전 방지

            // 💡 [Z축 버그 해결] 깊이 좌표를 카메라 방향(-50)으로 강제로 당겨서 UI 어두운 배경판 뒤로 파묻히지 않게 만듭니다.
            Vector3 newLocalPos = targetUI.localPosition;
            newLocalPos.z = -50f;
            effectGo.transform.localPosition = newLocalPos;

            // 💡 [레이어 버그 해결] 파티클 본체와 내부의 모든 자식 알갱이들의 레이어(Layer)를 무조건 "UI"로 강제 세팅합니다.
            effectGo.layer = LayerMask.NameToLayer("UI");
            foreach (Transform child in effectGo.transform)
            {
                child.gameObject.layer = LayerMask.NameToLayer("UI");
            }

            // 4. 파티클 시스템을 찾아 작동시킵니다.
            foreach (var particle in effectGo.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Play(true);
            }

            // 5. 연출이 끝나면 메모리를 위해 1초 뒤에 깔끔하게 파괴합니다.
            Destroy(effectGo, 1.0f);
        }

        // 시간이 2초 이하일 때 포스트 프로세싱의 비네트(화면 붉어짐) 맥박을 실시간 업데이트하는 함수
        protected void UpdateDangerPulse()
        {
            if (isGameActive && PHSGlitchEffectManager.Instance != null)
            {
                PHSGlitchEffectManager.Instance.UpdateVignettePulse(timeRemaining);
            }
        }

        // 만능 성공 연출 버튼
        protected void TriggerSuccess()
        {
            if (!isGameActive) return;
            isGameActive = false; // 중복 터치 및 연출 방지

            // 성공하는 순간 화면 테두리에 남아있던 빨간 깜빡임 효과를 깨끗하게 꺼줍니다.
            if (PHSGlitchEffectManager.Instance != null)
                PHSGlitchEffectManager.Instance.UpdateVignettePulse(0f);

            PlaySFX(successClip, false); // 성공 팡파레 소리 재생
            Invoke(nameof(GameSucceed), 0.5f); // 0.5초 뒤 미니게임 창 완전히 닫기
        }

        // 만능 실패 연출 버튼
        protected void TriggerFailure()
        {
            if (!isGameActive) return;
            isGameActive = false; // 중복 연출 방지

            // 실패하는 순간 화면 테두리의 빨간 깜빡임 효과를 꺼줍니다.
            if (PHSGlitchEffectManager.Instance != null)
                PHSGlitchEffectManager.Instance.UpdateVignettePulse(0f);

            PlaySFX(failClip, false); // 삐빅! 하는 실패 경고음 재생
            StartCoroutine(ShakeUI(GetComponent<RectTransform>(), 15f, 0.3f)); // 전체 패널 흔들기 연출

            if (PHSGlitchEffectManager.Instance != null)
                PHSGlitchEffectManager.Instance.TriggerGlitch(0.4f); // 화면 전체 렌즈 글리치 연출

            Invoke(nameof(GameFail), 0.5f); // 0.5초 뒤 미니게임 창 완전히 닫기
        }

        // 사운드를 무작위 피치로 찰지게 재생해주는 함수
        public void PlaySFX(AudioClip clip, bool randomizePitch = true)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.pitch = randomizePitch ? Random.Range(0.9f, 1.1f) : 1f;
            sfxSource.PlayOneShot(clip);
        }

        // UI 요소를 순간적으로 늘렸다가 줄여 통통 튕기게 만드는 애니메이션 코루틴
        public void PunchUI(RectTransform targetUI, float punchScale = 1.2f, float duration = 0.15f)
        {
            if (targetUI == null) return;

            activePunchTargets.TryGetValue(targetUI, out int activeCount);
            activePunchTargets[targetUI] = activeCount + 1;
            StartCoroutine(PunchRoutine(targetUI, punchScale, duration));
        }

        private IEnumerator PunchRoutine(RectTransform targetUI, float punchScale, float duration)
        {
            Vector3 orig = Vector3.one;
            Vector3 target = Vector3.one * punchScale;
            float half = duration / 2f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                targetUI.localScale = Vector3.Lerp(orig, target, elapsed / half);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                targetUI.localScale = Vector3.Lerp(target, orig, elapsed / half);
                yield return null;
            }
            targetUI.localScale = orig;
            ReleasePunchTarget(targetUI);
        }

        // UI 패널 자체를 사방으로 덜덜 흔드는 지진(Shake) 애니메이션 코루틴
        public IEnumerator ShakeUI(RectTransform targetUI, float amount, float duration)
        {
            if (targetUI == null) yield break;
            Vector2 originalPos = targetUI.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                targetUI.anchoredPosition = originalPos + new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount));
                yield return null;
            }
            targetUI.anchoredPosition = originalPos;
        }

        public void ForceFail() => TriggerFailure();

        public bool TryCancelGame()
        {
            if (!isGameActive) return false;

            isGameActive = false;
            CancelInvoke();
            StopAllCoroutines();
            ResetActivePunchTargets();

            if (PHSGlitchEffectManager.Instance != null)
                PHSGlitchEffectManager.Instance.UpdateVignettePulse(0f);

            currentTarget = null;
            return true;
        }

        protected void GameSucceed()
        {
            IMiniGameTarget target = currentTarget;
            currentTarget = null;
            target?.OnMiniGameSucceeded();
            PHSMiniGameManager.Instance.EndMiniGame(true);
        }

        protected void GameFail()
        {
            IMiniGameTarget target = currentTarget;
            currentTarget = null;
            target?.OnMiniGameFailed();
            PHSMiniGameManager.Instance.EndMiniGame(false);
        }

        private void ReleasePunchTarget(RectTransform targetUI)
        {
            if (!activePunchTargets.TryGetValue(targetUI, out int activeCount))
            {
                return;
            }

            if (activeCount <= 1)
            {
                activePunchTargets.Remove(targetUI);
                return;
            }

            activePunchTargets[targetUI] = activeCount - 1;
        }

        private void ResetActivePunchTargets()
        {
            foreach (RectTransform targetUI in activePunchTargets.Keys)
            {
                if (targetUI != null)
                {
                    targetUI.localScale = Vector3.one;
                }
            }

            activePunchTargets.Clear();
        }
    }
}
