using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class OxygenLeakEffectPool : MonoSingleton<OxygenLeakEffectPool>
    {
        [SerializeField] private OxygenLeakEffectInstance effectPrefab;
        [SerializeField] private int initialSize = 2;

        private readonly Queue<OxygenLeakEffectInstance> _pool = new Queue<OxygenLeakEffectInstance>();

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

        public OxygenLeakEffectInstance Get(Vector3 position, OxygenLeakEventDataSO data)
        {
            var instance = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(effectPrefab, transform);
            instance.transform.position = position;
            instance.Activate(data);
            return instance;
        }

        public void Return(OxygenLeakEffectInstance instance)
        {
            instance.Deactivate();
            _pool.Enqueue(instance);
        }
    }
}