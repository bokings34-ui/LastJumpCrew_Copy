using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// "다른 사람 캐릭터에도 이동/점프 애니메이션이 보이게 하는" 컴포넌트.
    ///
    /// [왜 필요한가]
    ///  기존 PHS_CuteWhiteGhostKeyboardAnimationController 는 NetworkPlayerController 의
    ///  HasMoveInput / IsRunning / IsGrounded 값을 읽어서 애니메이션을 고른다.
    ///  그런데 이 값들은 NetworkVariable 이 아니라 그냥 C# 프로퍼티라서,
    ///   - HasMoveInput / IsRunning → "자기 캐릭터를 조종하는 클라이언트"에서만 계산된다.
    ///   - IsGrounded / VerticalVelocity → "서버"에서만 계산된다.
    ///  결과적으로 내 화면에 보이는 남의 캐릭터는 이 값들이 계속 false 라서 Idle 에 멈춰 있다.
    ///
    /// [이 스크립트의 해결 방법 — 값을 새로 동기화하지 않는다]
    ///  NetworkTransform 이 이미 "위치"는 모든 클라이언트에 복제해 주고 있다.
    ///  그래서 남의 캐릭터는 매 프레임 위치가 얼마나 변했는지를 보고 속도를 거꾸로 계산(역산)한다.
    ///   - 수평 속도 → Idle / Walk / Run 판정
    ///   - 수직 속도 + 발밑 검사 → Jump / Fall 판정
    ///  네트워크 패킷을 하나도 더 쓰지 않고, 팀장(박한솔) 소유 코드도 건드리지 않는다.
    ///
    /// [내 캐릭터는 그대로 정확하게]
    ///  내가 조종하는 캐릭터(오너)는 역산이 필요 없다. NetworkPlayerController 의 실제 값을
    ///  그대로 읽어서 즉각 반응하게 한다. (역산은 보간 때문에 아주 약간 늦다)
    ///
    /// [배치 방법]
    ///  1) Animator 와 같은 GameObject(= NetworkPlayerController 가 붙어 있는 루트)에 붙인다.
    ///  2) 기존 PHS_CuteWhiteGhostKeyboardAnimationController 는 인스펙터에서 체크 해제한다.
    ///     (두 개가 동시에 CrossFade 를 호출하면 서로 애니메이션을 덮어쓴다)
    ///  3) groundLayers 에 바닥으로 취급할 레이어를 지정한다. (플레이어 레이어는 빼기)
    ///
    /// ※ 무중력 구간에서는 발밑에 바닥이 없으므로 Jump/Fall 상태가 유지된다.
    ///   이는 기존 스크립트의 동작(무중력에서 IsGrounded 가 항상 false)과 동일하다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class NetworkRemotePlayerAnimator : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("같은 오브젝트의 Animator. 비워두면 자동으로 찾는다.")]
        [SerializeField] private Animator animator;

        [Tooltip("소유권 판정에 쓰는 NetworkObject. 비워두면 부모에서 자동으로 찾는다.")]
        [SerializeField] private NetworkObject networkObject;

        [Tooltip("내 캐릭터일 때 정확한 값을 읽어올 대상. 비워두면 자동으로 찾는다. 없어도 역산으로 동작한다.")]
        [SerializeField] private NetworkPlayerController playerController;

        [Tooltip("발밑 검사용 캡슐 크기를 얻는다. 비워두면 자동으로 찾고, 없으면 아래 기본 반지름을 쓴다.")]
        [SerializeField] private CharacterController characterController;

        [Header("애니메이터 상태 이름")]
        [Tooltip("Animator Controller 안의 상태 이름과 정확히 같아야 한다. 오타가 나면 조용히 아무 것도 재생되지 않는다.")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string runStateName = "Run";
        [SerializeField] private string jumpStateName = "Jump";
        [SerializeField] private string fallStateName = "Fall";

        [Tooltip("상태를 바꿀 때 섞이는 시간(초). 기존 스크립트와 같은 0.12 를 기본값으로 둔다.")]
        [SerializeField, Min(0f)] private float crossFadeTime = 0.12f;

        [Header("속도 판정 기준 (원격 캐릭터용)")]
        [Tooltip("이 수평 속도(m/s)를 넘어야 '움직이는 중'으로 본다. 너무 낮으면 제자리에서 흔들릴 때도 걷기가 나온다.")]
        [SerializeField, Min(0.01f)] private float walkSpeedThreshold = 0.35f;

        [Tooltip("이 수평 속도(m/s)를 넘으면 달리기로 본다. NetworkPlayerController 의 moveSpeed(2.4)와 runSpeed(4.2) 사이 값을 쓴다.")]
        [SerializeField, Min(0.02f)] private float runSpeedThreshold = 3.3f;

        [Tooltip("공중에서 이 수직 속도(m/s) 이상으로 올라가면 Jump, 아니면 Fall 로 본다.")]
        [SerializeField] private float riseSpeedThreshold = 0.05f;

        [Tooltip("속도 부드럽게 하기. 값이 클수록 즉각 반응하고, 작을수록 덜 떨린다.")]
        [SerializeField, Min(0.1f)] private float speedSmoothing = 12f;

        [Tooltip("이 속도(m/s)를 넘는 위치 변화는 '순간이동(워프/리스폰)'으로 보고 측정을 초기화한다.")]
        [SerializeField, Min(1f)] private float teleportSpeedThreshold = 25f;

        [Header("발밑(접지) 검사")]
        [Tooltip("바닥으로 인정할 레이어. 플레이어 레이어는 반드시 빼야 자기 몸을 바닥으로 착각하지 않는다.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("발밑으로 얼마나 내려다볼지(m). 계단·경사에서 애니메이션이 튀면 조금 늘린다.")]
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.25f;

        [Tooltip("CharacterController 가 없을 때 쓸 발밑 검사 구체 반지름(m).")]
        [SerializeField, Min(0.01f)] private float fallbackProbeRadius = 0.3f;

        // ── 내부 상태 ──────────────────────────────────────────────────────
        private int idleHash;
        private int walkHash;
        private int runHash;
        private int jumpHash;
        private int fallHash;
        private int currentStateHash;

        // 위치 역산용. 이전 프레임 위치를 기억했다가 차이를 낸다.
        private Vector3 previousPosition;
        private bool hasPositionBaseline;
        private float smoothedHorizontalSpeed;
        private float smoothedVerticalSpeed;

        // 발밑 검사 결과를 담을 배열. 매 프레임 new 를 피하려고 미리 만들어 둔다.
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            if (networkObject == null)
            {
                networkObject = GetComponentInParent<NetworkObject>();
            }
            if (playerController == null)
            {
                playerController = GetComponent<NetworkPlayerController>();
            }
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            idleHash = Animator.StringToHash(idleStateName);
            walkHash = Animator.StringToHash(walkStateName);
            runHash = Animator.StringToHash(runStateName);
            jumpHash = Animator.StringToHash(jumpStateName);
            fallHash = Animator.StringToHash(fallStateName);

            if (animator == null)
            {
                Debug.LogError($"SBG_REMOTE_ANIM_FAILED reason=animator_missing obj={name}", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            // 껐다 켜지는 동안 위치가 바뀌었을 수 있으니 측정을 처음부터 다시 한다.
            ResetMeasurement();
        }

        private void Update()
        {
            MeasureSpeedFromPosition();

            // 내가 조종하는 캐릭터(또는 네트워크가 아직 안 붙은 단독 테스트)면
            // 역산 대신 컨트롤러의 진짜 값을 쓴다 → 반응이 즉각적이다.
            if (CanTrustControllerValues())
            {
                PlayFromController();
            }
            else
            {
                PlayFromEstimation();
            }
        }

        /// <summary>
        /// NetworkPlayerController 의 값을 그대로 믿어도 되는 상황인지 판단한다.
        /// 오너(내 캐릭터)이거나, 아직 네트워크 스폰 전(에디터 단독 테스트)일 때만 true.
        /// </summary>
        private bool CanTrustControllerValues()
        {
            if (playerController == null)
            {
                return false;
            }
            if (networkObject == null || !networkObject.IsSpawned)
            {
                return true; // 단독 실행/테스트 → 로컬 값이 곧 정답.
            }
            return networkObject.IsOwner;
        }

        // ── 내 캐릭터: 기존 스크립트와 동일한 판정 ──────────────────────────
        private void PlayFromController()
        {
            if (!playerController.IsGrounded)
            {
                Play(playerController.VerticalVelocity > riseSpeedThreshold ? jumpHash : fallHash);
                return;
            }

            if (!playerController.HasMoveInput)
            {
                Play(idleHash);
                return;
            }

            Play(playerController.IsRunning ? runHash : walkHash);
        }

        // ── 남의 캐릭터: 위치 변화로 역산한 값으로 판정 ─────────────────────
        private void PlayFromEstimation()
        {
            if (!IsGroundedByProbe())
            {
                Play(smoothedVerticalSpeed > riseSpeedThreshold ? jumpHash : fallHash);
                return;
            }

            if (smoothedHorizontalSpeed < walkSpeedThreshold)
            {
                Play(idleHash);
                return;
            }

            Play(smoothedHorizontalSpeed >= runSpeedThreshold ? runHash : walkHash);
        }

        /// <summary>
        /// 이번 프레임에 위치가 얼마나 변했는지로 수평/수직 속도를 계산한다.
        /// NetworkTransform 이 보간(Interpolate)하며 위치를 채워주므로, 남의 캐릭터도 값이 나온다.
        /// </summary>
        private void MeasureSpeedFromPosition()
        {
            var currentPosition = transform.position;
            var deltaTime = Time.deltaTime;

            if (!hasPositionBaseline || deltaTime <= 0f)
            {
                previousPosition = currentPosition;
                hasPositionBaseline = true;
                return;
            }

            var delta = currentPosition - previousPosition;
            previousPosition = currentPosition;

            var horizontal = new Vector3(delta.x, 0f, delta.z);
            var rawHorizontalSpeed = horizontal.magnitude / deltaTime;
            var rawVerticalSpeed = delta.y / deltaTime;

            // 워프/리스폰처럼 한 프레임에 순간이동한 경우는 속도로 치면 안 된다.
            if (rawHorizontalSpeed > teleportSpeedThreshold
                || Mathf.Abs(rawVerticalSpeed) > teleportSpeedThreshold)
            {
                ResetMeasurement();
                previousPosition = currentPosition;
                hasPositionBaseline = true;
                return;
            }

            // 지수 평활(exponential smoothing). 프레임률이 달라도 결과가 비슷하도록 Exp 를 쓴다.
            var blend = 1f - Mathf.Exp(-speedSmoothing * deltaTime);
            smoothedHorizontalSpeed = Mathf.Lerp(smoothedHorizontalSpeed, rawHorizontalSpeed, blend);
            smoothedVerticalSpeed = Mathf.Lerp(smoothedVerticalSpeed, rawVerticalSpeed, blend);
        }

        /// <summary>
        /// 발밑을 향해 구(sphere)를 쏴서 바닥이 있는지 본다.
        /// 원격 캐릭터는 CharacterController.Move 가 돌지 않아 isGrounded 를 믿을 수 없어서 직접 검사한다.
        /// </summary>
        private bool IsGroundedByProbe()
        {
            var radius = characterController != null
                ? Mathf.Max(0.01f, characterController.radius * 0.9f)
                : fallbackProbeRadius;

            // 캡슐 아래쪽 구의 중심에서 시작한다. CharacterController 가 없으면 발 위치 기준.
            var origin = characterController != null
                ? transform.TransformPoint(characterController.center)
                  + Vector3.down * Mathf.Max(0f, characterController.height * 0.5f - characterController.radius)
                : transform.position + Vector3.up * radius;

            var hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                groundHits,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                var hitCollider = groundHits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }
                // 자기 자신(또는 자식으로 달린 콜라이더)은 바닥이 아니다.
                if (hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }
                return true;
            }

            return false;
        }

        private void ResetMeasurement()
        {
            hasPositionBaseline = false;
            smoothedHorizontalSpeed = 0f;
            smoothedVerticalSpeed = 0f;
        }

        /// <summary>같은 상태를 계속 다시 재생하지 않도록 막고, 바뀔 때만 CrossFade 한다.</summary>
        private void Play(int stateHash)
        {
            if (currentStateHash == stateHash)
            {
                return;
            }

            currentStateHash = stateHash;
            animator.CrossFade(stateHash, crossFadeTime);
        }
    }
}
