using UnityEngine;

namespace SM
{
    [CreateAssetMenu(fileName = "EnemySpawnData", menuName = "SM/EventData/EnemySpawn")]
    public class EnemySpawnDataSO : EventDataSO
    {
        [Header("적 타입 프리팹")]
        public GameObject playerAttackEnemyPrefab;
        public GameObject deviceAttackEnemyPrefab;

        [Header("적 스폰 설정")]
        public int enemyCount = 3;
        public float spawnInterval = 1f;
    }
}