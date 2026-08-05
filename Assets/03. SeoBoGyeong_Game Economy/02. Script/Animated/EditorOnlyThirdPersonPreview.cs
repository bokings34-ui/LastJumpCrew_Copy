#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;

namespace LastJumpCrew.SeoBoGyeong.animate
{
    /// <summary>
    /// [에디터 전용] 애니메이션 확인용 "1인칭 + 3인칭 동시 보기" 컴포넌트.
    ///
    /// [왜 필요한가]
    /// 이 게임의 플레이어는 1인칭이라서 자기 자신의 몸(SkinnedMeshRenderer)을 일부러 꺼버린다.
    /// (NetworkPlayerController.SetLocalView → SetLocalOwnerVisualsVisible(false))
    /// 네트워크 스폰이 안 된 상태(IsSpawned == false)에서도 같은 경로를 타기 때문에,
    /// 호스트를 시작하지 않고 에디터에서 그냥 Play 를 누르면 캐릭터 몸이 항상 사라진다.
    /// → 애니메이션을 눈으로 확인할 수가 없다.
    ///
    /// [무엇을 하는가]
    /// 1) 플레이어 1인칭 카메라를 Display 1 에, 관찰용 3인칭 카메라를 Display 2 에 배정한다.
    ///    두 카메라를 모두 켠 채로 두므로 1인칭 화면이 사라지지 않는다.
    /// 2) URP 의 카메라 렌더링 직전 콜백을 이용해, "그 카메라에서만" 몸을 켜고 끈다.
    ///      - 플레이어 카메라를 그릴 때  → 몸 OFF (머리 안쪽이 안 보임, 원래 게임과 동일)
    ///      - 그 외 카메라를 그릴 때     → 몸 ON  (3인칭·씬 뷰에서 애니메이션 확인 가능)
    ///    renderer.enabled 는 원래 전역 on/off 라 카메라별 구분이 안 되는데,
    ///    이 콜백이 카메라마다 한 번씩 불리는 점을 이용해 매번 다시 세팅하는 방식이다.
    /// 3) AudioListener 는 씬에 하나만 있어야 하므로 플레이어 것만 남기고 나머지는 끈다.
    ///
    /// [에디터에서 두 화면 보는 법]
    ///  Window ▸ General ▸ Game 으로 Game 뷰를 하나 더 연다 → 탭을 떼서 나란히 배치
    ///  → 각 창 툴바에서 Display 1 / Display 2 를 각각 선택.
    ///  (Display.Activate() 는 빌드에서만 필요하고 에디터에서는 필요 없다)
    ///
    /// [안전성]
    /// - 파일 전체가 #if UNITY_EDITOR 로 감싸져 있어 실제 빌드에는 포함되지 않는다.
    /// - 남의 담당 스크립트(NetworkPlayerController 등)를 참조하지도, 수정하지도 않는다.
    /// - 네트워크(NGO) API 를 전혀 쓰지 않으므로 패키지 병합 상태와 무관하게 컴파일된다.
    ///
    /// [사용법]
    /// 1) 테스트 씬(예: 99. Test/Scenes/FBX_actionTest.unity)의 Player 루트에 붙인다.
    /// 2) 캐릭터가 잘 보이는 위치에 Camera 를 하나 만들어 Observer Camera 칸에 넣는다.
    ///    (그 카메라의 AudioListener 는 있어도 되고 없어도 된다. 자동으로 꺼준다)
    /// 3) Player Camera 칸은 비워두면 하위에서 자동으로 찾는다.
    /// 4) Play → Display 1 은 1인칭, Display 2 는 3인칭.
    ///
    /// ※ 실제 멀티플레이 동작 검증은 이 컴포넌트로 하지 않는다.
    ///    (동기화 확인은 Tools/ParkHanSol/Scene Test/Play Current Scene As Local Host 사용)
    /// </summary>
    [DisallowMultipleComponent]
    public class EditorOnlyThirdPersonPreview : MonoBehaviour
    {
        [Header("카메라")]
        [Tooltip("플레이어 1인칭 카메라. 비워두면 하위에서 자동으로 찾는다.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("캐릭터를 바라보도록 씬에 배치한 3인칭 관찰 카메라. 반드시 지정해야 Display 2 가 나온다.")]
        [SerializeField] private Camera observerCamera;

        [Header("디스플레이 번호 (0 = Display 1, 1 = Display 2)")]
        [SerializeField, Min(0)] private int playerCameraDisplay = 0;
        [SerializeField, Min(0)] private int observerCameraDisplay = 1;

        [Header("몸 표시")]
        [Tooltip("켜두면 플레이어 카메라에서만 몸을 숨기고, 나머지 카메라에서는 보여준다.")]
        [SerializeField] private bool hideBodyInFirstPerson = true;

        [Tooltip("끄면 모든 카메라에서 몸을 항상 보여준다. 1인칭 화면에서 애니메이션을 직접 보고 싶을 때 사용.")]
        [SerializeField] private bool applyPerCameraVisibility = true;

        // 캐시. 매 프레임 GetComponentsInChildren 을 도는 건 낭비라서 Awake 에서 한 번만 모은다.
        private SkinnedMeshRenderer[] bodyRenderers;
        private AudioListener[] playerAudioListeners;

        private bool loggedOnce;

        private void Awake()
        {
            // 비활성 오브젝트까지 포함해서 모은다(true).
            bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            playerAudioListeners = GetComponentsInChildren<AudioListener>(true);

            if (playerCamera == null)
            {
                playerCamera = FindPlayerCamera();
            }

            if (bodyRenderers.Length == 0)
            {
                Debug.LogWarning(
                    $"SBG_EDITOR_PREVIEW_NO_RENDERER target={name} " +
                    "reason=하위에 SkinnedMeshRenderer 가 없다. 캐릭터 루트에 붙였는지 확인.",
                    this);
            }

            if (observerCamera == null)
            {
                Debug.LogWarning(
                    $"SBG_EDITOR_PREVIEW_NO_OBSERVER target={name} " +
                    "reason=Observer Camera 가 비어 있다. Display 2 화면이 나오지 않는다.",
                    this);
            }

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Debug.LogWarning(
                    $"SBG_EDITOR_PREVIEW_NOT_SRP target={name} " +
                    "reason=URP 가 아니라 카메라별 몸 표시가 동작하지 않는다. " +
                    "Apply Per Camera Visibility 를 꺼서 항상 보이게 쓸 것.",
                    this);
            }
        }

        private void OnEnable()
        {
            // URP 는 카메라 하나를 그리기 직전마다 이 이벤트를 부른다.
            // 여기서 렌더러를 켜고 끄면 "그 카메라에만" 적용된 것처럼 보인다.
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;

            // 컴포넌트를 끌 때 몸을 꺼둔 채로 남기면 헷갈리므로 켜둔 상태로 되돌린다.
            SetBodyVisible(true);
        }

        // LateUpdate 를 쓰는 이유:
        // NetworkPlayerController 가 Update 쪽에서 카메라를 켜고 끄기 때문에, 그보다 뒤에서 덮어써야 한다.
        private void LateUpdate()
        {
            ApplyCameraDisplays();
            ApplyAudioListeners();
            EnsureBodyObjectsActive();

            if (!loggedOnce)
            {
                loggedOnce = true;
                Debug.Log(
                    $"SBG_EDITOR_PREVIEW_ACTIVE target={name} renderers={bodyRenderers.Length} " +
                    $"player={(playerCamera != null ? playerCamera.name : "none")}(display{playerCameraDisplay + 1}) " +
                    $"observer={(observerCamera != null ? observerCamera.name : "none")}(display{observerCameraDisplay + 1})",
                    this);
            }
        }

        /// <summary>두 카메라를 각자 디스플레이에 배정하고, 둘 다 켜진 상태로 유지한다.</summary>
        private void ApplyCameraDisplays()
        {
            if (playerCamera != null)
            {
                if (playerCamera.targetDisplay != playerCameraDisplay)
                {
                    playerCamera.targetDisplay = playerCameraDisplay;
                }

                // SetLocalView 가 꺼버렸을 수 있으므로 다시 켠다.
                if (!playerCamera.enabled)
                {
                    playerCamera.enabled = true;
                }
            }

            if (observerCamera != null)
            {
                if (observerCamera.targetDisplay != observerCameraDisplay)
                {
                    observerCamera.targetDisplay = observerCameraDisplay;
                }

                if (!observerCamera.enabled)
                {
                    observerCamera.enabled = true;
                }
            }
        }

        /// <summary>AudioListener 는 씬에 하나만 유효하다. 플레이어 것만 남긴다.</summary>
        private void ApplyAudioListeners()
        {
            if (observerCamera == null)
            {
                return;
            }

            var observerListener = observerCamera.GetComponent<AudioListener>();
            if (observerListener != null && observerListener.enabled && HasEnabledPlayerListener())
            {
                observerListener.enabled = false;
            }
        }

        private bool HasEnabledPlayerListener()
        {
            for (int i = 0; i < playerAudioListeners.Length; i++)
            {
                var listener = playerAudioListeners[i];
                if (listener != null && listener.enabled && listener.isActiveAndEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 렌더러가 붙은 오브젝트 자체가 꺼져 있으면 카메라별 제어를 해도 안 보인다.
        /// 오브젝트 활성 상태만 보장하고, enabled 는 카메라 콜백에서 다룬다.
        /// </summary>
        private void EnsureBodyObjectsActive()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var bodyRenderer = bodyRenderers[i];
                if (bodyRenderer == null)
                {
                    continue;
                }

                if (!bodyRenderer.gameObject.activeSelf)
                {
                    bodyRenderer.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>URP 가 카메라 하나를 그리기 직전에 부르는 콜백.</summary>
        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (!applyPerCameraVisibility)
            {
                SetBodyVisible(true);
                return;
            }

            // 플레이어 1인칭 카메라일 때만 몸을 숨긴다. 3인칭·씬 뷰에서는 보인다.
            var isFirstPersonCamera = hideBodyInFirstPerson
                && playerCamera != null
                && renderingCamera == playerCamera;

            SetBodyVisible(!isFirstPersonCamera);
        }

        private void SetBodyVisible(bool isVisible)
        {
            if (bodyRenderers == null)
            {
                return;
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var bodyRenderer = bodyRenderers[i];
                if (bodyRenderer == null)
                {
                    continue;
                }

                if (bodyRenderer.enabled != isVisible)
                {
                    bodyRenderer.enabled = isVisible;
                }
            }
        }

        /// <summary>하위 카메라 중 관찰 카메라가 아닌 첫 번째를 플레이어 카메라로 본다.</summary>
        private Camera FindPlayerCamera()
        {
            var cameras = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i] != observerCamera)
                {
                    return cameras[i];
                }
            }

            return null;
        }
    }
}
#endif
