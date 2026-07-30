using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EnemyPool : MonoSingleton<EnemyPool>
    {
        private readonly Dictionary<GameObject, Queue<EnemyBase>> _pools = new Dictionary<GameObject, Queue<EnemyBase>>();

        public EnemyBase Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<EnemyBase>();
                _pools[prefab] = queue;
            }

            EnemyBase instance;

            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
                instance.gameObject.SetActive(true);
                instance.Agent.Warp(position);

                bool warpSuccess = instance.Agent.Warp(position);
                Debug.Log($"[EnemyPool] Warp 결과: {warpSuccess}, 목표위치: {position}, 실제위치: {instance.transform.position}");

                instance.transform.rotation = rotation;
            }
            else
            {
                var obj = Instantiate(prefab, position, rotation, transform);
                instance = obj.GetComponent<EnemyBase>();
            }

            instance.Activate(prefab);
            return instance;
        }

        public void Return(GameObject prefab, EnemyBase instance)
        {
            instance.Deactivate();

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<EnemyBase>();
                _pools[prefab] = queue;
            }

            queue.Enqueue(instance);
        }
    }
}