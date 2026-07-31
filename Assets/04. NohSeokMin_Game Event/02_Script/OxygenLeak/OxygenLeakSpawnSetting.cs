using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class OxygenLeakSpawnSetting : MonoSingleton<OxygenLeakSpawnSetting>
    {
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        public Transform GetRandomPoint()
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return null;
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }
    }
}