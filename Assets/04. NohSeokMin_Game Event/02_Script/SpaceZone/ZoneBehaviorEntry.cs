using UnityEngine;

namespace SM
{
    [System.Serializable]
    public class ZoneBehaviorEntry
    {
        public ZoneType zone;

        [Tooltip("NebulaZone은 사용 안 함 (미니맵 신호만 발행)")]
        public EventId eventId;

        [Header("사고 발생 시도 주기 (초)")]
        public float intervalMin = 40f;
        public float intervalMax = 70f;
    }
}
