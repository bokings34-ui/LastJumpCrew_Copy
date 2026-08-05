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

            // ---- 내부 사고 스케줄러 ----
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartTest();
            }
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                StopTest();
            }

            // ---- 내부 사고 6종 개별 수동 발생 (숫자키 1~6) ----
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.Fire);
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.EnemySpawn);
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.OxygenLeak);
            }
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.PowerOff);
            }
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.EngineBreak);
            }
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                TrySpawn(EventId.MicDestroy);
            }

            // ---- 전체 강제 종료 ----
            if (Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                ForceClearAll();
            }

            // ---- Zone 선택 (7,8,9,0은 이미 쓰였으니 F1~F4로) ----
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                SetZone(ZoneType.PatrolZone);
            }
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetZone(ZoneType.MeteorZone);
            }
            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetZone(ZoneType.NebulaZone);
            }
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                SetZone(ZoneType.PlanetZone);
            }

            // ---- Zone 스케줄러 시작/정지 ----
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                StartZoneTest();
            }
            if (Keyboard.current.xKey.wasPressedThisFrame)
            {
                StopZoneTest();
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

        private void TrySpawn(EventId eventId)
        {
            if (EventScheduler.Instance == null)
            {
                Debug.LogError("씬에 EventScheduler가 존재하지 않습니다!");
                return;
            }

            EventScheduler.Instance.TrySpawnEvent(eventId, null);
            Debug.Log($"<color=lime>[TEST]</color> {eventId} 수동 발생 요청");
        }

        private void ForceClearAll()
        {
            if (EventScheduler.Instance != null)
            {
                Debug.Log("<color=orange>[TEST]</color> 전체 강제 클리어");
                EventScheduler.Instance.ForceClearAll();
            }
        }

        // ---- Zone 스케줄러 테스트 ----

        private void SetZone(ZoneType zone)
        {
            if (ZoneEventScheduler.Instance == null)
            {
                Debug.LogError("씬에 ZoneEventScheduler가 존재하지 않습니다!");
                return;
            }

            ZoneEventScheduler.Instance.SetCurrentZone(zone);
            Debug.Log($"<color=cyan>[TEST]</color> 현재 Zone: {zone}");
        }

        private void StartZoneTest()
        {
            if (ZoneEventScheduler.Instance != null)
            {
                Debug.Log("<color=cyan>[TEST]</color> Zone 이벤트 스케줄러 시작!");
                ZoneEventScheduler.Instance.StartScheduler();
            }
            else
            {
                Debug.LogError("씬에 ZoneEventScheduler가 존재하지 않습니다!");
            }
        }

        private void StopZoneTest()
        {
            if (ZoneEventScheduler.Instance != null)
            {
                Debug.Log("<color=cyan>[TEST]</color> Zone 이벤트 스케줄러 정지");
                ZoneEventScheduler.Instance.StopScheduler();
            }
        }
    }
}