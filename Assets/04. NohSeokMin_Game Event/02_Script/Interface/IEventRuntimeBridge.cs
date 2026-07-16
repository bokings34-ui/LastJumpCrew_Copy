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

    /// <summary>
    /// Keeps the event domain independent from the active runtime transport.
    /// Offline scenes leave this bridge unset; network scenes provide a server-authoritative bridge.
    /// </summary>
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

    /// <summary>
    /// Server-owned effect replication contract. Gameplay instances stay authoritative while
    /// remote peers consume presentation-only snapshots.
    /// </summary>
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

    /// <summary>
    /// Server-owned ship power sink used by the PowerOff event.
    /// The event domain only knows this contract and does not depend on the active ship implementation.
    /// </summary>
    public interface IShipPowerEventRuntimeBridge
    {
        bool TryApplyPowerOff(ulong eventInstanceId, out string reason);

        bool TryGetPowerOffState(out bool isPowerOff);
    }
}
