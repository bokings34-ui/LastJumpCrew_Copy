using SM;
using UnityEngine;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ShipAccidentEventTerminal : MonoBehaviour, IInteractable, CommonInteraction.IInteractable
    {
        [Header("호출할 함선 사고")]
        [SerializeField] private EventId eventId = EventId.Fire;

        [Header("상호작용 안내")]
        [SerializeField] private string interactionPrompt = "함선 사고 발생시키기";

        private bool isEventInProgress;

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return IsSupportedEvent(eventId)
                && !isEventInProgress
                && EventManager.Instance != null
                && RoomRegistry.Instance != null
                && !EventManager.Instance.IsActive(eventId);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            TriggerEvent();
        }

        bool CommonInteraction.IInteractable.CanInteract(CommonInteraction.IItemHolder itemHolder)
        {
            return IsSupportedEvent(eventId)
                && !isEventInProgress
                && EventManager.Instance != null
                && RoomRegistry.Instance != null
                && !EventManager.Instance.IsActive(eventId);
        }

        void CommonInteraction.IInteractable.Interact(CommonInteraction.IItemHolder itemHolder)
        {
            if (!((CommonInteraction.IInteractable)this).CanInteract(itemHolder))
            {
                return;
            }

            TriggerEvent();
        }

        private void TriggerEvent()
        {
            if (!IsSupportedEvent(eventId))
            {
                Debug.LogError($"[{nameof(ShipAccidentEventTerminal)}] 구현되지 않은 사고 이벤트입니다: {eventId}", this);
                return;
            }

            if (EventManager.Instance == null || RoomRegistry.Instance == null)
            {
                Debug.LogError($"[{nameof(ShipAccidentEventTerminal)}] EventManager 또는 RoomRegistry가 씬에 없습니다.", this);
                return;
            }

            var room = RoomRegistry.Instance.GetRandomRoom();
            if (room == null)
            {
                Debug.LogWarning($"[{nameof(ShipAccidentEventTerminal)}] 등록된 함선 방이 없어 {eventId} 이벤트를 호출할 수 없습니다.", this);
                return;
            }

            if (EventManager.Instance.IsActive(eventId))
            {
                Debug.LogWarning($"[{nameof(ShipAccidentEventTerminal)}] 이미 진행 중인 사고입니다: {eventId}", this);
                return;
            }

            isEventInProgress = true;
            EventManager.Instance.SpawnEvent(eventId, room, HandleEventFinished);
            Debug.Log($"[{nameof(ShipAccidentEventTerminal)}] 함선 사고 시작: {eventId}", this);
        }

        private void HandleEventFinished(EventBase finishedEvent, bool isSuccess)
        {
            if (finishedEvent.Id != eventId)
            {
                return;
            }

            isEventInProgress = false;
            Debug.Log($"[{nameof(ShipAccidentEventTerminal)}] 함선 사고 종료: {eventId}, success={isSuccess}", this);
        }

        private static bool IsSupportedEvent(EventId value)
        {
            return value == EventId.Fire
                || value == EventId.EnemySpawn
                || value == EventId.OxygenLeak;
        }
    }
}
