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
        private readonly Dictionary<Transform, FireEffectInstance> _occupiedPoints = new Dictionary<Transform, FireEffectInstance>();
        private readonly Dictionary<FireEffectInstance, Transform> _effectToPoint = new Dictionary<FireEffectInstance, Transform>();
        private readonly Dictionary<FireEffectInstance, uint> _effectInstanceIds = new Dictionary<FireEffectInstance, uint>();
        private IEventEffectRuntimeBridge _effectRuntimeBridge;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _timer = 0f;
            _activeEffects.Clear();
            _occupiedPoints.Clear();
            _effectToPoint.Clear();
            _effectInstanceIds.Clear();
            _effectRuntimeBridge = Context?.RuntimeBridge as IEventEffectRuntimeBridge;
            _repairRuntimeBridge = Context?.RuntimeBridge as IEventRepairRuntimeBridge;

            SpawnNextFire();
            if (_activeEffects.Count == 0)
            {
                Debug.LogError($"[{FireData.EventName}] 사용 가능한 화재 스폰 포인트가 없습니다.");
                OnFail();
            }
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
                Debug.Log($"<color=lime>[{FireData.EventName}]</color> 현재 레벨: {FireLevel}");
            }
        }

        private Transform GetFreeSpawnPoint()
        {
            var spawnPoints = Context?.Room?.FireSpawnPoints;
            if (spawnPoints == null || spawnPoints.Count == 0) return null;

            var freePoints = new List<Transform>();
            foreach (var point in spawnPoints)
            {
                if (!_occupiedPoints.ContainsKey(point))
                    freePoints.Add(point);
            }

            if (freePoints.Count == 0) return null;
            return freePoints[Random.Range(0, freePoints.Count)];
        }

        private void SpawnNextFire()
        {
            var point = GetFreeSpawnPoint();
            if (point == null) return;

            var effect = FireEffectPool.Instance.Get
                (point.position, FireData.damagePerSecond, FireData.maxRepairProgress);

            var effectInstanceId = AllocateEffectInstanceId();
            if (_effectRuntimeBridge != null && effectInstanceId == 0U)
            {
                FireEffectPool.Instance.Return(effect);
                Debug.LogError($"PHS_FIRE_EFFECT_SPAWN_FAILED reason=effect_id_missing event={InstanceId}");
                return;
            }

            effect.OnRemove += HandleRemoveFire;

            _occupiedPoints[point] = effect;
            _effectToPoint[effect] = point;
            _activeEffects.Add(effect);
            if (effectInstanceId != 0U)
            {
                if (!effect.BindRepairTarget(InstanceId, effectInstanceId, _repairRuntimeBridge))
                {
                    effect.OnRemove -= HandleRemoveFire;
                    _occupiedPoints.Remove(point);
                    _effectToPoint.Remove(effect);
                    _activeEffects.Remove(effect);
                    FireEffectPool.Instance.Return(effect);
                    Debug.LogError($"PHS_FIRE_EFFECT_SPAWN_FAILED reason=repair_target_registration event={InstanceId} effect={effectInstanceId}");
                    return;
                }

                _effectInstanceIds[effect] = effectInstanceId;
                _effectRuntimeBridge.PublishEffectSpawned(
                    InstanceId,
                    effectInstanceId,
                    EventEffectKind.Fire,
                    point.position,
                    0);
            }
        }

        private void HandleRemoveFire(FireEffectInstance effect)
        {
            effect.OnRemove -= HandleRemoveFire;
            effect.UnbindRepairTarget();
            PublishEffectRemoved(effect);

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
                effect.UnbindRepairTarget();
                PublishEffectRemoved(effect);
                FireEffectPool.Instance.Return(effect);
            }
            _activeEffects.Clear();
            _occupiedPoints.Clear();
            _effectToPoint.Clear();
            _effectInstanceIds.Clear();

            base.ForceTerminate();
        }

        private uint AllocateEffectInstanceId()
        {
            return _effectRuntimeBridge == null
                ? 0U
                : _effectRuntimeBridge.AllocateEffectInstanceId(InstanceId);
        }

        private void PublishEffectRemoved(FireEffectInstance effect)
        {
            if (!_effectInstanceIds.Remove(effect, out var effectInstanceId))
            {
                return;
            }

            _effectRuntimeBridge?.PublishEffectRemoved(InstanceId, effectInstanceId);
        }
    }
}
