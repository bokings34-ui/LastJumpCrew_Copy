namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 재사용 가능한 카운트다운 타이머(순수 로직, UnityEngine 비의존).
    /// 프레임마다 Tick(deltaTime) 으로 감소시키고, '방금 만료된 순간'을 한 번만 알려준다.
    /// 제한시간·쿨다운 등 어디서든 재사용 가능.
    /// (필요 시 01. MainGame 공용 폴더로 승격 — 팀장 협의 사항.)
    /// </summary>
    public sealed class CountdownTimer
    {
        /// <summary>남은 시간(초).</summary>
        public float Remaining { get; private set; }

        /// <summary>동작 중인가(만료·정지 시 false).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>남은 시간이 0 이하인가.</summary>
        public bool IsExpired => Remaining <= 0f;

        /// <summary>지정한 시간(초)으로 타이머를 시작한다.</summary>
        public void Start(float durationSeconds)
        {
            Remaining = durationSeconds < 0f ? 0f : durationSeconds;
            IsRunning = Remaining > 0f;
        }

        /// <summary>타이머를 멈춘다(남은 시간 값은 유지).</summary>
        public void Stop() => IsRunning = false;

        /// <summary>
        /// deltaTime 만큼 감소시킨다. 이번 호출에서 '방금 만료됐으면' true 를 한 번만 반환한다.
        /// 이미 멈췄거나 만료된 뒤에는 항상 false.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (!IsRunning) return false;

            Remaining -= deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                IsRunning = false;
                return true;   // 방금 만료
            }
            return false;
        }
    }
}
