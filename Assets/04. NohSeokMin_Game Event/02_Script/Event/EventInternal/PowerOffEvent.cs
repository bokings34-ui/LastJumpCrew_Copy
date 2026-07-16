using UnityEngine;

namespace SM
{
    public sealed class PowerOffEvent : EventBase
    {
        private IShipPowerEventRuntimeBridge powerRuntimeBridge;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            powerRuntimeBridge = Context?.RuntimeBridge as IShipPowerEventRuntimeBridge;
            if (powerRuntimeBridge == null)
            {
                Debug.LogError(
                    $"PHS_POWER_OFF_EVENT_FAILED reason=power_runtime_bridge_missing instance={InstanceId}");
                OnFail();
                return;
            }

            if (!powerRuntimeBridge.TryApplyPowerOff(InstanceId, out var reason))
            {
                Debug.LogError(
                    $"PHS_POWER_OFF_EVENT_FAILED reason={reason} instance={InstanceId}");
                OnFail();
                return;
            }

            Debug.Log($"PHS_POWER_OFF_EVENT_STARTED instance={InstanceId} room={RoomId}");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress
                || powerRuntimeBridge == null
                || !powerRuntimeBridge.TryGetPowerOffState(out var isPowerOff)
                || isPowerOff)
            {
                return;
            }

            OnResolve();
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            Debug.Log($"PHS_POWER_OFF_EVENT_RESOLVED instance={InstanceId} room={RoomId}");
        }
    }
}
