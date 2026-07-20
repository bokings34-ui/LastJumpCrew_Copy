using LastJumpCrew.Common;
using SM;
using UnityEngine;

public class TestDevice : MonoBehaviour, IDevice, IDamageable
{
    [Header("체력 설정")]
    [SerializeField] private int maxHealth = 99999;
    private int _currentHealth;
    public bool IsAlive => _currentHealth > 0;

    public Transform Transform => transform;

    private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

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
