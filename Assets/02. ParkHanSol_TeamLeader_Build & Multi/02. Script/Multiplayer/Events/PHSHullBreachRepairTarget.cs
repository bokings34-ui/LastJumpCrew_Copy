using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSHullBreachRepairTarget : MonoBehaviour, IEventRepairableEffect, IUtilityAttackTarget
    {
        private NetworkEventCoordinator coordinator;
        private PHSHullBreachRepairSite site;
        private bool repairComplete;

        public ulong EventInstanceId { get; private set; }
        public uint EffectInstanceId { get; private set; }
        public EventEffectKind EffectKind => EventEffectKind.HullBreach;
        public string RequiredItemId => "foam_sealant_gun";
        public string InteractionPrompt => "실란트 건 필요";
        public Vector3 RepairPosition => site == null ? transform.position : site.RepairPosition;
        public bool IsRepairComplete => repairComplete;

        public bool TryConfigure(NetworkEventCoordinator valueCoordinator, ulong eventInstanceId, uint effectInstanceId,
            PHSHullBreachRepairSite valueSite, out string reason)
        {
            coordinator = valueCoordinator;
            site = valueSite;
            EventInstanceId = eventInstanceId;
            EffectInstanceId = effectInstanceId;
            repairComplete = false;
            if (coordinator == null || site == null || eventInstanceId == 0UL || effectInstanceId == 0U)
            {
                reason = "repair_target_configuration_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryGetRepairPoint(Vector3 actorPosition, out Vector3 repairPoint)
        {
            repairPoint = RepairPosition;
            var bounds = site == null ? null : site.GetComponent<Collider>();
            if (bounds == null || !bounds.isTrigger || !bounds.bounds.Contains(actorPosition))
            {
                return false;
            }

            repairPoint = bounds.ClosestPoint(actorPosition);
            return true;
        }

        public bool TryApplyRepairStep(float amount)
        {
            if (repairComplete || amount <= 0f || coordinator == null)
            {
                return false;
            }

            if (!coordinator.TryResolveHullBreachServer(EventInstanceId, out var reason))
            {
                Debug.LogError($"PHS_HULL_BREACH_REPAIR_FAILED reason={reason} event={EventInstanceId}", this);
                return false;
            }

            repairComplete = true;
            return true;
        }

        public bool CanInteract(LastJumpCrew.Common.IItemHolder itemHolder) => !repairComplete && itemHolder != null && itemHolder.HasItem
            && itemHolder.CurrentItem != null && itemHolder.CurrentItem.ItemId == RequiredItemId;
        public void Interact(LastJumpCrew.Common.IItemHolder itemHolder) { }
        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            if (repairComplete || hit.ItemId != RequiredItemId || hit.Attacker == null || hit.RequestSequence == 0U)
            {
                return false;
            }

            var record = hit.Attacker.GetComponentInParent<NetworkPlayerItemRecord>();
            return record != null && coordinator != null && coordinator.RequestEffectRepair(this, record, hit.RequestSequence);
        }
    }
}
