using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "FireEventData", menuName = "SM/EventData/Fire")]
    public class FireEventDataSO : EventDataSO
    {
        [Header("화재 밸런스 설정")]
        public float levelUpInterval = 10f;
        [Min(1)] public int maxConcurrentFires = 4;
        public float damagePerSecond = 5f;
        public float maxRepairProgress = 20f;
    }
}
