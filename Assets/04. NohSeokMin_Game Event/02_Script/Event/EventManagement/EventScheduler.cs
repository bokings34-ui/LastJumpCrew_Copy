using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EventScheduler : MonoSingleton<EventScheduler>
    {
        [Header("내부 사고 이벤트 발생 풀")]
        [SerializeField] private List<EventId> eventPool = new List<EventId>
        {
            EventId.Fire,
            EventId.EnemySpawn,
            EventId.OxygenLeak,

            // TODO :: PowerOff, EngineBreak, MicDestroy 구현 완료 후 추가
        };

        private const float TotalTime = 300f;
        private const float SpawnInterval = 30f;
        private const float DoubleSpawnTime = 150f;
        private const int MaxActiveEvents = 2;
        private const float RequeueDelay = 5f;

        private float _runningTime;
        private float _spawnTimer;
        private bool _isRunning;
        private int _activeEventCount;

        private readonly Queue<EventId> _waitQueue = new Queue<EventId>();

        public void StartScheduler()
        {
            _runningTime = 0f;
            _spawnTimer = 0f;
            _activeEventCount = 0;
            _waitQueue.Clear();
            _isRunning = true;
        }

        public void StopScheduler()
        {
            _isRunning = false;
        }

        // 스테이지 종료 시 GameManager가 호출할 것 (스케줄러 정지, 진행 중이던 모든 사고 강제 종료)
        public void ForceClearAll()
        {
            _isRunning = false;
            StopAllCoroutines();
            _activeEventCount = 0;
            _waitQueue.Clear();

            EventManager.Instance.ForceClearAll();

            Debug.Log("[IncidentScheduler] 스테이지 종료 - 강제 클리어 완료.");
        }

        private void Update()
        {
            if (!_isRunning) return;

            _runningTime += Time.deltaTime;

            if (_runningTime >= TotalTime)
            {
                _isRunning = false;
                Debug.Log("<color=lime>[EventScheduler]</color> 5분 종료. 스케줄러 정지.");
                return;
            }

            _spawnTimer += Time.deltaTime;

            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;

                int spawnCount = _runningTime >= DoubleSpawnTime ? 2 : 1;
                StartCoroutine(SpawnEventSequence(spawnCount));
            }
        }

        private IEnumerator SpawnEventSequence(int count)
        {
            for (int i = 0; i < count; i++)
            {
                TryTriggerRandomEvent();

                if (i < count - 1)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        private void TryTriggerRandomEvent()
        {
            var eventId = GetRandomEventId();
            if (eventId == null) return;

            TrySpawnEvent(eventId.Value);
        }

        public void TrySpawnEvent(EventId eventId)
        {
            if (EventManager.Instance.IsActive(eventId) || _waitQueue.Contains(eventId))
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> {eventId}는 이미 진행 중이거나 대기 중, 요청 무시.");
                return;
            }

            if (_activeEventCount >= MaxActiveEvents)
            {
                _waitQueue.Enqueue(eventId);
                Debug.Log($"<color=lime>[EventScheduler]</color> 활성 사고 최대치 / 대기열 등록: {eventId}");
                return;
            }

            SpawnEvent(eventId);
        }

        private void SpawnEvent(EventId eventId)
        {
            var room = RoomRegistry.Instance.GetRandomRoom();

            if (room == null)
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> 등록된 Room이 없어 사고를 발생시킬 수 없습니다.");
                return;
            }

            _activeEventCount++;

            if (!EventManager.Instance.SpawnEvent(eventId, room, HandleEventFinished))
            {
                _activeEventCount--;
                Debug.LogError($"[EventScheduler] {eventId} 생성 요청이 거부되었습니다.", this);
                return;
            }

            Debug.Log($"<color=lime>[EventScheduler]</color> {eventId} 발생!");
        }

        private void HandleEventFinished(EventBase evt, bool success)
        {
            _activeEventCount--;
            StartCoroutine(SpawnWaitingEvent());
        }

        private IEnumerator SpawnWaitingEvent()
        {
            yield return new WaitForSeconds(RequeueDelay);

            if (_waitQueue.Count > 0 && _activeEventCount < MaxActiveEvents)
            {
                var nextId = _waitQueue.Dequeue();
                SpawnEvent(nextId);
            }
        }

        private EventId? GetRandomEventId()
        {
            var pool = new List<EventId>();

            foreach (var id in eventPool)
            {
                if (!EventManager.Instance.IsActive(id) && !_waitQueue.Contains(id))
                    pool.Add(id);
            }

            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }
    }
}
