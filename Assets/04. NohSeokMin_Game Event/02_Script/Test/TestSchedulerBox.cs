using UnityEngine;
using UnityEngine.InputSystem;
using LastJumpCrew.Common;

namespace SM
{
    public class TestSchedulerBox : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject uiPanel;

        public string InteractionPrompt { get { return "이벤트 테스트 메뉴 열기"; } }

        private void Start()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return true;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (uiPanel != null)
            {
                ToggleUIPanel();
            }
        }

        private void Update()
        {
            if (uiPanel == null) return;
            if (Keyboard.current == null) return;

            // [ 5 ] 키 -> UI 껐다 키기
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ToggleUIPanel();
            }

            // [ 6 ] 키 → 이벤트 스케줄러 시작(30초마다 한번씩 발생)
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                StartAll();
            }

            // [ 7 ] 키 → 이벤트 스케줄러 강제 종료
            if (Keyboard.current.digit7Key.wasPressedThisFrame)
            {
                StopAll();
            }

            // [ 8 ] 키 → 화재 이벤트 강제 발생
            if (Keyboard.current.digit8Key.wasPressedThisFrame)
            {
                SpawnForceEvent(EventId.Fire, "화재");
            }

            // [ 9 ] 키 → 적 침투 이벤트 강제 발생
            if (Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                SpawnForceEvent(EventId.EnemySpawn, "적 침투");
            }

            // [ 0 ] 키 → 산소 유출 이벤트 강제 발생
            if (Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                SpawnForceEvent(EventId.OxygenLeak, "산소 유출");
            }
        }

        private void StartAll()
        {
            EventScheduler.Instance.StartScheduler();
            ZoneEventScheduler.Instance.StartScheduler();
            Debug.Log("<color=lime>[함선 사고 이벤트 테스트]</color> 이벤트/존 스케줄러 시작.");
        }

        private void StopAll()
        {
            EventScheduler.Instance.ForceClearAll();
            ZoneEventScheduler.Instance.StopScheduler();
            Debug.Log("<color=lime>[함선 사고 이벤트 테스트]</color> 이벤트/존 스케줄러 종료 및 강제 클리어.");
        }

        private void ToggleUIPanel()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(!uiPanel.activeSelf);
            }
        }

        private void SpawnForceEvent(EventId eventId, string eventName)
        {
            var room = RoomRegistry.Instance.GetRandomRoom();

            if (room == null)
            {
                Debug.LogWarning($"<color=red>[함선 사고 이벤트 테스트]</color> 등록된 방이 없어 {eventName} 이벤트를 생성할 수 없습니다.");
                return;
            }

            EventManager.Instance.SpawnEvent(eventId, room);
            Debug.Log($"<color=lime>[함선 사고 이벤트 테스트]</color> {eventName}({eventId}) 강제 발생! (위치: {room.RoomId})");
        }
    }
}