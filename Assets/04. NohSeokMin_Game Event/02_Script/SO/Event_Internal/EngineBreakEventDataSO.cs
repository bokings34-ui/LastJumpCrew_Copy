using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "EngineBreakEventData", menuName = "SM/EventData/EngineBreak")]
    public class EngineBreakEventDataSO : EventDataSO
    {
        [Header("총 수리량 설정")]
        public float maxRepairProgress = 10f;

        [Header("연료 손실 설정")]
        public float fuelLossInterval = 5f;
    }
}