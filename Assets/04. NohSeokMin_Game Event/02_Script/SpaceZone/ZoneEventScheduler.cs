using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ZoneEventScheduler : MonoSingleton<ZoneEventScheduler>, IEventSpawner
    {
        public event Action OnNebulaTriggered;

        [Header("Zone별 이벤트 매핑 데이터")]
        [SerializeField] private ZoneBehaviorConfigSO behaviorConfig;

        [Header("발생 주기 (고정, 초)")]
        [SerializeField] private float spawnInterval = 30f;

        private bool _isRunning;
        private float _timer;
        private ZoneType _currentZone;
        private bool _hasZone;

        public void SetCurrentZone(ZoneType zone)
        {
            _currentZone = zone;
            _hasZone = true;
            Debug.Log($"<color=cyan>[ZoneEventScheduler]</color> 현재 Zone: {zone}");
        }

        public void StartScheduler()
        {
            _isRunning = true;
            _timer = 0f;
        }

        public void StopScheduler()
        {
            _isRunning = false;
        }

        public void ForceClearAll()
        {
            _isRunning = false;
            _timer = 0f;
            Debug.Log("<color=cyan>[ZoneEventScheduler]</color> 정지 및 초기화.");
        }

        private void Update()
        {
            if (!_isRunning || !_hasZone) return;

            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0f;
                TryTriggerZoneEvent();
            }
        }

        private void TryTriggerZoneEvent()
        {
            if (_currentZone == ZoneType.NebulaZone)
            {
                OnNebulaTriggered?.Invoke();
                Debug.Log("<color=magenta>[ZoneEventScheduler]</color> 성운지대 - 미니맵 토글 신호 발행.");
                return;
            }

            var candidates = behaviorConfig.GetEventIds(_currentZone);
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning($"<color=cyan>[ZoneEventScheduler]</color> {_currentZone}에 대한 이벤트 매핑이 없습니다.");
                return;
            }

            var pool = new List<EventId>();
            foreach (var id in candidates)
            {
                if (!EventManager.Instance.IsActive(id))
                    pool.Add(id);
            }

            if (pool.Count == 0)
            {
                Debug.Log($"<color=cyan>[ZoneEventScheduler]</color> {_currentZone}의 발생 가능한 이벤트가 모두 진행 중, 이번 주기 건너뜀.");
                return;
            }

            var eventId = pool[UnityEngine.Random.Range(0, pool.Count)];
            TrySpawnEvent(eventId, null);
        }

        public void TrySpawnEvent(EventId eventId, IRoom room)
        {
            var targetRoom = room ?? RoomRegistry.Instance.GetRandomRoom();
            if (targetRoom == null)
            {
                Debug.Log("<color=cyan>[ZoneEventScheduler]</color> 등록된 Room이 없어 발생시킬 수 없습니다.");
                return;
            }

            EventManager.Instance.SpawnEvent(eventId, targetRoom);
            Debug.Log($"<color=cyan>[ZoneEventScheduler]</color> {eventId} 경고 발생! (Zone: {_currentZone})");
        }

        void IEventSpawner.SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished)
        {
            TrySpawnEvent(id, targetRoom);
        }
    }
}