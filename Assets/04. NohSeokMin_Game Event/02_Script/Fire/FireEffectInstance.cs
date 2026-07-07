using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    [RequireComponent(typeof(Collider))]
    public class FireEffectInstance : MonoBehaviour
    {
        [Header("데미지 틱 설정")]
        [SerializeField] private float tickInterval = 1f;

        private float _damagePerSecond;
        private float _timer;
        private readonly HashSet<IDamageable> _targetsInRange = new HashSet<IDamageable>();

        public void Activate(float damagePerSecond)
        {
            _damagePerSecond = damagePerSecond;
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