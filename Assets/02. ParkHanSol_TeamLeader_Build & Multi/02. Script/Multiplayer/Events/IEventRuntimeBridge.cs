using UnityEngine;

namespace SM
{
    public enum EventEffectKind : byte
    {
        Fire = 0,
        OxygenLeak = 1,
        Enemy = 2
    }

    public enum EventEffectLifecycle : byte
    {
        Active = 0,
        Removed = 1
    }

    public interface IEventRuntimeBridge
    {
        bool IsAuthoritative { get; }

        ulong AllocateEventInstanceId();

        void PublishEventStarted(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state);

        void PublishEventStateChanged(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state);

        void PublishEventFinished(
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state,
            bool success);
    }

    public interface IEventEffectRuntimeBridge
    {
        uint AllocateEffectInstanceId(ulong eventInstanceId);

        void PublishEffectSpawned(
            ulong eventInstanceId,
            uint effectInstanceId,
            EventEffectKind effectKind,
            Vector3 worldPosition,
            byte variant);

        void PublishEffectPositionChanged(
            ulong eventInstanceId,
            uint effectInstanceId,
            Vector3 worldPosition);

        void PublishEffectRemoved(ulong eventInstanceId, uint effectInstanceId);
    }

    public interface IShipPowerEventRuntimeBridge
    {
        bool TryApplyPowerOff(ulong eventInstanceId, out string reason);

        bool TryGetPowerOffState(out bool isPowerOff);
    }

    /// <summary>
    /// Server-authoritative lifecycle metadata and repair progress publication.
    /// Event implementations may use this optional bridge without changing the
    /// base lifecycle contract.
    /// </summary>
    public interface IEventProgressRuntimeBridge
    {
        bool BindEventIncidentContextServer(
            ulong instanceId,
            ulong commandId,
            string locationId);

        bool PublishEventProgressServer(
            ulong instanceId,
            float progress,
            float requiredProgress);
    }

    public interface IShipModuleEventRuntimeBridge
    {
        bool TryConfigureShipModuleImpactServer(
            float moduleDamageMultiplier,
            float shipDamageMultiplier,
            out string reason);

        bool TryApplyInitialImpact(
            ulong eventInstanceId,
            EventId eventId,
            ShipModuleEventDataSO data,
            out string reason);

        bool TryApplyPeriodicImpact(
            ulong eventInstanceId,
            EventId eventId,
            ShipModuleEventDataSO data,
            out string reason);

        bool TryApplyResolveRepair(
            ulong eventInstanceId,
            EventId eventId,
            ShipModuleEventDataSO data,
            out string reason);
    }
}
