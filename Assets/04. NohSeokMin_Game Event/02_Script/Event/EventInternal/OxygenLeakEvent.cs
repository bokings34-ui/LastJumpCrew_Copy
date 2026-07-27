using UnityEngine;

namespace SM
{
    public class OxygenLeakEvent : InternalEvent
    {
        private OxygenLeakEventDataSO LeakData =>
            _data as OxygenLeakEventDataSO;

        private OxygenLeakEffectInstance _effect;
        private OxygenLeakEffectPool _effectPool;
        private IEventEffectRuntimeBridge _effectRuntimeBridge;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;
        private IOxygenLeakZone _zone;
        private uint _effectInstanceId;
        private bool _effectSpawnPublishAttempted;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _effect = null;
            _effectPool = null;
            _effectRuntimeBridge = null;
            _repairRuntimeBridge = null;
            _zone = null;
            _effectInstanceId = 0U;
            _effectSpawnPublishAttempted = false;

            if (!TryValidateStartDependencies(out var dependencyReason))
            {
                Debug.LogError(
                    $"PHS_OXYGEN_EVENT_FAILED " +
                    $"reason={dependencyReason} " +
                    $"event={InstanceId}");
                OnFail();
                return;
            }

            string startReason;
            try
            {
                if (TryStart(out startReason))
                {
                    return;
                }
            }
            catch (System.Exception exception)
            {
                CleanupFailedStart();
                Debug.LogError(
                    $"PHS_OXYGEN_EVENT_FAILED " +
                    $"reason=start_exception " +
                    $"event={InstanceId} " +
                    $"exception={exception.GetType().Name}:" +
                    $"{exception.Message}");
                OnFail();
                return;
            }

            CleanupFailedStart();
            Debug.LogError(
                $"PHS_OXYGEN_EVENT_FAILED reason={startReason} " +
                $"event={InstanceId} room={Context.Room?.RoomId}");
            OnFail();
        }

        private bool TryStart(out string reason)
        {
            if (!TryAcquireRoomZone(out reason))
            {
                return false;
            }

            _effect = _effectPool.Get(
                _zone.RepairPosition,
                LeakData,
                true);
            if (_effect == null)
            {
                reason = "effect_pool_returned_null";
                return false;
            }

            _effectInstanceId =
                _effectRuntimeBridge.AllocateEffectInstanceId(InstanceId);
            if (_effectInstanceId == 0U)
            {
                reason = "effect_id_missing";
                return false;
            }

            if (!_effect.BindRepairTarget(
                    InstanceId,
                    _effectInstanceId,
                    _repairRuntimeBridge))
            {
                reason = "repair_target_registration";
                return false;
            }

            _effect.OnSealed += HandleSealed;
            _effectSpawnPublishAttempted = true;
            _effectRuntimeBridge.PublishEffectSpawned(
                InstanceId,
                _effectInstanceId,
                EventEffectKind.OxygenLeak,
                _zone.RepairPosition,
                0);
            reason = null;
            return true;
        }

        public override void OnTick(float deltaTime)
        {
        }

        private void HandleSealed(OxygenLeakEffectInstance effect)
        {
            effect.OnSealed -= HandleSealed;
            effect.UnbindRepairTarget();
            PublishEffectRemoved();
            _effectPool.Return(effect);
            _effect = null;
            ReleaseZone();
            OnResolve();
        }

        protected override float GetMaxRepairProgress()
        {
            return 0f;
        }

        public override void ApplyRepair(float amount)
        {
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
            Debug.Log(
                $"<color=lime>[{LeakData.EventName}]</color> " +
                "산소 누출 수리 완료.");
        }

        public override void ForceTerminate()
        {
            if (_effect != null)
            {
                _effect.OnSealed -= HandleSealed;
                _effect.UnbindRepairTarget();
                PublishEffectRemoved();
                _effectPool.Return(_effect);
                _effect = null;
            }

            ReleaseZone();
            base.ForceTerminate();
        }

