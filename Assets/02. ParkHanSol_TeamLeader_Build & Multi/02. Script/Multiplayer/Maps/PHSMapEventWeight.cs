using System;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [Serializable]
    public sealed class PHSMapEventWeight
    {
        [SerializeField] private EventId eventId = EventId.Fire;
        [SerializeField, Min(1)] private int weight = 1;

        public EventId EventId => eventId;
        public int Weight => weight;

        public bool TryValidate(out string reason)
        {
            if (!Enum.IsDefined(typeof(EventId), eventId))
            {
                reason = $"event_id_invalid:{(int)eventId}";
                return false;
            }

            if (weight <= 0)
            {
                reason = $"event_weight_not_positive:event={eventId}:weight={weight}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
