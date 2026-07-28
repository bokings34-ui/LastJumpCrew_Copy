using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "MicDestroyEventData", menuName = "SM/EventData/MicDestroy")]
    public class MicDestroyEventDataSO : EventDataSO
    {
        [Header("통신 장비 마비 시간 설정")]
        public float disableDuration = 15f;
    }
}