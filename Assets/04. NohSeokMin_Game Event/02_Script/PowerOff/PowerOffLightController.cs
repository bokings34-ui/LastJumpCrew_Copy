using UnityEngine;

namespace SM
{
    public class PowerOffLightController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float lowIntensity = 0.1f;
        [SerializeField] private float transitionSpeed = 1f;

        private float _normalIntensity;
        private float _targetIntensity;

        private void Awake()
        {
            _normalIntensity = RenderSettings.ambientIntensity; // 시작 시점 값을 그대로 기억
            _targetIntensity = _normalIntensity;
        }

        private void Update()
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;
            bool shouldBeOff = evt != null && evt.IsPowerOffActive;

            _targetIntensity = shouldBeOff ? lowIntensity : _normalIntensity;

            RenderSettings.ambientIntensity = Mathf.MoveTowards(
                RenderSettings.ambientIntensity,
                _targetIntensity,
                transitionSpeed * Time.deltaTime);
        }

        private void OnDisable()
        {
            RenderSettings.ambientIntensity = _normalIntensity; // 비활성화 시 강제 복구
        }
    }
}