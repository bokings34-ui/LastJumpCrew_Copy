using UnityEngine;
using UnityEngine.AI;

namespace LastJumpCrew.SeoBoGyeong.Enemy.FleeEnemy
{
    /// <summary>
    /// 전기 생물형 적의 상태입니다.
    /// 이 몹은 플레이어를 공격하지 않고, 들키면 도망치는 것이 목적입니다.
    /// </summary>
    public enum EnemyState
    {
        Wander, // 평소 배회
        Flee    // 플레이어를 감지해 도망
    }

    /// <summary>
    /// 전기 생물형 적의 이동/상태 판단만 담당합니다.
    /// 기계 고장·포획·판매는 이 스크립트에 넣지 않고 별도 시스템으로 분리합니다.
    ///
    /// [멀티플레이 전제]
    /// 이 클래스는 스스로 Update()를 돌리지 않습니다.
    /// 외부(스폰 매니저)에서 Tick(deltaTime)을 호출해 주며,
    /// 통합 시 그 호출부에 서버 권한 게이트(IsServer)를 걸면
    /// 이 스크립트를 고치지 않아도 서버 권한 AI가 됩니다.
    /// (04 폴더 EnemyBase.Tick() 과 동일한 패턴)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class FleeEnemyAi : MonoBehaviour
    {
        [Header("이동 속도")]
        [SerializeField] private float wanderSpeed = 2f;   // 평소 어슬렁거리는 속도
        [SerializeField] private float fleeSpeed = 5f;     // 도망칠 때 속도

        [Header("감지 / 도망 거리")]
        [SerializeField] private float detectionRadius = 5f; // 이 거리 안에 플레이어가 들어오면 도망 시작
        [SerializeField] private float fleeRadius = 10f;     // 한 번에 도망칠 목표 거리
        [SerializeField] private float safeRadius = 8f;      // 이 거리 이상 벌어지면 다시 배회로 복귀

        [Header("배회 설정")]
        [SerializeField] private float wanderRadius = 6f;    // 배회 목적지를 뽑는 반경
        [SerializeField] private float arriveThreshold = 0.5f; // 목적지 도착 판정 거리

        [Header("판단 주기")]
        [SerializeField] private float decisionInterval = 0.2f; // 상태 판단 주기(초). 매 프레임 판단하지 않습니다.

        [Header("테스트 전용")]
        [Tooltip("혼자 테스트할 때만 켭니다. 팀 통합 시에는 반드시 꺼야 합니다. " +
                 "켜져 있으면 모든 클라이언트가 각자 AI를 돌려 몹 위치가 어긋납니다.")]
        [SerializeField] private bool selfTickForTest = false;

        private NavMeshAgent agent;
        private EnemyState currentState;
        private float decisionTimer;
        private bool isActive;

        /// <summary>현재 상태입니다. 애니메이션·이펙트 쪽에서 읽기용으로 사용합니다.</summary>
        public EnemyState CurrentState { get { return currentState; } }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.stoppingDistance = 0f;
            agent.autoBraking = false; // 배회 중 목적지마다 멈칫거리지 않게 합니다.
        }

        /// <summary>
        /// 스폰 시 호출합니다. 여기서부터 AI가 동작을 시작합니다.
        /// TODO(NET): 서버에서만 호출되어야 합니다.
        /// </summary>
        public void Activate()
        {
            isActive = true;
            decisionTimer = 0f;

            agent.enabled = true;
            EnterWander();
        }

        /// <summary>
        /// 디스폰/풀 반납 시 호출합니다.
        /// TODO(NET): 서버에서만 호출되어야 합니다.
        /// </summary>
        public void Deactivate()
        {
            isActive = false;

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        /// <summary>
        /// 외부(스폰 매니저)에서 매 프레임 호출해 주는 갱신 함수입니다.
        /// TODO(NET): 호출부에 if (!IsServer) return; 게이트를 걸어야 합니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!isActive) return;
            if (!agent.enabled || !agent.isOnNavMesh) return;

            decisionTimer += deltaTime;
            if (decisionTimer < decisionInterval) return;

            decisionTimer = 0f;

            switch (currentState)
            {
                case EnemyState.Wander:
                    UpdateWander();
                    break;

                case EnemyState.Flee:
                    UpdateFlee();
                    break;
            }
        }

        // 테스트 전용입니다. selfTickForTest 가 꺼져 있으면 아무 일도 하지 않습니다.
        // TODO(NET): 통합 시 이 Update 블록은 삭제하고 스폰 매니저가 Tick 을 호출하게 합니다.
        private void Update()
        {
            if (!selfTickForTest) return;

            if (!isActive) Activate();
            Tick(Time.deltaTime);
        }

        // ─────────────── 배회(Wander) ───────────────

