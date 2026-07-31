using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    [System.Serializable]
    public class ZoneBehaviorEntry
    {
        public ZoneType zone;

        [Tooltip("이 Zone에서 발생 가능한 외부 경고 이벤트들 (NebulaZone은 비워두고 미니맵 신호만 사용)")]
        public List<EventId> eventIds = new List<EventId>();
    }
}