using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 캐릭터의 "이동 방향(4/8방향)"까지 반영해서 애니메이션을 재생하는 컴포넌트.
    /// 2D Blend Tree 방식이라, 스크립트는 숫자만 넘기고 실제 클립 선택은 Animator 가 한다.
    ///
    /// ─────────────────────────────────────────────────────────────────────
    /// [왜 방향이 필요한가]
    ///  NetworkPlayerController.RotatePlayer() 는 "마우스"로만 캐릭터를 돌리고,
    ///  이동은 transform.right * move.x + transform.forward * move.y 로 계산한다.
    ///  즉 캐릭터는 마우스 보는 쪽을 향한 채 옆·뒤로도 걷는다(스트레이핑).
    ///  앞으로 걷기 클립 하나만 쓰면 뒤로 갈 때 발이 거꾸로 미끄러져 보인다.
    ///
    /// [네트워크에서 어떻게 동작하는가 — 추가 패킷 0]
    ///  플레이어 프리팹에 NetworkAnimator 는 없다. 애니메이션은 각자 자기 화면에서 재생한다.
    ///  대신 NetworkTransform 이 SyncPositionX + SyncRotAngleY + Interpolate 로
    ///  "위치"와 "바라보는 방향"을 모든 클라이언트에 복제해 준다.
    ///  → 남의 캐릭터도 위치 변화를 캐릭터 로컬 기준으로 되돌리면(InverseTransformDirection)
    ///    전/후/좌/우 성분이 그대로 나온다. NetworkVariable 을 새로 만들 필요가 없다.
    ///
    /// [내 캐릭터는 지연 없이]
    ///  내가 조종하는 캐릭터는 IPlayerControlInput.Move 를 그대로 쓴다.
    ///  이 값은 이미 캐릭터 로컬 기준이라 변환이 필요 없고, 네트워크 지연도 없다.
    ///
    /// [무중력 구분]
    ///  NetworkPlayerController.GravityMode 로 구분한다.
    ///  이 값은 NetworkPlayerGravityArea(평범한 MonoBehaviour)의 OnTriggerEnter/Exit 로 갱신되는데,
    ///  IsServer 가드가 없어서 모든 클라이언트에서 각자 실행된다.
    ///  → 원격 캐릭터에서도 값이 맞다. 함선 중력 on/off 도 ShipGravityZoneController 가
    ///    복제된 함선 상태를 받아 모든 클라이언트에 적용한다.
    ///
    /// ─────────────────────────────────────────────────────────────────────
    /// [배치 방법]
    ///  1) Animator 가 있는 GameObject(= NetworkPlayerController 가 붙은 루트)에 붙인다.
    ///  2) 같은 오브젝트의 다른 애니메이션 컨트롤 컴포넌트는 반드시 체크 해제한다.
    ///     (PHS_CuteWhiteGhostKeyboardAnimationController / NetworkRemotePlayerAnimator)
    ///     두 개가 동시에 CrossFade 를 호출하면 서로 덮어써서 애니메이션이 깜빡인다.
    ///  3) groundLayers 에서 플레이어 레이어는 빼둔다. (자기 몸을 바닥으로 착각 방지)
    ///  4) Animator Controller 에 아래를 만들어 둔다.
    ///     · 파라미터: MoveX(Float) / MoveY(Float) / Speed(Float) / IsZeroGravity(Bool)
    ///     · 상태: Locomotion(2D 블렌드 트리) / ZeroGravity
    ///            JumpStart(Jump.start 원샷) / JumpAir(Jump.fall 루프) / JumpLand(Jump.land 원샷)
    ///  ※ 이름이 하나라도 다르면 Awake 에서 에러 로그를 찍고 그 항목만 조용히 건너뛴다.
    ///
    /// [점프는 3단계로 이어진다]
    ///  지상 ─(발이 떨어짐)→ JumpStart ─(시간/하강)→ JumpAir ─(발이 닿음)→ JumpLand ─(시간)→ 지상
    ///  "지금 조건"만 보고 고르면 이런 순서를 만들 수 없어서, 어느 단계인지 기억해 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class NetworkPlayerLocomotionAnimator : MonoBehaviour
    {
        [Header("참조 (비워두면 자동으로 찾는다)")]
        [SerializeField] private Animator animator;
        [SerializeField] private NetworkObject networkObject;
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private CharacterController characterController;

        [Tooltip("IPlayerControlInput 을 구현한 컴포넌트(PlayerControlInput). 내 캐릭터의 입력을 지연 없이 읽는 데 쓴다.")]
        [SerializeField] private MonoBehaviour controlInputBehaviour;

        [Header("Animator 파라미터 이름")]
        [Tooltip("좌(-1) ~ 우(+1). 비워두면 사용하지 않는다.")]
        [SerializeField] private string moveXParameter = "MoveX";

        [Tooltip("뒤(-1) ~ 앞(+1). 비워두면 사용하지 않는다.")]
        [SerializeField] private string moveYParameter = "MoveY";

        [Tooltip("0 ~ 1 로 정규화된 속도. 걷기/달리기 블렌드나 재생속도 배율에 쓴다.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("무중력 여부. 비워두면 사용하지 않는다.")]
        [SerializeField] private string isZeroGravityParameter = "IsZeroGravity";

        [Tooltip("무중력에서 위(+1)/아래(-1) 이동. 지금은 선택 사항이라 비워둬도 된다.")]
        [SerializeField] private string moveUpParameter = "";

        [Header("Animator 상태 이름")]
        [Tooltip("2D 블렌드 트리가 들어 있는 지상 이동 상태.")]
        [SerializeField] private string locomotionStateName = "Locomotion";

        [Tooltip("도약하는 순간. 원샷 클립(Jump.start).")]
        [SerializeField] private string jumpStartStateName = "JumpStart";

        [Tooltip("공중에 떠 있는 동안. 반복 클립(Jump.fall, Loop Time 켜기).")]
        [SerializeField] private string jumpAirStateName = "JumpAir";

        [Tooltip("착지하는 순간. 원샷 클립(Jump.land).")]
        [SerializeField] private string jumpLandStateName = "JumpLand";

        [Tooltip("무중력 전용 상태. 비워두면 무중력에서도 지상 상태를 그대로 쓴다.")]
        [SerializeField] private string zeroGravityStateName = "ZeroGravity";

        [SerializeField, Min(0f)] private float crossFadeTime = 0.12f;

        [Header("점프 3단계 타이밍")]
        [Tooltip("JumpStart 를 유지할 시간(초). 이 시간이 지나거나 하강으로 바뀌면 JumpAir 로 넘어간다.")]
        [SerializeField, Min(0f)] private float jumpStartDuration = 0.25f;

        [Tooltip("JumpLand 를 유지할 시간(초). 길면 착지 후 조작이 굼떠 보인다.")]
        [SerializeField, Min(0f)] private float jumpLandDuration = 0.2f;

        [Tooltip("이 시간(초) 이상 공중에 있었을 때만 착지 모션을 재생한다. 계단·경사에서 접지가 한 프레임 끊길 때 착지가 튀는 것을 막는다.")]
        [SerializeField, Min(0f)] private float minAirTimeForLanding = 0.12f;

        [Tooltip("착지 모션 중에 이동 입력이 들어오면 기다리지 않고 바로 지상 이동으로 넘어간다.")]
        [SerializeField] private bool cancelLandingOnMove = true;

        [Header("속도 기준")]
        [Tooltip("정규화(0~1)의 기준이 되는 최고 속도. NetworkPlayerController 의 달리기 속도(기본 4.2)와 맞춘다.")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 4.2f;

        [Tooltip("이 속도(m/s) 미만이면 '정지'로 본다. 너무 낮으면 제자리에서 다리가 떨린다.")]
        [SerializeField, Min(0f)] private float deadZoneSpeed = 0.35f;

        [Tooltip("클수록 즉각 반응, 작을수록 부드럽다. 원격 캐릭터가 떨리면 값을 낮춘다.")]
        [SerializeField, Min(0.1f)] private float directionSmoothing = 12f;

        [Tooltip("발이 떨어질 때 이 수직 속도(m/s)를 넘겨 올라가면 '점프', 아니면 '걸어서 떨어짐'으로 본다.")]
        [SerializeField] private float riseSpeedThreshold = 0.05f;

        [Tooltip("이 속도(m/s)를 넘는 위치 변화는 순간이동(워프/리스폰)으로 보고 측정을 초기화한다.")]
        [SerializeField, Min(1f)] private float teleportSpeedThreshold = 25f;

        [Header("발밑(접지) 검사")]
        [Tooltip("바닥으로 인정할 레이어. 플레이어 레이어는 반드시 뺀다.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.25f;
        [SerializeField, Min(0.01f)] private float fallbackProbeRadius = 0.3f;

        // ── 내부 상태 ──────────────────────────────────────────────────────
        private readonly LocomotionDirectionSampler sampler = new();
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private IPlayerControlInput controlInput;

        private int moveXHash;
        private int moveYHash;
        private int speedHash;
        private int isZeroGravityHash;
        private int moveUpHash;
        private bool hasMoveX;
        private bool hasMoveY;
        private bool hasSpeed;
        private bool hasIsZeroGravity;
        private bool hasMoveUp;

        private int locomotionHash;
        private int jumpStartHash;
        private int jumpAirHash;
        private int jumpLandHash;
        private int zeroGravityHash;
        private bool hasLocomotionState;
        private bool hasJumpStartState;
        private bool hasJumpAirState;
        private bool hasJumpLandState;
        private bool hasZeroGravityState;

        /// <summary>점프 3단계를 "지금 어느 칸에 있는지" 기억하기 위한 표시.</summary>
        private enum AirPhase
        {
            Grounded,   // 땅 위 (지상 이동)
            JumpStart,  // 도약하는 순간
            JumpAir,    // 공중에 떠 있는 동안
            JumpLand    // 착지하는 순간
        }

        private AirPhase airPhase = AirPhase.Grounded;
        private float phaseTimer;   // 지금 단계에 머문 시간
        private float airTime;      // 이번에 공중에 떠 있던 총 시간

        private int currentStateHash;
        private Vector3 previousPosition;
        private bool hasPositionBaseline;
        private float smoothedVerticalSpeed;
        private float smoothedUpSpeed;

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

            // 인스펙터에서 넣어준 컴포넌트가 실제로 IPlayerControlInput 인지 확인한다.
            controlInput = controlInputBehaviour as IPlayerControlInput;
            if (controlInput == null)
            {
                // 안 넣어줬으면 자식까지 뒤져서 찾는다. 없어도 위치 역산으로 동작하므로 에러는 아니다.
                controlInput = GetComponentInChildren<IPlayerControlInput>(true);
            }

            if (animator == null)
            {
                Debug.LogError($"SBG_LOCOMOTION_ANIM_FAILED reason=animator_missing obj={name}", this);
                enabled = false;
                return;
            }

            CacheParameters();
            CacheStates();
        }

        private void OnEnable()
        {
            ResetMeasurement();
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            // 1) 위치 변화를 캐릭터 로컬 기준으로 되돌린다.
            //    localVelocity.x = 좌우, localVelocity.y = 상하, localVelocity.z = 앞뒤
            if (!TryMeasureLocalVelocity(deltaTime, out var localVelocity))
            {
                return; // 순간이동 등으로 이번 프레임은 건너뛴다.
            }

            var horizontalSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;

            // 2) 방향을 정한다. 내 캐릭터는 입력값(지연 0), 남의 캐릭터는 역산값.
            var localDirection = TryGetOwnerInputDirection(out var inputDirection)
                ? inputDirection
                : new Vector2(localVelocity.x, localVelocity.z);

            sampler.Update(
                localDirection,
                horizontalSpeed,
                maxSpeed,
                deadZoneSpeed,
                directionSmoothing,
                deltaTime);

            // 3) 애니메이터에 숫자를 넘긴다. 실제 클립 선택은 블렌드 트리가 한다.
            if (hasMoveX)
            {
                animator.SetFloat(moveXHash, sampler.MoveX);
            }
            if (hasMoveY)
            {
                animator.SetFloat(moveYHash, sampler.MoveY);
            }
            if (hasSpeed)
            {
                animator.SetFloat(speedHash, sampler.NormalizedSpeed);
            }
            if (hasMoveUp)
            {
                animator.SetFloat(moveUpHash, Mathf.Clamp(smoothedUpSpeed / Mathf.Max(0.1f, maxSpeed), -1f, 1f));
            }

            // 4) 상태를 고른다. 판정 순서가 중요하다.
            var isZeroGravity = IsZeroGravity();
            if (hasIsZeroGravity)
            {
                animator.SetBool(isZeroGravityHash, isZeroGravity);
            }

            // 무중력이 가장 먼저다. 무중력에는 발밑에 바닥이 없어서
            // 접지 검사를 먼저 하면 영원히 낙하 상태가 재생된다.
            if (isZeroGravity)
            {
                ResetAirPhase();
                if (hasZeroGravityState)
                {
                    Play(zeroGravityHash);
                    return;
                }
                Play(locomotionHash);
                return;
            }

            // 5) 점프 3단계를 진행시킨다.
            UpdateAirPhase(IsGroundedByProbe(), deltaTime);
            PlayCurrentPhase();
        }

        // ── 점프 3단계 상태 기계 ───────────────────────────────────────────
        //
        // 지상 ─(발이 떨어짐)→ JumpStart ─(시간/하강)→ JumpAir ─(발이 닿음)→ JumpLand ─(시간)→ 지상
        //
        // "지금 조건"만 보는 방식으로는 순서를 만들 수 없어서, 어느 칸에 있는지 기억해 둔다.

        /// <summary>이번 프레임의 접지 여부를 보고 단계를 진행시킨다.</summary>
        private void UpdateAirPhase(bool grounded, float deltaTime)
        {
            switch (airPhase)
            {
                case AirPhase.Grounded:
                    if (grounded)
                    {
                        break;
                    }
                    // 발이 떨어졌다. 위로 솟았으면 점프, 아니면 발판에서 걸어 내려간 것.
                    airTime = 0f;
                    phaseTimer = 0f;
                    airPhase = smoothedVerticalSpeed > riseSpeedThreshold
                        ? AirPhase.JumpStart
                        : AirPhase.JumpAir;
                    break;

                case AirPhase.JumpStart:
                    phaseTimer += deltaTime;
                    airTime += deltaTime;
                    if (grounded)
                    {
                        EnterLandingOrGround();
                        break;
                    }
                    // 도약 모션이 끝났거나 이미 떨어지기 시작했으면 공중 단계로.
                    if (phaseTimer >= jumpStartDuration || smoothedVerticalSpeed <= riseSpeedThreshold)
                    {
                        phaseTimer = 0f;
                        airPhase = AirPhase.JumpAir;
                    }
                    break;

                case AirPhase.JumpAir:
                    phaseTimer += deltaTime;
                    airTime += deltaTime;
                    if (grounded)
                    {
                        EnterLandingOrGround();
                    }
                    break;

                case AirPhase.JumpLand:
                    phaseTimer += deltaTime;
                    if (!grounded)
                    {
                        // 착지하자마자 다시 떠버린 경우(경사에서 튕김 등).
                        airTime = 0f;
                        phaseTimer = 0f;
                        airPhase = AirPhase.JumpAir;
                        break;
                    }
                    // 착지 모션이 끝났거나, 플레이어가 이미 움직이기 시작했으면 바로 풀어준다.
                    if (phaseTimer >= jumpLandDuration || (cancelLandingOnMove && sampler.IsMoving))
                    {
                        phaseTimer = 0f;
                        airPhase = AirPhase.Grounded;
                    }
                    break;
            }
        }

        /// <summary>
        /// 발이 닿았을 때 착지 모션을 재생할지 결정한다.
        /// 계단·경사에서 접지가 한 프레임 끊기는 것까지 착지로 치면 걸을 때마다 모션이 튄다.
        /// 그래서 최소 공중 시간을 넘긴 경우에만 착지로 인정한다.
        /// </summary>
        private void EnterLandingOrGround()
        {
            var deservesLanding = airTime >= minAirTimeForLanding && hasJumpLandState;
            airTime = 0f;
            phaseTimer = 0f;
            airPhase = deservesLanding ? AirPhase.JumpLand : AirPhase.Grounded;
        }

        /// <summary>현재 단계에 맞는 Animator 상태를 재생한다.</summary>
        private void PlayCurrentPhase()
        {
            switch (airPhase)
            {
                case AirPhase.JumpStart:
                    Play(jumpStartHash, allowFallbackToLocomotion: true);
                    break;
                case AirPhase.JumpAir:
                    Play(jumpAirHash, allowFallbackToLocomotion: true);
                    break;
                case AirPhase.JumpLand:
                    Play(jumpLandHash, allowFallbackToLocomotion: true);
                    break;
                default:
                    Play(locomotionHash);
                    break;
            }
        }

        private void ResetAirPhase()
        {
            airPhase = AirPhase.Grounded;
            phaseTimer = 0f;
            airTime = 0f;
        }

        /// <summary>
        /// 내가 조종하는 캐릭터일 때만 입력값을 방향으로 쓴다.
        /// IPlayerControlInput.Move 는 이미 캐릭터 로컬 기준(x=좌우, y=앞뒤)이라 변환이 필요 없다.
        /// </summary>
        private bool TryGetOwnerInputDirection(out Vector2 direction)
        {
            direction = Vector2.zero;
            if (controlInput == null)
            {
                return false;
            }

            // 네트워크에 스폰되지 않았다면 에디터 단독 테스트 상황 → 로컬 입력이 곧 정답.
            var isLocal = networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
            if (!isLocal)
            {
                return false;
            }

            var move = controlInput.Move;
            if (move.sqrMagnitude <= 0.000001f)
            {
                return false; // 입력이 없으면 역산값에 맡긴다(외력에 밀리는 중일 수 있다).
            }

            direction = move;
            return true;
        }

        /// <summary>
        /// 이번 프레임의 위치 변화를 캐릭터 로컬 좌표계 속도로 바꾼다.
        /// NetworkTransform 이 위치와 Y회전을 모두 복제해 주므로 남의 캐릭터에서도 값이 나온다.
        /// </summary>
        private bool TryMeasureLocalVelocity(float deltaTime, out Vector3 localVelocity)
        {
            localVelocity = Vector3.zero;

            var currentPosition = transform.position;
            if (!hasPositionBaseline)
            {
                previousPosition = currentPosition;
                hasPositionBaseline = true;
                return false;
            }

            var worldVelocity = (currentPosition - previousPosition) / deltaTime;
            previousPosition = currentPosition;

            // 워프·리스폰처럼 한 프레임에 순간이동한 경우는 속도로 치면 안 된다.
            if (worldVelocity.magnitude > teleportSpeedThreshold)
            {
                ResetMeasurement();
                previousPosition = currentPosition;
                hasPositionBaseline = true;
                return false;
            }

            // 월드 → 캐릭터 로컬. 캐릭터가 마우스로 돌아가 있어도 항상 "앞/뒤/좌/우"가 나온다.
            localVelocity = transform.InverseTransformDirection(worldVelocity);

            var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionSmoothing) * deltaTime);
            smoothedVerticalSpeed = Mathf.Lerp(smoothedVerticalSpeed, worldVelocity.y, blend);
            smoothedUpSpeed = Mathf.Lerp(smoothedUpSpeed, localVelocity.y, blend);
            return true;
        }

        /// <summary>
        /// 일반 중력 상태인지 무중력(함내 무중력 / 선외 유영)인지 판정한다.
        /// GravityMode 는 트리거로 갱신되어 모든 클라이언트에서 같은 값이 된다.
        /// </summary>
        private bool IsZeroGravity()
        {
            if (playerController == null)
            {
                return false;
            }

            return playerController.GravityMode != NetworkPlayerGravityMode.ShipGravity;
        }

        /// <summary>
        /// 발밑으로 구를 쏴서 바닥이 있는지 본다.
        /// 원격 캐릭터는 CharacterController.Move 가 돌지 않아 isGrounded 를 믿을 수 없고,
        /// NetworkPlayerController.IsGrounded 는 서버에서만 계산되므로 직접 검사한다.
        /// </summary>
        private bool IsGroundedByProbe()
        {
            var radius = characterController != null
                ? Mathf.Max(0.01f, characterController.radius * 0.9f)
                : fallbackProbeRadius;

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
                if (hitCollider.transform.IsChildOf(transform))
                {
                    continue; // 자기 자신은 바닥이 아니다.
                }
                return true;
            }

            return false;
        }

        private void ResetMeasurement()
        {
            hasPositionBaseline = false;
            smoothedVerticalSpeed = 0f;
            smoothedUpSpeed = 0f;
            sampler.Reset();
            ResetAirPhase();
        }

        /// <summary>
        /// 같은 상태를 계속 다시 재생하지 않도록 막고, 바뀔 때만 CrossFade 한다.
        /// Animator 에 없는 상태로 CrossFade 하면 에러가 쏟아지므로 존재 여부를 먼저 확인한다.
        /// </summary>
        private void Play(int stateHash, bool allowFallbackToLocomotion = false)
        {
            if (!HasState(stateHash))
            {
                // Jump/Fall 상태를 아직 안 만들었다면 지상 이동 상태로 대신 보낸다.
                if (!allowFallbackToLocomotion || !hasLocomotionState)
                {
                    return;
                }
                stateHash = locomotionHash;
            }

            if (currentStateHash == stateHash)
            {
                return;
            }

            currentStateHash = stateHash;
            animator.CrossFade(stateHash, crossFadeTime);
        }

        private bool HasState(int stateHash)
        {
            if (stateHash == jumpStartHash)
            {
                return hasJumpStartState;
            }
            if (stateHash == jumpAirHash)
            {
                return hasJumpAirState;
            }
            if (stateHash == jumpLandHash)
            {
                return hasJumpLandState;
            }
            if (stateHash == zeroGravityHash)
            {
                return hasZeroGravityState;
            }
            return hasLocomotionState;
        }

        // ── 이름 검증 ─────────────────────────────────────────────────────
        // Animator 에 없는 이름을 매 프레임 호출하면 에디터가 경고를 쏟아낸다.
        // 그래서 시작할 때 한 번만 확인하고, 없는 항목은 조용히 건너뛴다.

        private void CacheParameters()
        {
            hasMoveX = TryCacheParameter(moveXParameter, AnimatorControllerParameterType.Float, out moveXHash);
            hasMoveY = TryCacheParameter(moveYParameter, AnimatorControllerParameterType.Float, out moveYHash);
            hasSpeed = TryCacheParameter(speedParameter, AnimatorControllerParameterType.Float, out speedHash);
            hasMoveUp = TryCacheParameter(moveUpParameter, AnimatorControllerParameterType.Float, out moveUpHash);
            hasIsZeroGravity = TryCacheParameter(isZeroGravityParameter, AnimatorControllerParameterType.Bool, out isZeroGravityHash);
        }

        private bool TryCacheParameter(string parameterName, AnimatorControllerParameterType type, out int hash)
        {
            hash = 0;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false; // 비워두면 "안 쓰겠다"는 뜻이므로 경고하지 않는다.
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name != parameterName)
                {
                    continue;
                }
                if (parameter.type != type)
                {
                    Debug.LogError(
                        $"SBG_LOCOMOTION_ANIM_PARAM_TYPE param={parameterName} expected={type} actual={parameter.type} obj={name}",
                        this);
                    return false;
                }

                hash = parameter.nameHash;
                return true;
            }

            Debug.LogError($"SBG_LOCOMOTION_ANIM_PARAM_MISSING param={parameterName} obj={name}", this);
            return false;
        }

        private void CacheStates()
        {
            locomotionHash = Animator.StringToHash(locomotionStateName);
            jumpStartHash = Animator.StringToHash(jumpStartStateName);
            jumpAirHash = Animator.StringToHash(jumpAirStateName);
            jumpLandHash = Animator.StringToHash(jumpLandStateName);
            zeroGravityHash = Animator.StringToHash(zeroGravityStateName);

            hasLocomotionState = TryValidateState(locomotionStateName, locomotionHash, required: true);
            hasJumpStartState = TryValidateState(jumpStartStateName, jumpStartHash, required: false);
            hasJumpAirState = TryValidateState(jumpAirStateName, jumpAirHash, required: false);
            hasJumpLandState = TryValidateState(jumpLandStateName, jumpLandHash, required: false);
            hasZeroGravityState = TryValidateState(zeroGravityStateName, zeroGravityHash, required: false);
        }

        private bool TryValidateState(string stateName, int stateHash, bool required)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (animator.HasState(0, stateHash))
            {
                return true;
            }

            if (required)
            {
                Debug.LogError($"SBG_LOCOMOTION_ANIM_STATE_MISSING state={stateName} obj={name}", this);
            }
            else
            {
                Debug.LogWarning($"SBG_LOCOMOTION_ANIM_STATE_SKIPPED state={stateName} obj={name}", this);
            }

            return false;
        }
    }
}
