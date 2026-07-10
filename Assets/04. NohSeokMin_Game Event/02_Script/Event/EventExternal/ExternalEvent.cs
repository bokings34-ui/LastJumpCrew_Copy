using LastJumpCrew.Common;

namespace SM
{
    public abstract class ExternalEvent : EventBase, IMiniGameTarget
    {
        protected IRoom TargetRoom { get { return Context?.Room; } }
        protected IEventSpawner Spawner { get { return Context?.Spawner; } }

        public string MiniGameTargetId { get { return Id.ToString(); } }

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
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
            Spawner?.SpawnEvent(GetNextEventId(), TargetRoom);
        }

        protected abstract EventId GetNextEventId();
    }
}