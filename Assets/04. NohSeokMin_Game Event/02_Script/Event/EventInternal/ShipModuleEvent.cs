using UnityEngine;

namespace SM
{
    public abstract class ShipModuleEvent : InternalEvent
    {
        private float repairProgress;
        private float damageTimer;
        private IEventProgressRuntimeBridge progressBridge;
        private IShipModuleEventRuntimeBridge shipModuleBridge;

        protected ShipModuleEventDataSO ShipModuleData => _data as ShipModuleEventDataSO;

        public float RepairProgress => repairProgress;
        public float RequiredRepairProgress =>
            ShipModuleData == null ? 0f : ShipModuleData.RequiredRepairProgress;

        public override void OnTrigger()
        {
            if (ShipModuleData == null)
            {
                Debug.LogError($"PHS_SHIP_MODULE_EVENT_FAILED reason=data_type event={Id}");
                OnFail();
                return;
            }

            repairProgress = 0f;
            damageTimer = 0f;
            progressBridge = Context?.RuntimeBridge as IEventProgressRuntimeBridge;
            shipModuleBridge = Context?.RuntimeBridge as IShipModuleEventRuntimeBridge;

            if (shipModuleBridge != null
                && !shipModuleBridge.TryApplyInitialImpact(
                    InstanceId,
                    Id,
                    ShipModuleData,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_SHIP_MODULE_EVENT_FAILED reason=initial_impact:{reason} event={Id} instance={InstanceId}");
                OnFail();
                return;
            }

            ChangeState(EventState.InProgress);
            PublishProgress();
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress
                || shipModuleBridge == null
                || (ShipModuleData.PeriodicModuleDamage <= 0
                    && ShipModuleData.PeriodicShipDamage <= 0))
            {
                return;
            }

            damageTimer += deltaTime;
            if (damageTimer < ShipModuleData.DamageIntervalSeconds)
            {
                return;
            }

            damageTimer = 0f;
            if (!shipModuleBridge.TryApplyPeriodicImpact(
                InstanceId,
                Id,
                ShipModuleData,
                out var reason))
            {
                Debug.LogError(
                    $"PHS_SHIP_MODULE_EVENT_FAILED reason=periodic_impact:{reason} event={Id} instance={InstanceId}");
                OnFail();
            }
        }

        public override void ApplyRepair(float amount)
        {
            if (State != EventState.InProgress || amount <= 0f)
            {
                return;
            }

            repairProgress = Mathf.Min(
                repairProgress + amount,
                ShipModuleData.RequiredRepairProgress);
            PublishProgress();

            if (repairProgress >= ShipModuleData.RequiredRepairProgress)
            {
                OnResolve();
            }
        }

        public override void OnResolve()
        {
            if (State != EventState.InProgress)
            {
                return;
            }

            if (shipModuleBridge != null
                && !shipModuleBridge.TryApplyResolveRepair(
                    InstanceId,
                    Id,
                    ShipModuleData,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_SHIP_MODULE_EVENT_RESOLVE_REJECTED reason={reason} event={Id} instance={InstanceId}");
                return;
            }

            ChangeState(EventState.Resolve);
        }

        public override void ForceTerminate()
        {
            if (State == EventState.InProgress && shipModuleBridge != null)
            {
                shipModuleBridge.TryApplyResolveRepair(
                    InstanceId,
                    Id,
                    ShipModuleData,
                    out _);
            }

            base.ForceTerminate();
        }

        protected override float GetMaxRepairProgress()
        {
            return RequiredRepairProgress;
        }

        private void PublishProgress()
        {
            progressBridge?.PublishEventProgressServer(
                InstanceId,
                repairProgress,
                RequiredRepairProgress);
        }
    }
}
