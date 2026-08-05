using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// "캐릭터가 지금 어느 쪽으로 얼마나 빠르게 가고 있는가"를 애니메이터가 쓰기 좋은 숫자로 바꿔주는 계산기.
    ///
    /// [이 클래스가 하지 않는 일]
    ///  Animator 를 모른다. 네트워크도 모른다. Unity 컴포넌트조차 아니다(MonoBehaviour 아님).
    ///  오로지 "들어온 이동 벡터 → 부드럽게 다듬은 MoveX/MoveY/Speed" 변환만 한다.
    ///  이렇게 역할을 하나로 좁혀 두면 나중에 적(NPC)이나 다른 캐릭터에도 그대로 재사용할 수 있다.
    ///
    /// [좌표 기준 — 아주 중요]
    ///  넣어주는 이동 벡터는 반드시 "캐릭터 로컬 기준"이어야 한다.
    ///   x = 오른쪽(+) / 왼쪽(-),  y = 앞(+) / 뒤(-)
    ///  NetworkPlayerController 가 이동을 transform.right * move.x + transform.forward * move.y 로
    ///  계산하므로, 플레이어 입력값(IPlayerControlInput.Move)은 이미 이 기준과 같다. 변환이 필요 없다.
    ///  반대로 "위치 변화(delta)"를 쓸 때는 월드 기준이므로
    ///  transform.InverseTransformDirection() 으로 로컬로 바꿔서 넣어야 한다.
    ///
    /// [왜 부드럽게(평활) 만드는가]
    ///  원격 캐릭터의 위치는 네트워크로 띄엄띄엄 들어와서 값이 한 프레임씩 튄다.
    ///  그대로 애니메이터에 넣으면 다리가 덜덜 떨린다. 그래서 지수 평활로 완만하게 만든다.
    /// </summary>
    public sealed class LocomotionDirectionSampler
    {
        /// <summary>오른쪽(+1) ~ 왼쪽(-1). 이동이 느릴수록 0에 가까워진다.</summary>
        public float MoveX { get; private set; }

        /// <summary>앞(+1) ~ 뒤(-1). 이동이 느릴수록 0에 가까워진다.</summary>
        public float MoveY { get; private set; }

        /// <summary>다듬어진 실제 수평 속도(m/s). 걷기/달리기 판정이나 재생속도 배율에 쓴다.</summary>
        public float Speed { get; private set; }

        /// <summary>0(정지) ~ 1(최고속). 블렌드 트리에 넣기 좋게 0~1로 정규화한 속도.</summary>
        public float NormalizedSpeed { get; private set; }

        /// <summary>지금 "움직이는 중"으로 판정됐는지. 데드존을 넘었는지와 같은 뜻.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>
        /// 한 프레임치 값을 갱신한다.
        /// </summary>
        /// <param name="localDirection">캐릭터 로컬 기준 이동 방향. 크기는 신경 쓰지 않고 방향만 쓴다.</param>
        /// <param name="speed">실제 수평 속도(m/s).</param>
        /// <param name="maxSpeed">정규화 기준이 되는 최고 속도(m/s). 보통 달리기 속도.</param>
        /// <param name="deadZoneSpeed">이 속도 미만이면 "안 움직임"으로 본다(m/s).</param>
        /// <param name="smoothing">클수록 즉각 반응, 작을수록 부드럽다. 보통 8~15.</param>
        /// <param name="deltaTime">이번 프레임의 경과 시간.</param>
        public void Update(
            Vector2 localDirection,
            float speed,
            float maxSpeed,
            float deadZoneSpeed,
            float smoothing,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            IsMoving = speed >= deadZoneSpeed && localDirection.sqrMagnitude > 0.000001f;

            // 정규화 속도: 0~1. maxSpeed 가 0 이하로 잘못 들어와도 나눗셈이 터지지 않게 막는다.
            var safeMaxSpeed = Mathf.Max(0.01f, maxSpeed);
            var targetNormalizedSpeed = IsMoving ? Mathf.Clamp01(speed / safeMaxSpeed) : 0f;

            // 목표 방향: 방향만 남기고(normalized) 크기는 정규화 속도로 다시 입힌다.
            // → 천천히 걸으면 원점(Idle)에 가깝고, 빠르면 바깥(Walk/Run 클립)으로 간다.
            var targetDirection = IsMoving
                ? localDirection.normalized * targetNormalizedSpeed
                : Vector2.zero;

            // 지수 평활(exponential smoothing).
            // 1 - Exp(-k * dt) 형태를 쓰면 프레임률이 달라져도 결과가 거의 같아진다.
            var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothing) * deltaTime);

            MoveX = Mathf.Lerp(MoveX, targetDirection.x, blend);
            MoveY = Mathf.Lerp(MoveY, targetDirection.y, blend);
            NormalizedSpeed = Mathf.Lerp(NormalizedSpeed, targetNormalizedSpeed, blend);
            Speed = Mathf.Lerp(Speed, IsMoving ? speed : 0f, blend);
        }

        /// <summary>
        /// 순간이동(워프/리스폰)이나 컴포넌트 재활성화처럼 "이전 값을 믿으면 안 되는" 순간에 호출한다.
        /// </summary>
        public void Reset()
        {
            MoveX = 0f;
            MoveY = 0f;
            Speed = 0f;
            NormalizedSpeed = 0f;
            IsMoving = false;
        }
    }
}
