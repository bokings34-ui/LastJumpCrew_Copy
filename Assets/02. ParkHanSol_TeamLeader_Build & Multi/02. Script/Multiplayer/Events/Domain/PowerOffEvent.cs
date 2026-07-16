using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.Domain
{
    public sealed class PowerOffEvent : EventBase
    {
        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            Debug.LogError(
                "PHS_POWER_OFF_EVENT_FAILED reason=phs_runtime_adapter_not_connected");
            OnFail();
        }

        public override void OnTick(float deltaTime)
        {
            // PHS runtime adapter owns power state polling after team-domain withdrawal.
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            Debug.Log("PHS_POWER_OFF_EVENT_RESOLVED");
        }
    }
}
