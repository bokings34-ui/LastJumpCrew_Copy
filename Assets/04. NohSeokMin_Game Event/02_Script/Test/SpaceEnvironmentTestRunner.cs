using UnityEngine;
using UnityEngine.InputSystem;
using LastJumpCrew.Common;

namespace SM
{
    public class SpaceEnvironmentTestRunner : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current == null) return;

            // [ Q ] 키 → EventScheduler 시작
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                ZoneEventScheduler.Instance.SetCurrentZone(ZoneType.PatrolZone);
                EventScheduler.Instance.StartScheduler();
                ZoneEventScheduler.Instance.StartScheduler();

                Debug.Log($"<color=lime>[Test]</color> 이벤트 스케줄러 시작");
            }

            // [ W ] 키 → Fire 수동 발생
            if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                var room = RoomRegistry.Instance.GetRandomRoom();
                EventManager.Instance.SpawnEvent(EventId.Fire, room);
                Debug.Log("W → Fire 수동 발생");
            }

            // [ E ] 키 → EnemySpawn 수동 발생
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                var room = RoomRegistry.Instance.GetRandomRoom();
                EventManager.Instance.SpawnEvent(EventId.EnemySpawn, room);
                Debug.Log("E → EnemySpawn 수동 발생");
            }

            // [ r ] 키 → OxygenLeak 수동 발생
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                var room = RoomRegistry.Instance.GetRandomRoom();
                EventManager.Instance.SpawnEvent(EventId.OxygenLeak, room);
                Debug.Log("T → OxygenLeak 수동 발생");
            }

            // [ 1 ] 키 → 정상 속도
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                Debug.Log("[TimeScale] 1배속 : 정상 속도로 복구합니다.");
            }

            // [ 2 ] 키 → 2배속 (살짝 빠르게)
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                Time.timeScale = 2f;
                Debug.Log("[TimeScale] 2배속 : 시간을 빠르게 흘려보냅니다.");
            }

            // [ 3 ] 키 → 4배속 (초고속 스킵!)
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                Time.timeScale = 4f;
                Debug.Log("[TimeScale] 4배속 : 답답한 대기 시간을 초고속으로 스킵합니다!");
            }

            // [ 0 ] 키 → 일시 정지 (로그 확인 및 디버깅용)
            if (Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                Time.timeScale = 0f;
                Debug.Log("⏸ [TimeScale] 0배속 : 게임을 일시 정지합니다.");
            }
        }
    }
}