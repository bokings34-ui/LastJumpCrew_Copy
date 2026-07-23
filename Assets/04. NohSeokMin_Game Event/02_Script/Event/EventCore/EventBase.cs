using System;

namespace SM
{
    public abstract class EventBase
    {
        private EventState _state = EventState.Ready;
        public EventState State
        {
            get { return _state; }
            protected set { _state = value; }
        }

        protected EventDataSO _data;
        protected EventContext Context { get; private set; }

        public ulong InstanceId { get { return Context?.InstanceId ?? 0UL; } }
        public EventType Type { get { return _data.Type; } }
        public EventId Id { get { return _data.Id; } }
        public string RoomId { get { return Context?.Room?.RoomId ?? string.Empty; } }

        public event Action<EventBase, bool> OnFinished;

        public virtual void Initialize(EventDataSO data, EventContext context)
        {
            _state = EventState.Ready;
            _data = data;
            Context = context;
        }

        // 발생
        public abstract void OnTrigger();

        // 매 프레임 흘러갈 로직
        public virtual void OnTick(float deltaTime)
        {

        }

        // 해결
        public abstract void OnResolve();

        // 실패
        public virtual void OnFail()
        {
            ChangeState(EventState.Fail);
        }

        // 스테이지 종료 시 강제 종료
        public virtual void ForceTerminate()
        {
            ChangeState(EventState.Resolve);
        }

        protected void ChangeState(EventState nextState)
        {
            if (_state == EventState.Resolve || _state == EventState.Fail)
            {
                return;
            }

            _state = nextState;

            Context?.RuntimeBridge?.PublishEventStateChanged(
                InstanceId,
                Id,
                RoomId,
                nextState);

            if (nextState == EventState.Resolve)
            {
                OnFinished?.Invoke(this, true);
            }
            else if (nextState == EventState.Fail)
            {
                OnFinished?.Invoke(this, false);
            }
        }
    }
}
