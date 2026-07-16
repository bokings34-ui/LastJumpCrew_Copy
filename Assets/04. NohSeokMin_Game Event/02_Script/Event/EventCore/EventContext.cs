namespace SM
{
    public class EventContext
    {
        public IRoom Room { get; private set; }
        public IEventSpawner Spawner { get; private set; }

        public EventContext(IRoom room, IEventSpawner spawner)
        {
            Room = room;
            Spawner = spawner;
        }
    }
}