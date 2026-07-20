using UnityEngine;

namespace SM
{
    public class OxygenLeakEvent : InternalEvent
    {
        private OxygenLeakEventDataSO LeakData { get { return _data as OxygenLeakEventDataSO; } }

        private OxygenLeakEffectInstance _effect;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);

            var point = ShipSpawnPointConfig.Instance.GetRandomPoint();

            if (point == null)
            {
                Debug.Log($"</color=lime>[{LeakData.EventName}]</color> 스폰 가능한 스폰 포인트가 없음.");
                OnFail();
                return;
            }

            _effect = OxygenLeakEffectPool.Instance.Get(point.transform.position, LeakData);
            _effect.OnSealed += HandleSealed;
        }

        public override void OnTick(float deltaTime)
        {
        }

        private void HandleSealed(OxygenLeakEffectInstance effect)
        {
            effect.OnSealed -= HandleSealed;
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
                OxygenLeakEffectPool.Instance.Return(_effect);
                _effect = null;
            }

            base.ForceTerminate();
        }
    }
}