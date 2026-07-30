using UnityEngine;
using UnityEngine.Rendering;

namespace SM
{
    public class PowerOffLightController : MonoBehaviour
    {
        [Header("전력 차단 시 활성화할 어둠 Volume")]
        [SerializeField] private Volume powerOffVolume;

        [Header("전환 속도")]
        [SerializeField] private float transitionSpeed = 3f;

        private bool _isPowerOff;
        private float _targetWeight;

        private void Update()
        {
            var currentEvent = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;
            bool shouldBeOff = currentEvent != null && currentEvent.IsPowerOffActive;

            if (shouldBeOff != _isPowerOff)
            {
                _isPowerOff = shouldBeOff;
                _targetWeight = shouldBeOff ? 1f : 0f;

                if (shouldBeOff)
                    Debug.Log("<color=yellow>[PowerOffLightController]</color> 화면 어둡게 전환 시작.");
                else
                    Debug.Log("<color=lime>[PowerOffLightController]</color> 화면 밝기 복구 시작.");
            }

            if (powerOffVolume != null)
            {
                powerOffVolume.weight = Mathf.MoveTowards(powerOffVolume.weight, _targetWeight, transitionSpeed * Time.deltaTime);
            }
        }
    }
}