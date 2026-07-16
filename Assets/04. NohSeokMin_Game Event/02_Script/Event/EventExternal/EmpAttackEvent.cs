namespace SM
{
    public class EmpAttackEvent : ExternalEvent
    {
        protected override EventId GetNextEventId()
        {
            // TODO :: 원래 PowerOff 인데 아직 미구현이라 Fire로 임시 변경
            // return EventId.PowerOff;
            return EventId.Fire;
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}