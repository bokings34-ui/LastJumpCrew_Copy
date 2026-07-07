using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "EventRegistry", menuName = "SM/EventRegistry")]
    public class EventRegistrySO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public EventId id;
            public EventDataSO data;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<EventId, EventDataSO> _eventDataMap;

        public EventDataSO GetData(EventId id)
        {
            if (_eventDataMap == null)
            {
                _eventDataMap = new Dictionary<EventId, EventDataSO>();

                foreach (var entry in entries)
                    _eventDataMap[entry.id] = entry.data;
            }

            _eventDataMap.TryGetValue(id, out var data);
            return data;
        }
    }
}