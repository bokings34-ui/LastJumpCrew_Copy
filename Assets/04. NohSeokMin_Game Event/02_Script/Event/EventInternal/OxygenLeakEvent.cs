using UnityEngine;

namespace SM
{
    public class OxygenLeakEvent : InternalEvent
    {
        private OxygenLeakEventDataSO LeakData { get { return _data as OxygenLeakEventDataSO; } }

        private OxygenLeakEffectInstance _effect;
        private IEventEffectRuntimeBridge _effectRuntimeBridge;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;
        private uint _effectInstanceId;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _effectRuntimeBridge = Context?.RuntimeBridge as IEventEffectRuntimeBridge;
            _repairRuntimeBridge = Context?.RuntimeBridge as IEventRepairRuntimeBridge;
            _effectInstanceId = 0U;

            var point = ShipSpawnPointConfig.Instance.GetRandomPoint();

            if (point == null)
            {
                Debug.Log($"</color=lime>[{LeakData.EventName}]</color> 스폰 가능한 스폰 포인트가 없음.");
                OnFail();
                return;
            }

            _effect = OxygenLeakEffectPool.Instance.Get(point.position, LeakData);
            if (_effectRuntimeBridge != null)
            {
                _effectInstanceId = _effectRuntimeBridge.AllocateEffectInstanceId(InstanceId);
                if (_effectInstanceId == 0U)
                {
                    OxygenLeakEffectPool.Instance.Return(_effect);
                    _effect = null;
                    Debug.LogError($"PHS_OXYGEN_EFFECT_SPAWN_FAILED reason=effect_id_missing event={InstanceId}");
                    OnFail();
                    return;
                }

                if (!_effect.BindRepairTarget(InstanceId, _effectInstanceId, _repairRuntimeBridge))
                {
                    OxygenLeakEffectPool.Instance.Return(_effect);
                    _effect = null;
                    _effectInstanceId = 0U;
                    Debug.LogError($"PHS_OXYGEN_EFFECT_SPAWN_FAILED reason=repair_target_registration event={InstanceId}");
                    OnFail();
                    return;
                }

                _effectRuntimeBridge.PublishEffectSpawned(
                    InstanceId,
                    _effectInstanceId,
                    EventEffectKind.OxygenLeak,
                    point.position,
                    0);
            }

            _effect.OnSealed += HandleSealed;
        }

        public override void OnTick(float deltaTime)
        {
        }

        private void HandleSealed(OxygenLeakEffectInstance effect)
        {
            effect.OnSealed -= HandleSealed;
            effect.UnbindRepairTarget();
            PublishEffectRemoved();
            OxygenLeakEffectPool.Instance.Return(effect);
            _effect = null;

            OnResolve();
        }

        protected override float GetMaxRepairProgress() => 0f;
        public override void ApplyRepair(float amount) { }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            Debug.Log($"<color=lime>[{LeakData.EventName}]</color> 누출 부위 수리 완료.");
        }

        public override void ForceTerminate()
        {
            if (_effect != null)
            {
                _effect.OnSealed -= HandleSealed;
                _effect.UnbindRepairTarget();
                PublishEffectRemoved();
                OxygenLeakEffectPool.Instance.Return(_effect);
                _effect = null;
            }

            base.ForceTerminate();
        }

        private void PublishEffectRemoved()
        {
            if (_effectInstanceId == 0U)
            {
                return;
            }

            _effectRuntimeBridge?.PublishEffectRemoved(InstanceId, _effectInstanceId);
            _effectInstanceId = 0U;
        }
    }
}
