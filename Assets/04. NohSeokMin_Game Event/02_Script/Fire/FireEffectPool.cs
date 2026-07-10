using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class FireEffectPool : MonoSingleton<FireEffectPool>
    {
        [Header("이펙트 프리팹")]
        [SerializeField] private FireEffectInstance effectPrefab;
        [SerializeField] private int initialSize = 10;

        private readonly Queue<FireEffectInstance> _pool = new Queue<FireEffectInstance>();

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < initialSize; i++)
            {
                var obj = Instantiate(effectPrefab, transform);
                obj.gameObject.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        public FireEffectInstance Get(Vector3 position, float damagePerSecond, float maxRepairProgress)
        {
            var instance = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(effectPrefab, transform);

            instance.transform.position = position;
            instance.Activate(damagePerSecond, maxRepairProgress);
            return instance;
        }

        public void Return(FireEffectInstance instance)
        {
            instance.Deactivate();
            _pool.Enqueue(instance);
        }
    }
}