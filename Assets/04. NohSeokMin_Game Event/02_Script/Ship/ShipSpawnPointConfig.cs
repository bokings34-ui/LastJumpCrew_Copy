using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPointConfig : MonoSingleton<ShipSpawnPointConfig>
    {
        [SerializeField] private List<ShipSpawnPoint> spawnPoints = new List<ShipSpawnPoint>();

        [Header("자동 이웃 연결 설정")]
        [SerializeField] private float neighborConnectionRadius = 4f;
        [SerializeField] private bool autoConnectOnAwake = true;

        protected override void Awake()
        {
            base.Awake();

            if (autoConnectOnAwake)
            {
                AutoConnectNeighbors();
            }
        }

        public ShipSpawnPoint GetRandomPoint()
        {
            if (spawnPoints.Count == 0) return null;
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }

        [ContextMenu("Auto Connect Neighbors")]
        public void AutoConnectNeighbors()
        {
            int connectionCount = 0;

            foreach (var point in spawnPoints)
            {
                point.ClearNeighbors();
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                for (int j = i + 1; j < spawnPoints.Count; j++)
                {
                    var a = spawnPoints[i];
                    var b = spawnPoints[j];

                    float dist = Vector3.Distance(a.transform.position, b.transform.position);

                    if (dist <= neighborConnectionRadius)
                    {
                        a.AddNeighbor(b);
                        b.AddNeighbor(a);
                        connectionCount++;
                    }
                }
            }
        }
    }
}