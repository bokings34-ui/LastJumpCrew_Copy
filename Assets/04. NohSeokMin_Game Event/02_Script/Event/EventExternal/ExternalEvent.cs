using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    public abstract class ExternalEvent : EventBase, IMiniGameTarget
    {
        private const float MiniGameTimeLimit = 30f;

        protected IRoom TargetRoom { get { return Context?.Room; } }
        protected IEventSpawner Spawner { get { return Context?.Spawner; } }

        private float _elapsedTime;

        public string MiniGameTargetId { get { return Id.ToString(); } }

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _elapsedTime = 0f;
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _elapsedTime += deltaTime;

            if (_elapsedTime >= MiniGameTimeLimit)
            {
                Debug.Log($"<color=lime>[{Id}]</color> 제한 시간({MiniGameTimeLimit}초) 초과, 실패.");
                OnFail();
            }
        }

        public void OnMiniGameSucceeded()
        {
            OnResolve();
        }

        public void OnMiniGameFailed()
        {
            OnFail();
        }

        public override void OnFail()
        {
            ChangeState(EventState.Fail);
            if (Context?.RuntimeBridge == null)
            {
                Spawner?.SpawnEvent(GetNextEventId(), TargetRoom);
            }
        }

        protected abstract EventId GetNextEventId();
    }
}
