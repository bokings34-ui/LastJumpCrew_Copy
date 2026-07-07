using UnityEngine;

namespace SM
{
    public abstract class InternalEvent : EventBase, IRepairable
    {
        protected IRoom TargetRoom { get { return Context?.Room; } }

        private float _repairProgress = 0f;
        public float RepairProgress { get { return _repairProgress; } }
        public bool IsRepaired { get; private set; }

        public override void Initialize(EventDataSO data, EventContext context)
        {
            base.Initialize(data, context);
            _repairProgress = 0f;
            IsRepaired = false;

            if (TargetRoom == null)
            {
                Debug.Log($"[{Id}] Room이 지정되지 않았습니다.");
            }
        }

        // 수리 상호작용 시 호출
        public virtual void ApplyRepair(float amount)
        {
            if (State != EventState.InProgress) return;

            _repairProgress += amount;

            if (_repairProgress >= GetMaxRepairProgress())
            {
                IsRepaired = true;
                OnResolve();
            }
        }

        protected abstract float GetMaxRepairProgress();
    }
}