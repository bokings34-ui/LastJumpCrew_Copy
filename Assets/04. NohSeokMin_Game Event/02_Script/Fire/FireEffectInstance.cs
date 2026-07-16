using System;
using System.Collections.Generic;
using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    [RequireComponent(typeof(Collider))]
    public class FireEffectInstance :
        MonoBehaviour,
        IInteractable,
        IRequireHeldItem,
        IEventRepairableEffect
    {
        private const string FireExtinguisherItemId = "fire_extinguisher";
        [Header("데미지 틱 설정")]
        [SerializeField] private float tickInterval = 1f;

        private float _damagePerSecond;
        private float _maxRepairProgress;
        private float _repairProgress;
        private float _timer;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;

        public bool IsRepaired { get; private set; }
        public ulong EventInstanceId { get; private set; }
        public uint EffectInstanceId { get; private set; }
        public EventEffectKind EffectKind => EventEffectKind.Fire;
        public Vector3 RepairPosition => transform.position;
        public bool IsRepairComplete => IsRepaired;
        public event Action<FireEffectInstance> OnRemove;

        private readonly HashSet<IDamageable> _targetsInRange = new HashSet<IDamageable>();

        public void Activate(float damagePerSecond, float maxRepairProgress)
        {
            _damagePerSecond = damagePerSecond;
            _maxRepairProgress = maxRepairProgress;
            _repairProgress = 0f;
            IsRepaired = false;
            _timer = 0f;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            UnbindRepairTarget();
            _targetsInRange.Clear();
            gameObject.SetActive(false);
        }

        public bool BindRepairTarget(
            ulong eventInstanceId,
            uint effectInstanceId,
            IEventRepairRuntimeBridge repairRuntimeBridge)
        {
            UnbindRepairTarget();
            if (eventInstanceId == 0UL || effectInstanceId == 0U || repairRuntimeBridge == null)
            {
                return false;
            }

            EventInstanceId = eventInstanceId;
            EffectInstanceId = effectInstanceId;
            _repairRuntimeBridge = repairRuntimeBridge;
            if (_repairRuntimeBridge.RegisterRepairTarget(this))
            {
                return true;
            }

            EventInstanceId = 0UL;
            EffectInstanceId = 0U;
            _repairRuntimeBridge = null;
            return false;
        }

        public void UnbindRepairTarget()
        {
            if (_repairRuntimeBridge != null && EventInstanceId != 0UL && EffectInstanceId != 0U)
            {
                _repairRuntimeBridge.UnregisterRepairTarget(EventInstanceId, EffectInstanceId);
            }

            EventInstanceId = 0UL;
            EffectInstanceId = 0U;
            _repairRuntimeBridge = null;
        }

        private void Update()
        {
            if (_targetsInRange.Count == 0) return;

            _timer += Time.deltaTime;

            if (_timer >= tickInterval)
            {
                _timer = 0f;
                int damage = Mathf.RoundToInt(_damagePerSecond * tickInterval);

                foreach (var target in _targetsInRange)
                {
                    if (target.IsAlive)
                    {
                        target.ApplyDamage(damage, gameObject);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null) _targetsInRange.Add(damageable);
        }

        private void OnTriggerExit(Collider other)
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null) _targetsInRange.Remove(damageable);
        }

        // __________ IRequireHeldItem ______________

        public string RequiredItemId { get { return FireExtinguisherItemId; } }

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        // __________ IInteractable ______________

        public string InteractionPrompt { get { return "소화기 필요"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !IsRepaired && IsRequirementMet(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

        // ___________ 플레이어가 수리할 때 호출하는 함수 ____________
        public void ApplyRepair(float amount)
        {
            if (IsRepaired) return;

            _repairProgress += amount;

            if (_repairProgress >= _maxRepairProgress)
            {
                IsRepaired = true;
                OnRemove?.Invoke(this);
            }
        }

        public bool TryApplyRepairStep(float amount)
        {
            if (IsRepaired || amount <= 0f)
            {
                return false;
            }

            ApplyRepair(amount);
            return true;
        }
    }
}
