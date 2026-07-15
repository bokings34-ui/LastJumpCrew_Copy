using System;
using UnityEngine;

namespace SM
{
    public class ZoneEventScheduler : MonoSingleton<ZoneEventScheduler>
    {
        public event Action OnNebulaTriggered;

        [SerializeField] private ZoneBehaviorConfigSO behaviorConfig;

        private ZoneBehaviorEntry _currentEntry;
        private ZoneType _currentZone;
        private bool _isRunning;
        private float _timer;
        private float _currentInterval;

        public void SetCurrentZone(ZoneType zone)
        {
            _currentEntry = behaviorConfig.GetEntry(zone);

            if (_currentEntry == null)
            {
                Debug.Log($"<color=lime>[ZoneEventScheduler]</color> {zone}에 대한 설정이 없음.");
                return;
            }

            _currentZone = zone;
            _timer = 0f;
            RollNextInterval();

            Debug.Log($"<color=lime>[ZoneEventScheduler]</color> 현재 존 : {zone}");
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

        private void Update()
        {
            if (!_isRunning || _currentEntry == null) return;

            _timer += Time.deltaTime;

            if (_timer >= _currentInterval)
            {
                _timer = 0f;
                RollNextInterval();
                ExecuteZoneEvent();
            }
        }

        private void RollNextInterval()
        {
            _currentInterval = UnityEngine.Random.Range(_currentEntry.intervalMin, _currentEntry.intervalMax);
        }

        private void ExecuteZoneEvent()
        {
            if (_currentZone == ZoneType.NebulaZone)
            {
                OnNebulaTriggered?.Invoke();
                Debug.Log("<color=lime>[ZoneEventScheduler]</color> 성운지대 : 미니맵 끄기 신호 발행.");
                return;
            }
            EventScheduler.Instance.TrySpawnEvent(_currentEntry.eventId);
        }
    }
}