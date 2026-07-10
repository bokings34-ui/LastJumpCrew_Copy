using System;
using System.Collections.Generic;
using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    [RequireComponent(typeof(Collider))]
    public class FireEffectInstance : MonoBehaviour, IInteractable
    {
        [Header("데미지 틱 설정")]
        [SerializeField] private float tickInterval = 1f;

        private float _damagePerSecond;
        private float _maxRepairProgress;
        private float _repairProgress;
        private float _timer;

        public bool IsRepaired { get; private set; }
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
            _targetsInRange.Clear();
            gameObject.SetActive(false);
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

        public string InteractionPrompt { get { return "소화기 필요"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return false;
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

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
    }
}