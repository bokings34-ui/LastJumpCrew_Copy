using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EventManager : MonoSingleton<EventManager>
    {
        [Header("이벤트 데이터 레지스트리")]
        [SerializeField] private EventRegistrySO registry;

        private readonly Dictionary<EventId, EventBase> _activeEvents = new Dictionary<EventId, EventBase>();
        private readonly List<EventBase> _eventsToTickCache = new List<EventBase>();

        private void Update()
        {
            //foreach (var evt in _activeEvents.Values)
            //{
            //    evt.OnTick(Time.deltaTime);
            //}

            _eventsToTickCache.Clear();

            foreach (var evt in _activeEvents.Values)
            {
                _eventsToTickCache.Add(evt);
            }

            for (int i = 0; i < _eventsToTickCache.Count; i++)
            {
                var evt = _eventsToTickCache[i];

                if (_activeEvents.ContainsKey(evt.Id))
                {
                    evt.OnTick(Time.deltaTime);
                }
            }
        }

        public EventBase GetActiveEvent(EventId id)
        {
            _activeEvents.TryGetValue(id, out var evt);
            return evt;
        }

        public bool IsActive(EventId id)
        {
            return _activeEvents.ContainsKey(id);
        }

        public void SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null)
        {
            SpawnEventWithContext(
                id,
                targetRoom,
                new EventContext(targetRoom, EventScheduler.Instance),
                onFinished);
        }

        public bool SpawnEventWithContext(
            EventId id,
            IRoom targetRoom,
            EventContext context,
            Action<EventBase, bool> onFinished = null)
        {
            if (_activeEvents.ContainsKey(id))
            {
                Debug.Log($"<color=lime>[EventManager]</color> {id}는 이미 진행 중입니다.");
                return false;
            }

            if (registry == null || targetRoom == null || context == null)
            {
                Debug.LogError($"PHS_EVENT_SPAWN_FAILED reason=missing_reference event={id}", this);
                return false;
            }

            var data = registry.GetData(id);

            if (data == null)
            {
                Debug.Log($"<color=lime>[EventManager]</color> {id}에 대한 EventDataSO가 Registry에 없습니다.");
                return false;
            }

            var evt = EventFactory.Create(id);
            if (evt == null)
            {
                Debug.LogError($"PHS_EVENT_SPAWN_FAILED reason=factory event={id}", this);
                return false;
            }

            evt.OnFinished += HandleEventFinished;
            if (onFinished != null) evt.OnFinished += onFinished;

            evt.Initialize(data, context);
            _activeEvents[id] = evt;
            try
            {
                evt.OnTrigger();
                return true;
            }
            catch (Exception exception)
            {
                evt.OnFinished -= HandleEventFinished;
                if (onFinished != null) evt.OnFinished -= onFinished;
                _activeEvents.Remove(id);
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void HandleEventFinished(EventBase evt, bool success)
        {
            evt.OnFinished -= HandleEventFinished;
            _activeEvents.Remove(evt.Id);

            Debug.Log($"<color=lime>[EventManager]</color> {evt.Id} 종료 (성공 여부 : {success})");
        }

        public void ApplyRepairTo(EventId id, float amount)
        {
            if (_activeEvents.TryGetValue(id, out var evt) && evt is InternalEvent internalEvent)
            {
                internalEvent.ApplyRepair(amount);
            }
        }

        public IMiniGameTarget GetMiniGameTarget(string targetId)
        {
            foreach (var evt in _activeEvents.Values)
            {
                if (evt is IMiniGameTarget target && target.MiniGameTargetId == targetId)
                {
                    return target;
                }
            }
            return null;
        }

        public void ForceClearAll()
        {
            _eventsToTickCache.Clear();
            foreach (var evt in _activeEvents.Values)
            {
                _eventsToTickCache.Add(evt);
            }

            foreach (var evt in _eventsToTickCache)
            {
                if (_activeEvents.ContainsKey(evt.Id))
                {
                    evt.ForceTerminate();
                }
            }

            _activeEvents.Clear();

            Debug.Log($"<color=lime>[EventManager]</color> 모든 활성 이벤트 강제 종료 및 초기화.");
        }
    }
}
