namespace SM
{
    public class EventContext
    {
        public ulong InstanceId { get; private set; }
        public IRoom Room { get; private set; }
        public IEventSpawner Spawner { get; private set; }
        public IEventRuntimeBridge RuntimeBridge { get; private set; }

        public EventContext(IRoom room, IEventSpawner spawner)
            : this(0UL, room, spawner, null)
        {
        }

        public EventContext(
            ulong instanceId,
            IRoom room,
            IEventSpawner spawner,
            IEventRuntimeBridge runtimeBridge)
        {
            InstanceId = instanceId;
            Room = room;
            Spawner = spawner;
            RuntimeBridge = runtimeBridge;
        }
    }
}
