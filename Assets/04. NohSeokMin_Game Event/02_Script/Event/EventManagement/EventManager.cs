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
        private readonly Dictionary<ulong, EventBase> _activeEventsByInstance = new Dictionary<ulong, EventBase>();
        private readonly List<EventBase> _eventsToTickCache = new List<EventBase>();
        private IEventRuntimeBridge _runtimeBridge;
        private ulong _nextLocalInstanceId;

        public bool HasRuntimeBridge { get { return _runtimeBridge != null; } }
        public bool IsRuntimeAuthority { get { return _runtimeBridge == null || _runtimeBridge.IsAuthoritative; } }

        private void Update()
        {
            if (!IsRuntimeAuthority)
            {
                return;
            }

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

        public bool IsActive(EventId id)
        {
            return _activeEvents.ContainsKey(id);
        }

        public bool IsInstanceActive(ulong instanceId)
        {
            return instanceId != 0UL && _activeEventsByInstance.ContainsKey(instanceId);
        }

        public bool ConfigureRuntimeBridge(IEventRuntimeBridge runtimeBridge)
        {
            if (runtimeBridge == null)
            {
                Debug.LogError("[EventManager] Runtime bridge is null.", this);
                return false;
            }

            if (_runtimeBridge != null && !ReferenceEquals(_runtimeBridge, runtimeBridge))
            {
                Debug.LogError("[EventManager] A different runtime bridge is already configured.", this);
                return false;
            }

            if (_activeEvents.Count > 0)
            {
                Debug.LogError("[EventManager] Runtime bridge cannot change while events are active.", this);
                return false;
            }

            _runtimeBridge = runtimeBridge;
            Debug.Log($"PHS_EVENT_RUNTIME_BRIDGE_CONFIGURED authority={runtimeBridge.IsAuthoritative}", this);
            return true;
        }

        public void ClearRuntimeBridge(IEventRuntimeBridge runtimeBridge)
        {
            if (runtimeBridge != null && ReferenceEquals(_runtimeBridge, runtimeBridge))
            {
                _runtimeBridge = null;
                Debug.Log("PHS_EVENT_RUNTIME_BRIDGE_CLEARED", this);
            }
        }

        public bool SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null)
        {
            return TrySpawnEvent(id, targetRoom, out _, onFinished);
        }

        public bool TrySpawnEvent(
            EventId id,
            IRoom targetRoom,
            out ulong instanceId,
            Action<EventBase, bool> onFinished = null)
        {
            instanceId = 0UL;

            if (!IsRuntimeAuthority)
            {
                Debug.LogWarning($"PHS_EVENT_SPAWN_REJECTED reason=not_authority event={id}", this);
                return false;
            }

            if (_activeEvents.ContainsKey(id))
            {
                Debug.Log($"<color=lime>[EventManager]</color> {id}는 이미 진행 중입니다.");
                return false;
            }

            if (registry == null)
            {
                Debug.LogError($"<color=lime>[EventManager]</color> EventRegistry가 연결되지 않았습니다.", this);
                return false;
            }

            if (targetRoom == null)
            {
                Debug.LogError($"<color=lime>[EventManager]</color> {id} 대상 Room이 없습니다.", this);
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
                Debug.LogError($"<color=lime>[EventManager]</color> {id} Event 생성기가 없습니다.", this);
                return false;
            }

            if (_runtimeBridge != null
                && RequiresReplicatedEffects(id)
                && _runtimeBridge is not IEventEffectRuntimeBridge)
            {
                Debug.LogError(
                    $"PHS_EVENT_SPAWN_REJECTED reason=effect_runtime_bridge_missing event={id}",
                    this);
                return false;
            }

            instanceId = AllocateInstanceId();
            if (instanceId == 0UL)
            {
                Debug.LogError($"PHS_EVENT_SPAWN_REJECTED reason=instance_id_missing event={id}", this);
                return false;
            }

            var context = new EventContext(instanceId, targetRoom, this, _runtimeBridge);

            evt.OnFinished += HandleEventFinished;
            if (onFinished != null) evt.OnFinished += onFinished;

            evt.Initialize(data, context);
            // OnTrigger 중 즉시 Resolve/Fail이 발생해도 완료 콜백이 이 항목을 제거할 수 있게
            // 활성 목록에 먼저 등록한다.
            _activeEvents[id] = evt;
            _activeEventsByInstance[instanceId] = evt;
            _runtimeBridge?.PublishEventStarted(
                instanceId,
                id,
                targetRoom.RoomId,
                evt.State);

            try
            {
                evt.OnTrigger();
                Debug.Log(
                    $"PHS_EVENT_SPAWN_ACCEPTED instance={instanceId} event={id} room={targetRoom.RoomId} state={evt.State}",
                    this);
                return true;
            }
            catch (Exception exception)
            {
                evt.OnFinished -= HandleEventFinished;
                if (onFinished != null) evt.OnFinished -= onFinished;
                _activeEvents.Remove(id);
                _activeEventsByInstance.Remove(instanceId);
                _runtimeBridge?.PublishEventFinished(
                    instanceId,
                    id,
                    targetRoom.RoomId,
                    EventState.Fail,
                    false);
                Debug.LogException(exception, this);
                return false;
            }
        }

        private ulong AllocateInstanceId()
        {
            if (_runtimeBridge != null)
            {
                return _runtimeBridge.AllocateEventInstanceId();
            }

            _nextLocalInstanceId++;
            if (_nextLocalInstanceId == 0UL)
            {
                _nextLocalInstanceId++;
            }

            return _nextLocalInstanceId;
        }

        private static bool RequiresReplicatedEffects(EventId id)
        {
            return id == EventId.Fire
                || id == EventId.OxygenLeak
                || id == EventId.EnemySpawn;
        }

        private void HandleEventFinished(EventBase evt, bool success)
        {
            evt.OnFinished -= HandleEventFinished;
            _activeEvents.Remove(evt.Id);
            _activeEventsByInstance.Remove(evt.InstanceId);
            _runtimeBridge?.PublishEventFinished(
                evt.InstanceId,
                evt.Id,
                evt.RoomId,
                evt.State,
                success);

            Debug.Log($"PHS_EVENT_FINISHED instance={evt.InstanceId} event={evt.Id} success={success}", this);
        }

        public void ApplyRepairTo(EventId id, float amount)
        {
            if (!IsRuntimeAuthority)
            {
                Debug.LogWarning($"PHS_EVENT_REPAIR_REJECTED reason=not_authority event={id}", this);
                return;
            }

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
            if (!IsRuntimeAuthority)
            {
                Debug.LogWarning("PHS_EVENT_FORCE_CLEAR_REJECTED reason=not_authority", this);
                return;
            }

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
            _activeEventsByInstance.Clear();

            Debug.Log($"<color=lime>[EventManager]</color> 모든 활성 이벤트 강제 종료 및 초기화.");
        }
    }
}
