using System;
using System.Collections.Generic;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public static class EventManagerRuntimeAdapter
    {
        private sealed class RuntimeState
        {
            public IEventRuntimeBridge Bridge;
            public readonly Dictionary<ulong, EventId> EventIdsByInstance = new();
            public readonly Dictionary<EventId, ulong> InstanceIdsByEvent = new();
        }

        private sealed class RuntimeSpawner : IEventSpawner
        {
            private readonly EventManager manager;

            public RuntimeSpawner(EventManager manager)
            {
                this.manager = manager;
            }

            public void SpawnEvent(
                EventId id,
                IRoom targetRoom,
                Action<EventBase, bool> onFinished = null)
            {
                EventManagerRuntimeAdapter.TrySpawnEvent(
                    manager,
                    id,
                    targetRoom,
                    onFinished,
                    out _);
            }
        }

        private static readonly Dictionary<EventManager, RuntimeState> States = new();
        private static ulong nextOfflineInstanceId;

        public static bool ConfigureRuntimeBridge(
            this EventManager manager,
            IEventRuntimeBridge bridge)
        {
            if (manager == null || bridge == null)
            {
                Debug.LogError("PHS_EVENT_ADAPTER_CONFIG_FAILED reason=missing_reference");
                return false;
            }

            if (States.TryGetValue(manager, out var existing)
                && existing.Bridge != null
                && !ReferenceEquals(existing.Bridge, bridge))
            {
                Debug.LogError("PHS_EVENT_ADAPTER_CONFIG_FAILED reason=bridge_conflict", manager);
                return false;
            }

            if (existing == null)
            {
                existing = new RuntimeState();
                States.Add(manager, existing);
            }

            existing.Bridge = bridge;
            return true;
        }

        public static void ClearRuntimeBridge(
            this EventManager manager,
            IEventRuntimeBridge bridge)
        {
            if (manager == null
                || !States.TryGetValue(manager, out var state)
                || !ReferenceEquals(state.Bridge, bridge))
            {
                return;
            }

            States.Remove(manager);
        }

        public static bool HasRuntimeBridge(this EventManager manager)
        {
            return manager != null
                && States.TryGetValue(manager, out var state)
                && state.Bridge != null;
        }

        public static bool IsRuntimeAuthority(this EventManager manager)
        {
            return manager != null
                && (!States.TryGetValue(manager, out var state)
                    || state.Bridge == null
                    || state.Bridge.IsAuthoritative);
        }

        public static bool IsInstanceActive(this EventManager manager, ulong instanceId)
        {
            return manager != null
                && States.TryGetValue(manager, out var state)
                && state.EventIdsByInstance.TryGetValue(instanceId, out var eventId)
                && manager.IsActive(eventId);
        }

        public static bool TrySpawnEvent(
            this EventManager manager,
            EventId eventId,
            IRoom room,
            out ulong instanceId)
        {
            return TrySpawnEvent(manager, eventId, room, null, out instanceId);
        }

        public static bool TrySpawnEvent(
            this EventManager manager,
            EventId eventId,
            IRoom room,
            Action<EventBase, bool> onFinished,
            out ulong instanceId)
        {
            instanceId = 0UL;
            if (manager == null || room == null)
            {
                Debug.LogError($"PHS_EVENT_ADAPTER_SPAWN_FAILED reason=missing_reference event={eventId}");
                return false;
            }

            if (manager.IsActive(eventId))
            {
                Debug.LogWarning($"PHS_EVENT_ADAPTER_SPAWN_REJECTED reason=already_active event={eventId}", manager);
                return false;
            }

            States.TryGetValue(manager, out var state);
            var bridge = state?.Bridge;
            if (bridge != null && !bridge.IsAuthoritative)
            {
                Debug.LogError($"PHS_EVENT_ADAPTER_SPAWN_FAILED reason=not_authority event={eventId}", manager);
                return false;
            }

            instanceId = bridge != null
                ? bridge.AllocateEventInstanceId()
                : AllocateOfflineInstanceId();
            var allocatedInstanceId = instanceId;

            state ??= GetOrCreateState(manager);
            state.EventIdsByInstance[allocatedInstanceId] = eventId;
            state.InstanceIdsByEvent[eventId] = allocatedInstanceId;

            var spawnCallCompleted = false;
            var finishedDuringSpawn = false;
            EventBase immediateFinishedEvent = null;
            var immediateSuccess = false;

            void HandleFinished(EventBase finishedEvent, bool success)
            {
                onFinished?.Invoke(finishedEvent, success);

                if (!spawnCallCompleted)
                {
                    finishedDuringSpawn = true;
                    immediateFinishedEvent = finishedEvent;
                    immediateSuccess = success;
                    return;
                }

                PublishFinished(state, allocatedInstanceId, room.RoomId, finishedEvent, success);
            }

            bridge?.PublishEventStarted(
                allocatedInstanceId,
                eventId,
                room.RoomId,
                EventState.Ready);

            var eventContext = new EventContext(
                allocatedInstanceId,
                room,
                new RuntimeSpawner(manager),
                bridge);
            var spawnAccepted = manager.SpawnEventWithContext(
                eventId,
                room,
                eventContext,
                HandleFinished);
            spawnCallCompleted = true;

            if (!spawnAccepted)
            {
                bridge?.PublishEventFinished(
                    allocatedInstanceId,
                    eventId,
                    room.RoomId,
                    EventState.Fail,
                    false);
                RemoveInstance(state, allocatedInstanceId, eventId);
                Debug.LogError(
                    $"PHS_EVENT_ADAPTER_SPAWN_FAILED reason=manager_rejected event={eventId}",
                    manager);
                return false;
            }

            if (finishedDuringSpawn)
            {
                PublishFinished(state, allocatedInstanceId, room.RoomId, immediateFinishedEvent, immediateSuccess);
                Debug.LogError(
                    $"PHS_EVENT_ADAPTER_SPAWN_FAILED reason=finished_during_spawn event={eventId} instance={allocatedInstanceId}",
                    manager);
                return false;
            }

            if (!manager.IsActive(eventId))
            {
                RemoveInstance(state, allocatedInstanceId, eventId);
                Debug.LogError(
                    $"PHS_EVENT_ADAPTER_SPAWN_FAILED reason=manager_rejected event={eventId}",
                    manager);
                return false;
            }

            return true;
        }

        private static RuntimeState GetOrCreateState(EventManager manager)
        {
            if (States.TryGetValue(manager, out var state))
            {
                return state;
            }

            state = new RuntimeState();
            States.Add(manager, state);
            return state;
        }

        private static ulong AllocateOfflineInstanceId()
        {
            nextOfflineInstanceId++;
            if (nextOfflineInstanceId == 0UL)
            {
                nextOfflineInstanceId++;
            }

            return nextOfflineInstanceId;
        }

        private static void PublishFinished(
            RuntimeState state,
            ulong instanceId,
            string roomId,
            EventBase finishedEvent,
            bool success)
        {
            if (finishedEvent == null)
            {
                Debug.LogError($"PHS_EVENT_ADAPTER_FINISH_FAILED reason=event_missing instance={instanceId}");
                return;
            }

            state.Bridge?.PublishEventFinished(
                instanceId,
                finishedEvent.Id,
                roomId,
                finishedEvent.State,
                success);
            RemoveInstance(state, instanceId, finishedEvent.Id);
        }

        private static void RemoveInstance(RuntimeState state, ulong instanceId, EventId eventId)
        {
            state.EventIdsByInstance.Remove(instanceId);
            if (state.InstanceIdsByEvent.TryGetValue(eventId, out var mapped)
                && mapped == instanceId)
            {
                state.InstanceIdsByEvent.Remove(eventId);
            }
        }
    }
}
