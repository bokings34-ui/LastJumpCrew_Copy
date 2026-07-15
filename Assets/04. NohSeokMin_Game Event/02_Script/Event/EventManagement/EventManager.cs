using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EventManager : MonoSingleton<EventManager>, IEventSpawner
    {
        [Header("이벤트 데이터 레지스트리")]
        [SerializeField] private EventRegistrySO registry;

        private readonly Dictionary<EventId, EventBase> _activeEvents = new Dictionary<EventId, EventBase>();

        private void Update()
        {
            var activeEvents = new List<EventBase>(_activeEvents.Values);
            foreach (var evt in activeEvents)
            {
                if (_activeEvents.ContainsKey(evt.Id))
                {
                    evt.OnTick(Time.deltaTime);
                }
            }
        }

        public bool IsActive(EventId id)
        {
            return _activeEvents.ContainsKey(id);
        }

        public bool SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null)
        {
            if (_activeEvents.ContainsKey(id))
            {
                Debug.Log($"<color=lime>[EventManager]</color> {id}는 이미 진행 중입니다.");
                return false;
            }

            var data = registry.GetData(id);

            if (data == null)
            {
                Debug.LogError($"[EventManager] {id}에 대한 EventDataSO가 Registry에 없습니다.", this);
                return false;
            }

            var evt = EventFactory.Create(id);
            if (evt == null)
            {
                Debug.LogError($"[EventManager] {id} 이벤트 생성에 실패했습니다.", this);
                return false;
            }

            var context = new EventContext(targetRoom, this);

            evt.OnFinished += HandleEventFinished;
            if (onFinished != null) evt.OnFinished += onFinished;

            evt.Initialize(data, context);
            _activeEvents[id] = evt;
            evt.OnTrigger();

            return true;
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
            foreach (var evt in _activeEvents.Values)
            {
                evt.ForceTerminate();
            }

            _activeEvents.Clear();

            Debug.Log($"<color=lime>[EventManager]</color> 모든 활성 이벤트 강제 종료 및 초기화.");
        }
    }
}
