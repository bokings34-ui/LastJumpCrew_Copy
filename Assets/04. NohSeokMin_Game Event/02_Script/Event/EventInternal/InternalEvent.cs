using UnityEngine;

namespace SM
{
    public abstract class InternalEvent : EventBase
    {
        protected IRoom TargetRoom { get { return Context?.Room; } }

        public override void Initialize(EventDataSO data, EventContext context)
        {
            base.Initialize(data, context);
        }

        // 실제 호출은 IInteractable이 IUsableItem.Use()를 처리하는 과정에서 이어질 예정
        public virtual void ApplyRepair(float amount) { }
        protected abstract float GetMaxRepairProgress();
    }
}