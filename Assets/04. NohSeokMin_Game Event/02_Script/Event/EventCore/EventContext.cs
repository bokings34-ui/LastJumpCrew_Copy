namespace SM
{
    public class EventContext
    {
        public ulong InstanceId { get; private set; }
        public IRoom Room { get; private set; }
        public IEventSpawner Spawner { get; private set; }
        public IEventRuntimeBridge RuntimeBridge { get; private set; }

        public EventContext(
            ulong instanceId,
            IRoom room,
            IEventSpawner spawner,
            IEventRuntimeBridge runtimeBridge = null)
        {
            InstanceId = instanceId;
            Room = room;
            Spawner = spawner;
            RuntimeBridge = runtimeBridge;
        }
    }
}
