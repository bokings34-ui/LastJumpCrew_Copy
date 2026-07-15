using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "OxygenLeakEventData", menuName = "SM/EventData/OxygenLeak")]
    public class OxygenLeakEventDataSO : EventDataSO
    {
        [Header("흡입 범위 설정")]
        public float outerPullRadius = 5f;
        public float innerDamageRadius = 1.5f;
        public float pullSpeed = 2f;

        [Header("피해 설정")]
        public int centerDamage = 500;
        public float damageTickInterval = 1f;

        [Header("수리 설정")]
        public float maxRepairProgress = 10f;
    }
}