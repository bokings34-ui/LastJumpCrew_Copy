using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
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
            var powerBridge = Context?.RuntimeBridge as IShipPowerEventRuntimeBridge;
            if (powerBridge == null)
            {
                Debug.LogError("PHS_POWER_OFF_FAILED reason=power_runtime_bridge_missing");
                OnFail();
                return;
            }

            if (!powerBridge.TryApplyPowerOff(InstanceId, out var reason))
            {
                Debug.LogError($"PHS_POWER_OFF_FAILED reason=apply_rejected:{reason} instance={InstanceId}");
                OnFail();
                return;
            }

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