        private void EnterWander()
        {
            currentState = EnemyState.Wander;
            agent.speed = wanderSpeed;
            agent.isStopped = false;

            SetRandomDestination();
        }

        private void UpdateWander()
        {
            Transform player = FindNearestPlayer();

            // 플레이어가 감지 범위 안에 들어오면 즉시 도망 상태로 전환합니다.
            if (player != null
                && Vector3.Distance(transform.position, player.position) <= detectionRadius)
            {
                EnterFlee();
                return;
            }

            // 목적지에 도착했거나 길을 잃었으면 새 목적지를 뽑습니다.
            bool needNewDestination =
                !agent.hasPath
                || agent.pathPending == false && agent.remainingDistance <= arriveThreshold;

            if (needNewDestination)
            {
                SetRandomDestination();
            }
        }

        private void SetRandomDestination()
        {
            // 자기 주변 구(sphere) 안에서 임의의 점을 하나 뽑고,
            // 그 점에서 가장 가까운 NavMesh 위 지점을 목적지로 씁니다.
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        // ─────────────── 도망(Flee) ───────────────

        private void EnterFlee()
        {
            currentState = EnemyState.Flee;
            agent.speed = fleeSpeed; // 화들짝 놀라 속도를 올립니다.
            agent.isStopped = false;
        }

        private void UpdateFlee()
        {
            Transform player = FindNearestPlayer();

            // 쫓아오던 플레이어가 사라졌으면 배회로 복귀합니다.
            if (player == null)
            {
                EnterWander();
                return;
            }

            float distance = Vector3.Distance(transform.position, player.position);

            // 충분히 멀어졌으면 다시 배회합니다.
            if (distance >= safeRadius)
            {
                EnterWander();
                return;
            }

            Vector3 fleeTarget;
            if (TryFindFleePosition(player.position, out fleeTarget))
            {
                agent.SetDestination(fleeTarget);
            }
        }

        /// <summary>
        /// 플레이어 반대 방향으로 도망갈 수 있는 NavMesh 위 지점을 찾습니다.
        /// 정반대 방향이 벽이면 각도를 좌우로 벌려가며 다시 시도합니다.
        /// (구석에 몰렸을 때 벽에 박혀 멈추는 문제를 막기 위한 처리입니다.)
        /// </summary>
        private bool TryFindFleePosition(Vector3 playerPosition, out Vector3 result)
        {
            Vector3 awayDirection = transform.position - playerPosition;
            awayDirection.y = 0f;

            // 플레이어와 정확히 같은 위치에 겹친 예외 상황 처리입니다.
            if (awayDirection.sqrMagnitude < 0.001f)
            {
                awayDirection = transform.forward;
            }

            awayDirection.Normalize();

            // 0도(정반대) → ±40도 → ±80도 → ±120도 순으로 후보를 검사합니다.
            float[] candidateAngles = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };

            for (int i = 0; i < candidateAngles.Length; i++)
            {
                Vector3 direction =
                    Quaternion.Euler(0f, candidateAngles[i], 0f) * awayDirection;

                Vector3 candidate = transform.position + direction * fleeRadius;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, fleeRadius * 0.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                // 찾은 지점이 오히려 플레이어에게 더 가까우면 버립니다.
                float currentDistance = Vector3.Distance(transform.position, playerPosition);
                float candidateDistance = Vector3.Distance(hit.position, playerPosition);

                if (candidateDistance <= currentDistance)
                {
                    continue;
                }

                result = hit.position;
                return true;
            }

            result = transform.position;
            return false;
        }

        // ─────────────── 대상 탐색 ───────────────

        /// <summary>
        /// 가장 가까운 플레이어를 찾습니다.
        /// 4인 멀티이므로 인스펙터로 한 명을 고정 지정하지 않고 매번 탐색합니다.
        ///
        /// PlayerRegistry 는 노석민(04 폴더) 소유이며 여기서는 읽기만 합니다.
        /// TODO: 여러 명에게 둘러싸였을 때를 대비하려면 가장 가까운 한 명이 아니라
        ///       주변 플레이어들의 평균 방향에서 도망가도록 개선이 필요합니다.
        /// </summary>
        private Transform FindNearestPlayer()
        {
            SM.PlayerRegistry registry = SM.PlayerRegistry.Peek();

            if (registry == null)
            {
                return null; // 아직 플레이어가 등록되지 않은 상태입니다. 그냥 배회합니다.
            }

            return registry.GetNearestPlayer(transform.position);
        }

        // ─────────────── 에디터 디버그 ───────────────

        private void OnDrawGizmosSelected()
        {
            // 노란색: 감지 범위 / 초록색: 안전 거리 / 파란색: 배회 반경
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, safeRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, wanderRadius);
        }
    }
}
