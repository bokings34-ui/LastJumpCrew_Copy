namespace SM
{
    public class EmpAttackEvent : ExternalEvent
    {
        protected override EventId GetNextEventId()
        {
            return Context?.RuntimeBridge == null
                ? EventId.Fire
                : EventId.PowerOff;
        }

        public override void OnFail()
        {
            if (Context?.RuntimeBridge != null)
            {
                ChangeState(EventState.Fail);
                return;
            }

            base.OnFail();
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}
