using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class FireEvent : InternalEvent
    {
        private FireEventDataSO FireData { get { return _data as FireEventDataSO; } }

        private float _timer = 0f;
        public int FireLevel { get { return _activeEffects.Count; } }

        private readonly List<FireEffectInstance> _activeEffects = new List<FireEffectInstance>();
        private readonly Dictionary<ShipSpawnPoint, FireEffectInstance> _occupiedPoints = new Dictionary<ShipSpawnPoint, FireEffectInstance>();
        private readonly Dictionary<FireEffectInstance, ShipSpawnPoint> _effectToPoint = new Dictionary<FireEffectInstance, ShipSpawnPoint>();

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _timer = 0f;
            _activeEffects.Clear();
            _occupiedPoints.Clear();
            _effectToPoint.Clear();

            SpawnNextFire();
            //Debug.Log($"<color=lime>[{FireData.EventName}]</color> 발생! 초기 레벨: {FireLevel}");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _timer += deltaTime;

            if (_timer >= FireData.levelUpInterval)
            {
                _timer = 0f;
                SpawnNextFire();
            }
        }

        private ShipSpawnPoint PickNextSpawnPoint()
        {
            if (_occupiedPoints.Count == 0)
            {
                return ShipSpawnPointConfig.Instance.GetRandomPoint();
            }

            var candidates = new List<ShipSpawnPoint>();

            foreach (var occupied in _occupiedPoints.Keys)
            {
                foreach (var neighbor in occupied.Neighbors)
                {
                    if (!_occupiedPoints.ContainsKey(neighbor) && !candidates.Contains(neighbor))
                    {
                        candidates.Add(neighbor);
                    }
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        private void SpawnNextFire()
        {
            var point = PickNextSpawnPoint();
            if (point == null) return;

            var effect = FireEffectPool.Instance.Get(point.transform.position, FireData.damagePerSecond, FireData.maxRepairProgress);
            effect.OnRemove += HandleRemoveFire;

            _occupiedPoints[point] = effect;
            _effectToPoint[effect] = point;
            _activeEffects.Add(effect);

            Debug.Log($"<color=lime>[{FireData.EventName}]</color> 현재 레벨: {FireLevel}");
        }

        private void HandleRemoveFire(FireEffectInstance effect)
        {
            effect.OnRemove -= HandleRemoveFire;

            if (_effectToPoint.TryGetValue(effect, out var point))
            {
                _occupiedPoints.Remove(point);
                _effectToPoint.Remove(effect);
            }

            _activeEffects.Remove(effect);
            FireEffectPool.Instance.Return(effect);

            Debug.Log($"<color=lime>[{FireData.EventName}]</color> 화재 진화 성공 / 남은 화재 수: {_activeEffects.Count}");

            if (_activeEffects.Count == 0)
            {
                OnResolve();
            }
        }

        protected override float GetMaxRepairProgress() => 0f;

        public override void ApplyRepair(float amount)
        { 
            // Fire는 개별 단위로 진화되므로 사용 안 함
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            Debug.Log($"<color=lime>[{FireData.EventName}]</color> 종료.");
        }

        public override void ForceTerminate()
        {
            foreach (var effect in _activeEffects)
            {
                effect.OnRemove -= HandleRemoveFire;
                FireEffectPool.Instance.Return(effect);
            }

            _activeEffects.Clear();
            _occupiedPoints.Clear();
            _effectToPoint.Clear();

            base.ForceTerminate();
        }
    }
}