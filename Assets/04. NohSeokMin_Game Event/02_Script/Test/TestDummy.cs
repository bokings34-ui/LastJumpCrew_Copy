using UnityEngine;

namespace SM
{
    public class TestDummy : MonoBehaviour, IDamageable
    {
        public void TakeDamage(float amount)
        {
            Debug.Log($"{name}이(가) {amount:F1} 데미지를 받음");
        }
    }
}