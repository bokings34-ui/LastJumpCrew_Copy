using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    [RequireComponent(typeof(Collider))]
    public class FireEffectInstance : MonoBehaviour, IRepairable
    {
        [Header("데미지 틱 설정")]
        [SerializeField] private float tickInterval = 1f;

        private float _damagePerSecond;
        private float _maxRepairProgress;
        private float _repairProgress;
        private float _timer;

        public float RepairProgress { get { return _repairProgress; } }
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

        private void Update()
        {
            if (_targetsInRange.Count == 0) return;

            _timer += Time.deltaTime;

            if (_timer >= tickInterval)
            {
                _timer = 0f;
                foreach (var target in _targetsInRange)
                {
                    target.TakeDamage(_damagePerSecond * tickInterval);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var damageable = other.GetComponent<IDamageable>();

            if (damageable != null)
            {
                _targetsInRange.Add(damageable);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var damageable = other.GetComponent<IDamageable>();

            if (damageable != null)
            {
                _targetsInRange.Remove(damageable);
            }
        }
    }
}