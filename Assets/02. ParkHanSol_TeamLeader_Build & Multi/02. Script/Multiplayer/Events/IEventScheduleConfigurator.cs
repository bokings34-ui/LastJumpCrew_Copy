using System;
using SM;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public enum PHSNetworkEventChannel : byte
    {
        LegacyMixed = 0,
        ExternalThreat = 1,
        LegacyInternal = 2
    }

    [Serializable]
    public struct WeightedEventScheduleEntry
    {
        public EventId eventId;
        public float weight;

        public WeightedEventScheduleEntry(EventId eventId, float weight)
        {
            this.eventId = eventId;
            this.weight = weight;
        }
    }

    public interface IEventScheduleConfigurator
    {
        bool TryConfigureServer(
            PHSNetworkEventChannel channel,
            WeightedEventScheduleEntry[] entries,
            float intervalMinSeconds,
            float intervalMaxSeconds,
            int maximumActiveEvents,
            out string reason);

        bool TryStartServer(out string reason);

        bool TryStopServer(out string reason);
    }
}
