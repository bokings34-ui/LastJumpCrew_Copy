using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EnemySpawnSetting : MonoSingleton<EnemySpawnSetting>
    {
        [SerializeField] private List<EnemySpawnGroup> spawnGroups = new List<EnemySpawnGroup>();

        public EnemySpawnGroup GetRandomPoint()
        {
            if (spawnGroups == null || spawnGroups.Count == 0) return null;
            return spawnGroups[Random.Range(0, spawnGroups.Count)];
        }
    }
}