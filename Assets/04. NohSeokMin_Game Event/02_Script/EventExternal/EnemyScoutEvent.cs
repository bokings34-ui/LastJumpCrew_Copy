namespace SM
{
    public class EnemyScoutEvent : ExternalEvent
    {
        protected override EventId GetNextEventId()
        {
            return EventId.EnemySpawn;
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}