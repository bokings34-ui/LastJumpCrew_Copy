using System;
using UnityEngine;

namespace SM
{
    public class EngineBreakEvent : InternalEvent
    {
        private EngineBreakEventDataSO EngineData { get { return _data as EngineBreakEventDataSO; } }

        private float _repairProgress;
        private float _fuelLossTimer;

        public float RepairProgress => _repairProgress;
        public float MaxRepairProgress =>
            EngineData == null ? 0f : EngineData.maxRepairProgress;

        // 담당 매니저(워프 게이지 시스템)가 구독
        public event Action OnEngineBroken;
        public event Action OnFuelLoss;
        public event Action OnEngineRestored;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _repairProgress = 0f;
            _fuelLossTimer = 0f;

            OnEngineBroken?.Invoke();

            Debug.Log($"<color=lime>[{EngineData.EventName}]</color> 발생. 엔진 고장 신호 발행.");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _fuelLossTimer += deltaTime;
            if (_fuelLossTimer >= EngineData.fuelLossInterval)
            {
                _fuelLossTimer = 0f;
                OnFuelLoss?.Invoke();
            }
        }

        protected override float GetMaxRepairProgress() => EngineData.maxRepairProgress;

        public override void ApplyRepair(float amount)
        {
            if (State != EventState.InProgress) return;

            _repairProgress += amount;
            if (_repairProgress >= EngineData.maxRepairProgress)
            {
                OnResolve();
            }
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            OnEngineRestored?.Invoke();
            Debug.Log($"<color=lime>[{EngineData.EventName}]</color> 엔진 수리 완료.");
        }

        public override void ForceTerminate()
        {
            OnEngineRestored?.Invoke();
            base.ForceTerminate();
        }
    }
}
