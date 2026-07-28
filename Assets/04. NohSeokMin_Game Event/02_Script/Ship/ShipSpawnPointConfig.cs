using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPointConfig : MonoSingleton<ShipSpawnPointConfig>
    {
        [SerializeField] private List<ShipSpawnPoint> spawnPoints = new List<ShipSpawnPoint>();
        [SerializeField] private float neighborConnectionRadius = 5f;
        [SerializeField] private bool autoConnectOnAwake = true;

        protected override void Awake()
        {
            base.Awake();
            if (autoConnectOnAwake) AutoConnectNeighbors();
        }

        // 점유되지 않은 포인트 중 랜덤 (Fire 첫 발화, EnemySpawn, OxygenLeak 공용)
        public ShipSpawnPoint GetRandomFreePoint()
        {
            var free = new List<ShipSpawnPoint>();
            foreach (var point in spawnPoints)
            {
                if (point.IsFree) free.Add(point);
            }

            if (free.Count == 0) return null;
            return free[Random.Range(0, free.Count)];
        }

        // Fire 확산 전용: 이미 점유된 포인트들의 "비어있는 이웃"만 후보
        public ShipSpawnPoint GetRandomFreeNeighbor(IEnumerable<ShipSpawnPoint> occupiedPoints)
        {
            var candidates = new List<ShipSpawnPoint>();
            foreach (var occupied in occupiedPoints)
            {
                foreach (var neighbor in occupied.Neighbors)
                {
                    if (neighbor.IsFree && !candidates.Contains(neighbor))
                        candidates.Add(neighbor);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        [ContextMenu("Auto Connect Neighbors")]
        public void AutoConnectNeighbors()
        {
            foreach (var point in spawnPoints) point.ClearNeighbors();

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                for (int j = i + 1; j < spawnPoints.Count; j++)
                {
                    var a = spawnPoints[i];
                    var b = spawnPoints[j];
                    if (Vector3.Distance(a.transform.position, b.transform.position) <= neighborConnectionRadius)
                    {
                        a.AddNeighbor(b);
                        b.AddNeighbor(a);
                    }
                }
            }
        }
    }
}