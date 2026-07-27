namespace SM
{
    public class EmpAttackEvent : ExternalEvent
    {
        protected override EventId GetNextEventId()
        {
            return EventId.Fire;
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}
