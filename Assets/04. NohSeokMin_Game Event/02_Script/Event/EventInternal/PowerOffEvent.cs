using System;
using UnityEngine;

namespace SM
{
    public class PowerOffEvent : EventBase
    {
        public event Action OnPowerOff;
        public event Action OnPowerRestored;

        public bool IsPowerOffActive { get; private set; }

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            IsPowerOffActive = true;
            OnPowerOff?.Invoke();

            Debug.Log($"<color=lime>[PowerOff]</color> 발생. 전력 차단 신호 발행.");
        }

        public override void OnTick(float deltaTime)
        {
        }

        public void NotifyPowerRestored()
        {
            if (State != EventState.InProgress) return;
            OnResolve();
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            IsPowerOffActive = false;
            OnPowerRestored?.Invoke();
            Debug.Log("<color=lime>[PowerOff]</color> 전력 복구 완료.");
        }

        public override void ForceTerminate()
        {
            IsPowerOffActive = false;
            OnPowerRestored?.Invoke();
            base.ForceTerminate();
        }
    }
}