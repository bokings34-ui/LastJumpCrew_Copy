using SM;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using Unity.Netcode;
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
        [SerializeField, Min(1f)] private float serverInteractionDistance = 4f;

        private bool isEventInProgress;

        public string InteractionPrompt => interactionPrompt;
        public EventId ConfiguredEventId => eventId;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return CanInteractCore();
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
            return CanInteractCore();
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

            if (IsNetworkSessionActive(out var networkCoordinator))
            {
                if (networkCoordinator == null || !networkCoordinator.IsSpawned)
                {
                    Debug.LogError(
                        $"PHS_EVENT_TERMINAL_NETWORK_REJECTED reason=coordinator_missing event={eventId}",
                        this);
                    return;
                }

                if (!networkCoordinator.RequestEventFromTerminal(eventId))
                {
                    Debug.LogWarning(
                        $"PHS_EVENT_TERMINAL_NETWORK_REJECTED reason=request_not_accepted event={eventId}",
                        this);
                }

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

            var eventManager = EventManager.Instance;
            var finishedDuringSpawn = false;
            var spawnCallCompleted = false;

            void HandleSpawnFinished(EventBase finishedEvent, bool isSuccess)
            {
                if (!spawnCallCompleted)
                {
                    finishedDuringSpawn = true;
                }

                HandleEventFinished(finishedEvent, isSuccess);
            }

            isEventInProgress = true;
            var spawnAccepted = eventManager.TrySpawnEvent(
                eventId,
                room,
                HandleSpawnFinished,
                out _);
            spawnCallCompleted = true;

            if (!spawnAccepted)
            {
                isEventInProgress = false;
                Debug.LogError($"[{nameof(ShipAccidentEventTerminal)}] 함선 사고 생성 실패: {eventId}", this);
                return;
            }

            if (finishedDuringSpawn)
            {
                if (eventManager.IsActive(eventId))
                {
                    Debug.LogError(
                        $"[{nameof(ShipAccidentEventTerminal)}] 즉시 종료된 {eventId} 이벤트가 EventManager에 활성 상태로 남았습니다. " +
                        "EventManager의 이벤트 등록 순서를 확인해야 합니다.",
                        this);
                }
                else
                {
                    Debug.Log($"[{nameof(ShipAccidentEventTerminal)}] 함선 사고가 즉시 종료되었습니다: {eventId}", this);
                }

                return;
            }

            if (!eventManager.IsActive(eventId))
            {
                isEventInProgress = false;
                Debug.LogError($"[{nameof(ShipAccidentEventTerminal)}] 함선 사고 생성 실패: {eventId}", this);
                return;
            }

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

        public bool IsServerRequestValid(EventId requestedEventId, Vector3 playerPosition)
        {
            if (!isActiveAndEnabled
                || requestedEventId != eventId
                || !IsSupportedEvent(requestedEventId))
            {
                return false;
            }

            return (transform.position - playerPosition).sqrMagnitude
                <= serverInteractionDistance * serverInteractionDistance;
        }

        private bool CanInteractCore()
        {
            if (!IsSupportedEvent(eventId))
            {
                return false;
            }

            if (IsNetworkSessionActive(out var networkCoordinator))
            {
                return networkCoordinator != null
                    && networkCoordinator.IsSpawned
                    && !networkCoordinator.IsEventActive(eventId);
            }

            return !isEventInProgress
                && EventManager.Instance != null
                && RoomRegistry.Instance != null
                && !EventManager.Instance.IsActive(eventId);
        }

        private static bool IsNetworkSessionActive(out NetworkEventCoordinator networkCoordinator)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                networkCoordinator = null;
                return false;
            }

            networkCoordinator = NetworkEventCoordinator.Instance;
            return true;
        }

        private static bool IsSupportedEvent(EventId value)
        {
            return value == EventId.Fire
                || value == EventId.EnemySpawn
                || value == EventId.OxygenLeak;
        }
    }
}