        private bool TryValidateStartDependencies(out string reason)
        {
            if (LeakData == null)
            {
                reason = "event_data_required";
                return false;
            }

            var runtimeBridge = Context?.RuntimeBridge;
            if (runtimeBridge == null || !runtimeBridge.IsAuthoritative)
            {
                reason = "authoritative_bridge_required";
                return false;
            }

            _effectRuntimeBridge = runtimeBridge as IEventEffectRuntimeBridge;
            if (_effectRuntimeBridge == null)
            {
                reason = "effect_runtime_bridge_required";
                return false;
            }

            _repairRuntimeBridge = runtimeBridge as IEventRepairRuntimeBridge;
            if (_repairRuntimeBridge == null)
            {
                reason = "repair_runtime_bridge_required";
                return false;
            }

            if (!OxygenLeakEffectPool.HasInstance
                || OxygenLeakEffectPool.Peek() == null)
            {
                reason = "effect_pool_required";
                return false;
            }

            _effectPool = OxygenLeakEffectPool.Peek();
            reason = null;
            return true;
        }

        private void CleanupFailedStart()
        {
            if (_effect != null)
            {
                var effect = _effect;
                _effect = null;
                effect.OnSealed -= HandleSealed;

                try
                {
                    effect.UnbindRepairTarget();
                }
                catch (System.Exception exception)
                {
                    LogCleanupFailure("repair_target", exception);
                }

                try
                {
                    _effectPool.Return(effect);
                }
                catch (System.Exception exception)
                {
                    LogCleanupFailure("effect_pool", exception);
                }
            }

            if (_effectSpawnPublishAttempted)
            {
                try
                {
                    PublishEffectRemoved();
                }
                catch (System.Exception exception)
                {
                    LogCleanupFailure("effect_snapshot", exception);
                }
            }
            else
            {
                _effectInstanceId = 0U;
            }

            if (_zone == null)
            {
                return;
            }

            try
            {
                _zone.Deactivate();
            }
            catch (System.Exception exception)
            {
                LogCleanupFailure("oxygen_zone", exception);
            }
            finally
            {
                _zone = null;
            }
        }

        private void LogCleanupFailure(
            string target,
            System.Exception exception)
        {
            Debug.LogError(
                $"PHS_OXYGEN_EVENT_CLEANUP_FAILED " +
                $"target={target} event={InstanceId} " +
                $"exception={exception.GetType().Name}:" +
                $"{exception.Message}");
        }

        private bool TryAcquireRoomZone(out string reason)
        {
            _zone = null;
            if (Context?.Room is not Component roomComponent)
            {
                reason = "room_component_missing";
                return false;
            }

            var behaviours = roomComponent.GetComponents<MonoBehaviour>();
            IOxygenLeakZoneProvider provider = null;
            foreach (var behaviour in behaviours)
            {
                if (behaviour is not IOxygenLeakZoneProvider candidate)
                {
                    continue;
                }

                if (provider != null)
                {
                    reason = "zone_provider_duplicate";
                    return false;
                }

                provider = candidate;
            }

            if (provider == null)
            {
                reason = "zone_provider_missing";
                return false;
            }

            if (!provider.TryAcquireZone(out _zone, out reason)
                || _zone == null)
            {
                reason = string.IsNullOrWhiteSpace(reason)
                    ? "zone_acquire_failed"
                    : $"zone_acquire_failed:{reason}";
                return false;
            }

            reason = null;
            return true;
        }

        private void ReleaseZone()
        {
            _zone?.Deactivate();
            _zone = null;
        }

        private void PublishEffectRemoved()
        {
            if (_effectInstanceId == 0U)
            {
                _effectSpawnPublishAttempted = false;
                return;
            }

            var effectInstanceId = _effectInstanceId;
            _effectInstanceId = 0U;
            _effectSpawnPublishAttempted = false;
            _effectRuntimeBridge?.PublishEffectRemoved(
                InstanceId,
                effectInstanceId);
        }
    }
}
