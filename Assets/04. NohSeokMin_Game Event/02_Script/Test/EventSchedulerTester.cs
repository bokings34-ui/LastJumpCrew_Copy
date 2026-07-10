using UnityEngine;
using UnityEngine.InputSystem;

namespace SM
{
    public class EventSchedulerTester : MonoBehaviour
    {
        [Header("시간 배속 설정")]
        [Range(0, 10)][SerializeField] private float timeScale = 5f;

        private void Update()
        {
            if (Keyboard.current == null) return;

            Time.timeScale = timeScale;

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                StartTest();
            }
            if (Keyboard.current.sKey.wasPressedThisFrame)
            {
                StopTest();
            }
        }

        public void StartTest()
        {
            if (EventScheduler.Instance != null)
            {
                Debug.Log("<color=lime>[TEST]</color> 이벤트 스케줄러 가동 시작!");
                EventScheduler.Instance.StartScheduler();
            }
            else
            {
                Debug.LogError("씬에 EventScheduler가 존재하지 않습니다!");
            }
        }

        public void StopTest()
        {
            if (EventScheduler.Instance != null)
            {
                Debug.Log("<color=lime>[TEST]</color> 이벤트 스케줄러 정지");
                EventScheduler.Instance.StopScheduler();
            }
        }
    }
}