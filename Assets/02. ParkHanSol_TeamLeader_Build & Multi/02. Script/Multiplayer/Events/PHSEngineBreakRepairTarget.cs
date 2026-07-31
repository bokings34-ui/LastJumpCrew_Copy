using System;
using LastJumpCrew.Common;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSEngineBreakRepairTarget :
        MonoBehaviour,
        IEngineBreakRepairTarget,
        IUtilityAttackTarget
    {
        private const string WrenchItemId = "wrench";

        [Header("Inspector References")]
        [SerializeField] private Collider repairHitVolume;

        private IEventRepairRuntimeBridge repairRuntimeBridge;
        private Func<float, bool> repairStep;

        public ulong EventInstanceId { get; private set; }
        public uint EffectInstanceId { get; private set; }
        public EventEffectKind EffectKind => EventEffectKind.EngineBreak;
        public string RequiredItemId => WrenchItemId;
        public Vector3 RepairPosition => repairHitVolume != null
            ? repairHitVolume.ClosestPoint(transform.position)
            : transform.position;
        public bool IsRepairComplete => repairStep == null;
        public string InteractionPrompt => "렌치로 엔진 수리";

        private void Awake()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_ENGINE_REPAIR_TARGET_SETUP_FAILED reason={reason} target={name}",
                    this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            UnbindEngineBreak();
        }

        public bool CanInteract(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            return !IsRepairComplete
                && itemHolder != null
                && itemHolder.HasItem
                && itemHolder.CurrentItem != null
                && itemHolder.CurrentItem.ItemId == WrenchItemId;
        }

        public void Interact(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            if (itemHolder is Component holder)
            {
                RequestRepair(holder.gameObject, NextRequestSequence());
            }
        }

        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            return hit.Attacker != null
                && hit.ItemId == WrenchItemId
                && hit.RequestSequence != 0U
                && RequestRepair(hit.Attacker, hit.RequestSequence);
        }

        public bool TryBindEngineBreak(
            ulong eventInstanceId,
            uint effectInstanceId,
            IEventRepairRuntimeBridge runtimeBridge,
            Func<float, bool> eventRepairStep)
        {
            UnbindEngineBreak();
            if (!enabled
                || eventInstanceId == 0UL
                || effectInstanceId == 0U
                || runtimeBridge == null
                || eventRepairStep == null)
            {
                return false;
            }

            EventInstanceId = eventInstanceId;
            EffectInstanceId = effectInstanceId;
            repairRuntimeBridge = runtimeBridge;
            repairStep = eventRepairStep;
            if (repairRuntimeBridge.RegisterRepairTarget(this))
            {
                return true;
            }

            ClearBinding();
            return false;
        }

        public void UnbindEngineBreak()
        {
            if (repairRuntimeBridge != null
                && EventInstanceId != 0UL
                && EffectInstanceId != 0U)
            {
                repairRuntimeBridge.UnregisterRepairTarget(
                    EventInstanceId,
                    EffectInstanceId);
            }

            ClearBinding();
        }

        public bool TryApplyRepairStep(float amount)
        {
            return repairStep != null
                && amount > 0f
                && repairStep(amount);
        }

        public bool TryValidate(out string reason)
        {
            if (repairHitVolume == null)
            {
                reason = "repair_hit_volume_missing";
                return false;
            }

            if (!repairHitVolume.enabled)
            {
                reason = "repair_hit_volume_disabled";
                return false;
            }

            reason = null;
            return true;
        }

        private uint requestSequence;

        private uint NextRequestSequence()
        {
            requestSequence++;
            if (requestSequence == 0U)
            {
                requestSequence = 1U;
            }

            return requestSequence;
        }

        private bool RequestRepair(GameObject attacker, uint sequence)
        {
            if (repairRuntimeBridge == null || attacker == null)
            {
                return false;
            }

            var itemRecord = attacker.GetComponentInParent<NetworkPlayerItemRecord>();
            if (itemRecord == null)
            {
                itemRecord = attacker.GetComponentInChildren<NetworkPlayerItemRecord>(true);
            }

            if (itemRecord == null)
            {
                Debug.LogError(
                    $"PHS_ENGINE_REPAIR_REQUEST_REJECTED reason=item_record_missing target={name}",
                    this);
                return false;
            }

            return repairRuntimeBridge.RequestEffectRepair(
                this,
                itemRecord,
                sequence);
        }

        private void ClearBinding()
        {
            EventInstanceId = 0UL;
            EffectInstanceId = 0U;
            repairRuntimeBridge = null;
            repairStep = null;
        }
    }
}
