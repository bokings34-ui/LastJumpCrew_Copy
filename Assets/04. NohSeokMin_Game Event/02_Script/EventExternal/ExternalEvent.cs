namespace SM
{
    public abstract class ExternalEvent : EventBase, IMinigameResult
    {
        protected IRoom TargetRoom { get { return Context?.Room; } }
        protected IEventSpawner Spawner { get { return Context?.Spawner; } }

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
        }

        // 미니게임 결과 확정 시 호출
        public void MinigameResult(bool success)
        {
            if (success)
            {
                OnResolve();
            }
            else 
            {
                OnFail();
            }
        }

        public override void OnFail()
        {
            ChangeState(EventState.Fail);
            Spawner?.SpawnEvent(GetNextEventId(), TargetRoom);
        }

        protected abstract EventId GetNextEventId();
    }
}