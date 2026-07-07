using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class FireEvent : InternalEvent
    {
        private FireEventDataSO FireData { get { return _data as FireEventDataSO; } }

        private int _fireLevel = 1;
        private float _timer = 0f;
        private int _nextSpawnIndex = 0;
        private readonly List<FireEffectInstance> _activeEffects = new List<FireEffectInstance>();

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _fireLevel = 1;
            _timer = 0f;
            _nextSpawnIndex = 0;
            _activeEffects.Clear();

            SpawnNextEffect();
            Debug.Log($"<color=lime>[{FireData.EventName}]</color> 발생! 초기 레벨: {_fireLevel}");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _timer += deltaTime;

            if (_timer >= FireData.levelUpInterval)
            {
                _timer = 0f;
                _fireLevel++;
                SpawnNextEffect();
                Debug.Log($"<color=lime>[{FireData.EventName}]</color> 현재 레벨: {_fireLevel}");
            }
        }

        private void SpawnNextEffect()
        {
            if (Context == null || Context.Room == null) return;

            var spawnPoints = Context.Room.FireSpawnPoints;

            if (spawnPoints == null || spawnPoints.Count == 0) return;

            if (_nextSpawnIndex >= spawnPoints.Count)
            {
                Debug.Log($"<color=lime>[{FireData.EventName}]</color> 스폰 포인트 추가 설정 필요.");
                return;
            }

            var point = spawnPoints[_nextSpawnIndex];
            var effect = FireEffectPool.Instance.Get(point.position, FireData.damagePerSecond);

            _activeEffects.Add(effect);
            _nextSpawnIndex++;
        }

        protected override float GetMaxRepairProgress() => FireData.maxRepairProgress;

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);

            foreach (var effect in _activeEffects)
            {
                FireEffectPool.Instance.Return(effect);
            }

            _activeEffects.Clear();
            Debug.Log($"<color=lime>[{FireData.EventName}]</color> 종료.");
        }
    }
}