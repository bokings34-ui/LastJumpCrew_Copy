using System;
using UnityEngine;

namespace SM
{
    public class MicDestroyEvent : EventBase
    {
        private MicDestroyEventDataSO MicData { get { return _data as MicDestroyEventDataSO; } }

        private float _timer;

        public event Action OnMicDisabled;
        public event Action OnMicRestored;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _timer = 0f;

            OnMicDisabled?.Invoke();

            Debug.Log($"<color=lime>[{MicData.EventName}]</color> 통신 장비 파괴. {MicData.disableDuration}초간 마이크 사용 불가.");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _timer += deltaTime;
            if (_timer >= MicData.disableDuration)
            {
                OnResolve();
            }
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            OnMicRestored?.Invoke();

            Debug.Log($"<color=lime>[{MicData.EventName}]</color> 통신 장비 복구 완료.");
        }

        public override void ForceTerminate()
        {
            OnMicRestored?.Invoke();
            base.ForceTerminate();
        }
    }
}