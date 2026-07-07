using UnityEngine;

namespace SM
{
    public abstract class EventDataSO : ScriptableObject 
    {
        [Header("이벤트 정보")]
        [SerializeField] private EventType _type;
        [SerializeField] private EventId _id;
        [SerializeField] private string _eventName;
        [TextArea][SerializeField] private string _description;

        public EventType Type { get { return _type; } }
        public EventId Id { get { return _id; } }
        public string EventName { get { return _eventName; } }
        public string Description { get { return _description; } }
    }
}