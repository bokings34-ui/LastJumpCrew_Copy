using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPointConfig : MonoSingleton<ShipSpawnPointConfig>
    {
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        public Transform GetRandomPoint()
        {
            if (spawnPoints.Count == 0) return null;
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }

        public Transform GetRandomFreePoint(ICollection<Transform> occupiedPoints)
        {
            var free = new List<Transform>();
            foreach (var point in spawnPoints)
            {
                if (!occupiedPoints.Contains(point))
                    free.Add(point);
            }

            if (free.Count == 0) return null;
            return free[Random.Range(0, free.Count)];
        }
    }
}