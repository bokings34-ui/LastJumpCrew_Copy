using System;
using UnityEngine;

namespace SM
{
    public class PowerOffEvent : EventBase
    {
        // 담당 매니저가 구독: 문 잠금, 정전, 배터리 삭제를 여기서 전부 처리
        public event Action OnPowerOff;
        // 배터리 재장착 감지 시 담당 매니저가 이 이벤트를 호출해서 종료시킴
        public event Action OnPowerRestored;
        private IPowerFailureRoom _powerRoom;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);

            var roomComponent = Context?.Room as Component;
            _powerRoom = roomComponent == null
                ? null
                : roomComponent.GetComponent<IPowerFailureRoom>();
            if (_powerRoom == null)
            {
                Debug.LogError(
                    $"PHS_POWER_OFF_EVENT_FAILED reason=room_power_controller_missing event={InstanceId} room={RoomId}");
                OnFail();
                return;
            }

            if (!_powerRoom.TrySetPowerFailure(
                    true,
                    InstanceId,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_POWER_OFF_EVENT_FAILED reason={reason} event={InstanceId} room={RoomId}");
                _powerRoom = null;
                OnFail();
                return;
            }

            OnPowerOff?.Invoke();

            Debug.Log(
                $"<color=lime>[PowerOff]</color> 발생. room={RoomId} event={InstanceId}");
        }

        public override void OnTick(float deltaTime)
        {
            // 시간 기반 로직 없음. 담당 매니저가 배터리 재장착 감지 시 NotifyPowerRestored() 직접 호출
        }

        // 담당 매니저가 배터리 재장착을 감지하면 이 메서드를 호출
        public void NotifyPowerRestored()
        {
            if (State != EventState.InProgress) return;
            OnResolve();
        }

        public override void OnResolve()
        {
            if (_powerRoom != null
                && !_powerRoom.TrySetPowerFailure(
                    false,
                    InstanceId,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_POWER_RESTORE_FAILED reason={reason} event={InstanceId} room={RoomId}");
                return;
            }

            ChangeState(EventState.Resolve);
            OnPowerRestored?.Invoke();
            _powerRoom = null;
            Debug.Log(
                $"<color=lime>[PowerOff]</color> 전력 복구 완료. room={RoomId} event={InstanceId}");
        }

        public override void ForceTerminate()
        {
            if (_powerRoom != null
                && !_powerRoom.TrySetPowerFailure(
                    false,
                    InstanceId,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_POWER_FORCE_RESTORE_FAILED reason={reason} event={InstanceId} room={RoomId}");
            }

            _powerRoom = null;
            OnPowerRestored?.Invoke();
            base.ForceTerminate();
        }
    }
}
