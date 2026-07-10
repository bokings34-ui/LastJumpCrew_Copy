namespace SM
{
    public class MeteorAttackEvent : ExternalEvent
    {
        protected override EventId GetNextEventId()
        {
            return EventId.OxygenLeak;
        }
        
        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}