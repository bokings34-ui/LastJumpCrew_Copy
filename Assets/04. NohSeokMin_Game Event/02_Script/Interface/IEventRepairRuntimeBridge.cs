using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public interface IEventRepairTargetHandle : IInteractable
    {
        ulong EventInstanceId { get; }
        uint EffectInstanceId { get; }
        EventEffectKind EffectKind { get; }
        string RequiredItemId { get; }
    }

    public interface IEventRepairableEffect : IEventRepairTargetHandle
    {
        Vector3 RepairPosition { get; }
        bool IsRepairComplete { get; }
        bool TryApplyRepairStep(float amount);
    }

    public interface IEventRepairRuntimeBridge
    {
        bool RegisterRepairTarget(IEventRepairableEffect target);
        void UnregisterRepairTarget(ulong eventInstanceId, uint effectInstanceId);
    }
}
