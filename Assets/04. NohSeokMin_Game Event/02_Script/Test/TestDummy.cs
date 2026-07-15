using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    public class TestDummy : MonoBehaviour, IDamageable
    {
        [Header("체력 설정")]
        [SerializeField] private int maxHealth = 10;
        private int _currentHealth;

        public bool IsAlive => _currentHealth > 0;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        private void OnEnable() { PlayerRegistry.Instance.Register(transform); }
        private void OnDisable() { PlayerRegistry.Peek()?.Unregister(transform); }

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);

            //Debug.Log($"<color=orange>[TestDummy]</color> {name}이(가) {attacker.name}에게 {amount} 데미지를 받음! (현재 체력: {_currentHealth}/{maxHealth})");

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log($"<color=red>[TestDummy]</color> {name}이(가) 사망했습니다.");

            gameObject.SetActive(false);
        }
    }
}