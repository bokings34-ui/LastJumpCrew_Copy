using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EventScheduler : MonoSingleton<EventScheduler>
    {
        [Header("사고 발생 풀")]
        [SerializeField]
        private List<EventId> eventPool = new List<EventId>
        {
            EventId.Fire,
            EventId.EnemySpawn,
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
                TrySpawnEvent();

                if (i < count - 1)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        private void TrySpawnEvent()
        {
            var eventId = GetRandomEventId();

            if (eventId == null)
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> 발생 가능한 이벤트가 없음.");
                return;
            }

            if (_activeEventCount >= MaxActiveEvents)
            {
                _waitQueue.Enqueue(eventId.Value);

                Debug.Log($"<color=lime>[EventScheduler]</color> 활성 사고 최대치({MaxActiveEvents}) 도달. " +
                    $"/ 대기열 등록 : {eventId.Value}");
                return;
            }

            SpawnEvent(eventId.Value);
        }

        private void SpawnEvent(EventId eventId)
        {
            var room = RoomRegistry.Instance.GetRandomRoom();

            if (room == null)
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> 등록된 Room이 없어 사고를 발생시킬 수 없습니다.");
                return;
            }

            EventManager.Instance.SpawnEvent(eventId, room, HandleEventFinished);
            _activeEventCount++;
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