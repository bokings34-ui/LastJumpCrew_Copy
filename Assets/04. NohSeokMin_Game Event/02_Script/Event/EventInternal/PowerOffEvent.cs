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

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            OnPowerOff?.Invoke();

            Debug.Log($"<color=lime>[PowerOff]</color> 발생. 전력 차단 신호 발행.");
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
            ChangeState(EventState.Resolve);
            OnPowerRestored?.Invoke();
            Debug.Log("<color=lime>[PowerOff]</color> 전력 복구 완료.");
        }

        public override void ForceTerminate()
        {
            OnPowerRestored?.Invoke();
            base.ForceTerminate();
        }
    }
}