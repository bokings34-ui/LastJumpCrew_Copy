using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EventScheduler : MonoSingleton<EventScheduler>, IEventSpawner
    {
        [Header("내부 사고 이벤트 발생 풀")]
        [SerializeField]
        private List<EventId> eventPool = new List<EventId>
        {
            EventId.Fire,
            EventId.EnemySpawn,
            EventId.OxygenLeak,

            // TODO :: PowerOff, EngineBreak, MicDestroy 구현 완료 후 추가
        };

        private const float TotalTime = 300f;
        private const float SpawnInterval = 15f;
        private const float DoubleSpawnTime = 150f;
        private const int MaxActiveEvents = 3;
        private const float RequeueDelay = 5f;

        private float _runningTime;
        private float _spawnTimer;
        private bool _isRunning;
        private int _activeEventCount;
        private bool _isProcessingQueue;

        private struct WaitEvent
        {
            public EventId EventId;
            public IRoom Room;
        }

        private readonly Queue<WaitEvent> _waitQueue = new Queue<WaitEvent>();

        public bool IsRunning => _isRunning;
        public float RunningTime => _runningTime;

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

        public void ForceClearAll()
        {
            _isRunning = false;
            StopAllCoroutines();
            _activeEventCount = 0;
            _waitQueue.Clear();

            EventManager.Instance.ForceClearAll();

            Debug.Log("<color=lime>[EventScheduler]</color> 스테이지 종료 - 강제 클리어 완료.");
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

                int spawnCount = GetSpawnCountForCurrentTime(_runningTime);
                StartCoroutine(SpawnEventSequence(spawnCount));
            }
        }

        // 순수 판단 함수 - 입력(시간)만으로 결과가 결정됨. 네트워크에서 서버 시간을 넣어 그대로 재사용 가능.
        public static int GetSpawnCountForCurrentTime(float runningTime)
        {
            return runningTime >= DoubleSpawnTime ? 2 : 1;
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

            TrySpawnEvent(eventId.Value, null);
        }

        public void TrySpawnEvent(EventId eventId, IRoom room)
        {
            if (IsWaiting(eventId))
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> {eventId}는 이미 대기열에 있음, 중복 요청 무시.");
                return;
            }

            bool alreadyActive = EventManager.Instance.IsActive(eventId);
            bool slotsFull = _activeEventCount >= MaxActiveEvents;

            if (alreadyActive || slotsFull)
            {
                _waitQueue.Enqueue(new WaitEvent { EventId = eventId, Room = room });

                string reason = alreadyActive ? "이미 진행 중" : "활성 슬롯 최대치";
                Debug.Log($"<color=lime>[EventScheduler]</color> {eventId} 대기열 등록 (사유: {reason})");
                LogWaitQueue();
                return;
            }

            SpawnEvent(eventId, room);
        }

        private void LogWaitQueue()
        {
            if (_waitQueue.Count == 0)
            {
                Debug.Log("<color=lime>[EventScheduler]</color> 현재 대기열: 비어있음");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=lime>[EventScheduler]</color> 현재 대기열: ");

            int index = 1;
            foreach (var item in _waitQueue)
            {
                sb.Append($"({index}) {item.EventId}  ");
                index++;
            }

            Debug.Log(sb.ToString());
        }

        private void SpawnEvent(EventId eventId, IRoom room)
        {
            var targetRoom = room ?? RoomRegistry.Instance.GetRandomRoom();

            if (targetRoom == null)
            {
                Debug.Log($"<color=lime>[EventScheduler]</color> 등록된 Room이 없어 사고를 발생시킬 수 없습니다.");
                return;
            }

            EventManager.Instance.SpawnEvent(eventId, targetRoom, HandleEventFinished);
            _activeEventCount++;

            Debug.Log($"<color=lime>[EventScheduler]</color> {eventId} 발생!");
        }

        private void HandleEventFinished(EventBase evt, bool success)
        {
            _activeEventCount--;
            TryProcessQueue();
        }

        private void TryProcessQueue()
        {
            if (_isProcessingQueue) return;
            if (_waitQueue.Count == 0) return;

            StartCoroutine(ProcessQueueRoutine());
        }

        private IEnumerator ProcessQueueRoutine()
        {
            _isProcessingQueue = true;

            while (_waitQueue.Count > 0)
            {
                yield return new WaitForSeconds(RequeueDelay);

                if (_activeEventCount >= MaxActiveEvents) continue;

                var next = _waitQueue.Peek();
                if (EventManager.Instance.IsActive(next.EventId))
                {
                    continue;
                }

                _waitQueue.Dequeue();
                SpawnEvent(next.EventId, next.Room);
                LogWaitQueue();
            }

            _isProcessingQueue = false;
        }

        private bool IsWaiting(EventId id)
        {
            foreach (var item in _waitQueue)
            {
                if (item.EventId == id) return true;
            }
            return false;
        }

        private EventId? GetRandomEventId()
        {
            var pool = new List<EventId>();

            foreach (var id in eventPool)
            {
                if (!EventManager.Instance.IsActive(id) && !IsWaiting(id))
                    pool.Add(id);
            }

            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        void IEventSpawner.SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished)
        {
            TrySpawnEvent(id, targetRoom);
        }
    }
}