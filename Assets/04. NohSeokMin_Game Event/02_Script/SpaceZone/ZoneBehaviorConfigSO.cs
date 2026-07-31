using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "ZoneBehaviorConfig", menuName = "SM/ZoneBehaviorConfig")]
    public class ZoneBehaviorConfigSO : ScriptableObject
    {
        [SerializeField] private List<ZoneBehaviorEntry> entries = new List<ZoneBehaviorEntry>();

        public List<EventId> GetEventIds(ZoneType zone)
        {
            foreach (var entry in entries)
            {
                if (entry.zone == zone) return entry.eventIds;
            }
            return null;
        }
    }
}