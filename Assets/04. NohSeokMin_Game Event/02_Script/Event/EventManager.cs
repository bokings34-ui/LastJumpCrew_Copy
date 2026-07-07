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
            foreach (var evt in _activeEvents.Values)
            {
                evt.OnTick(Time.deltaTime);
            }
        }

        public void SpawnEvent(EventId id, IRoom targetRoom)
        {
            if (_activeEvents.ContainsKey(id))
            {
                Debug.Log($"<color=lime>[ShipEventManager]</color> {id}는 이미 진행 중입니다.");
                return;
            }

            var data = registry.GetData(id);

            if (data == null)
            {
                Debug.Log($"<color=lime>[ShipEventManager]</color> {id}에 대한 EventDataSO가 Registry에 없습니다.");
                return;
            }

            var evt = EventFactory.Create(id);
            var context = new EventContext(targetRoom, this);

            evt.OnFinished += HandleEventFinished;
            evt.Initialize(data, context);
            evt.OnTrigger();

            _activeEvents[id] = evt;
        }

        // TODO :: 수리 상호작용 시 호출해서 진행도 전달
        public void ApplyRepairTo(EventId id, float amount)
        {
            if (_activeEvents.TryGetValue(id, out var evt) && evt is IRepairable repairable)
            {
                repairable.ApplyRepair(amount);
            }
        }

        private void HandleEventFinished(EventBase evt, bool success)
        {
            evt.OnFinished -= HandleEventFinished;
            _activeEvents.Remove(evt.Id);

            Debug.Log($"<color=lime>[EventManager]</color> {evt.Id} 종료 (성공 여부 : {success})");
        }
    }
}