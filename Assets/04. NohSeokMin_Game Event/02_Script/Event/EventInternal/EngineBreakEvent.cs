using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EngineBreakEvent : InternalEvent
    {
        private EngineBreakEventDataSO EngineData { get { return _data as EngineBreakEventDataSO; } }

        private float _repairProgress;
        private float _fuelLossTimer;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;
        private readonly List<IEngineBreakRepairTarget> _repairTargets = new();

        // 담당 매니저(워프 게이지 시스템)가 구독
        public event Action OnEngineBroken;
        public event Action OnFuelLoss;
        public event Action OnEngineRestored;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _repairProgress = 0f;
            _fuelLossTimer = 0f;

            if (!TryBindRepairTargets(out var reason))
            {
                Debug.LogError(
                    $"PHS_ENGINE_EVENT_FAILED reason={reason} event={InstanceId} room={RoomId}");
                CleanupRepairTargets();
                OnFail();
                return;
            }

            OnEngineBroken?.Invoke();

            Debug.Log($"<color=lime>[{EngineData.EventName}]</color> 발생. 엔진 고장 신호 발행.");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            _fuelLossTimer += deltaTime;
            if (_fuelLossTimer >= EngineData.fuelLossInterval)
            {
                _fuelLossTimer = 0f;
                OnFuelLoss?.Invoke();
            }
        }

        protected override float GetMaxRepairProgress() => EngineData.maxRepairProgress;

        public override void ApplyRepair(float amount)
        {
            if (State != EventState.InProgress || amount <= 0f) return;

            _repairProgress = Mathf.Min(
                EngineData.maxRepairProgress,
                _repairProgress + amount);
            if (_repairProgress >= EngineData.maxRepairProgress)
            {
                OnResolve();
            }
        }

        public override void OnResolve()
        {
            CleanupRepairTargets();
            ChangeState(EventState.Resolve);
            OnEngineRestored?.Invoke();
            Debug.Log($"<color=lime>[{EngineData.EventName}]</color> 엔진 수리 완료.");
        }

        public override void ForceTerminate()
        {
            CleanupRepairTargets();
            OnEngineRestored?.Invoke();
            base.ForceTerminate();
        }

        private bool TryBindRepairTargets(out string reason)
        {
            if (EngineData == null || EngineData.maxRepairProgress <= 0f)
            {
                reason = "engine_data_invalid";
                return false;
            }

            _repairRuntimeBridge =
                Context?.RuntimeBridge as IEventRepairRuntimeBridge;
            if (_repairRuntimeBridge == null)
            {
                reason = "repair_runtime_bridge_required";
                return false;
            }

            if (Context?.Room is not Component roomComponent)
            {
                reason = "room_component_required";
                return false;
            }

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude);
            foreach (var behaviour in behaviours)
            {
                if (behaviour.gameObject.scene != roomComponent.gameObject.scene
                    || behaviour is not IEngineBreakRepairTarget target)
                {
                    continue;
                }

                var effectInstanceId =
                    (Context.RuntimeBridge as IEventEffectRuntimeBridge)
                    ?.AllocateEffectInstanceId(InstanceId) ?? 0U;
                if (effectInstanceId == 0U
                    || !target.TryBindEngineBreak(
                        InstanceId,
                        effectInstanceId,
                        _repairRuntimeBridge,
                        TryApplyRepairStep))
                {
                    reason = $"repair_target_bind_failed:{behaviour.name}";
                    return false;
                }

                _repairTargets.Add(target);
            }

            if (_repairTargets.Count == 0)
            {
                reason = "reactor_repair_targets_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryApplyRepairStep(float amount)
        {
            if (State != EventState.InProgress || amount <= 0f)
            {
                return false;
            }

            ApplyRepair(amount);
            return true;
        }

        private void CleanupRepairTargets()
        {
            foreach (var target in _repairTargets)
            {
                target?.UnbindEngineBreak();
            }

            _repairTargets.Clear();
            _repairRuntimeBridge = null;
        }
    }
}
