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
        private IOxygenLeakZoneProvider _zoneProvider;
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
            _zoneProvider = null;
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
                $"event={InstanceId}");
            OnFail();
        }

        private bool TryStart(out string reason)
        {
            if (!_zoneProvider.TryAcquireZone(out _zone, out var zoneReason)
                || _zone == null)
            {
                reason = $"oxygen_zone_acquire_failed:{zoneReason ?? "zone_missing"}";
                return false;
            }

            var leakPosition = _zone.RepairPosition;

            _effect = _effectPool.Get(
                leakPosition,
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
                leakPosition,
                0);
            Debug.Log(
                $"PHS_OXYGEN_EVENT_STARTED event={InstanceId} " +
                $"room={Context.Room.RoomId} zone={_zone.ZoneId} " +
                $"position={leakPosition}");
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
            ReleaseOxygenZone();
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

            ReleaseOxygenZone();
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

            var roomComponent = Context?.Room as Component;
            if (roomComponent == null)
            {
                reason = "room_component_required";
                return false;
            }

            _zoneProvider =
                roomComponent.GetComponent<IOxygenLeakZoneProvider>();
            if (_zoneProvider == null)
            {
                reason = "oxygen_zone_provider_required";
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

            ReleaseOxygenZone();
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

        private void ReleaseOxygenZone()
        {
            var zone = _zone;
            _zone = null;
            if (zone == null)
            {
                return;
            }

            try
            {
                zone.Deactivate();
            }
            catch (System.Exception exception)
            {
                LogCleanupFailure("oxygen_zone", exception);
            }
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
